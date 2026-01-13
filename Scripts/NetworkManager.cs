using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

/// <summary>
/// Maneja la conexión WebSocket con el servidor y envío/recepción de mensajes.
/// </summary>
public partial class NetworkManager : Node
{
	[Export] public string ServerUrl = "ws://127.0.0.1:8080/ws";
	[Export] public int PlayerId = 1;
	[Export] public string PlayerName = "Player1";
	[Export] public float ReconnectDelay = 2.0f;
	[Export] public int MaxReconnectAttempts = 10;
	
	private WebSocketPeer webSocket;
	private GameState gameState;
	private SnapshotParser snapshotParser;
	private float reconnectTimer = 0f;
	private int reconnectAttempts = 0;
	private bool isReady = false;
	private bool isJoined = false;
	private float joinTimer = 0f;
	private bool joinSent = false;
	
	// Eventos
	[Signal]
	public delegate void OnConnectedEventHandler();
	
	[Signal]
	public delegate void OnDisconnectedEventHandler();
	
	[Signal]
	public delegate void OnSnapshotReceivedEventHandler();
	
	[Signal]
	public delegate void OnErrorReceivedEventHandler(string error);
	
	[Signal]
	public delegate void OnAckReceivedEventHandler(bool ok, string msg);
	
	public override void _Ready()
	{
		GD.Print("🔧 NetworkManager._Ready() iniciado");
		gameState = new GameState();
		gameState.PlayerName = PlayerName;
		snapshotParser = new SnapshotParser();
		
		GD.Print($"🔧 Intentando conectar a {ServerUrl}");
		Connect();
	}
	
	public override void _Process(double delta)
	{
		// Llamar Poll() siempre para procesar la conexión
		if (webSocket != null)
		{
			webSocket.Poll();
		}

		// Debug: mostrar estado
		if (webSocket != null)
		{
			var state = webSocket.GetReadyState();
			if (state == WebSocketPeer.State.Open)
			{
				// Enviar Join una sola vez cuando se abre
				if (!joinSent)
				{
					GD.Print("✅ WebSocket abierto, enviando Join...");
					SendJoin();
					joinSent = true;
				}
				{
					try
					{
						// En Godot 4, obtener el mensaje de texto
						byte[] data = webSocket.GetPacket();
						if (data != null && data.Length > 0)
						{
							string message = System.Text.Encoding.UTF8.GetString(data);
							ProcessMessage(message);
						}
					}
					catch (Exception e)
					{
						GD.PrintErr($"Error recibiendo mensaje: {e.Message}");
					}
				}
			}
		else if (state == WebSocketPeer.State.Connecting)
			{
				// Esperando conexión
				joinTimer += (float)delta;
				if (joinTimer > 1f && joinTimer < 1.1f)
				{
					GD.Print("⏳ Esperando apertura de WebSocket...");
				}
				if (joinTimer > 10f)
				{
					GD.PrintErr("❌ Timeout conexión WebSocket después de 10s");
					HandleDisconnect();
					joinTimer = 0f;
				}
			}
			else if (state == WebSocketPeer.State.Closed)
			{
				joinSent = false;
				HandleDisconnect();
			}
		}
		
		// Retry de reconexión
		if (webSocket == null || webSocket.GetReadyState() == WebSocketPeer.State.Closed)
		{
			reconnectTimer += (float)delta;
			if (reconnectTimer >= ReconnectDelay && reconnectAttempts < MaxReconnectAttempts)
			{
				reconnectTimer = 0f;
				Connect();
			}
		}
	}
	
	private void Connect()
	{
		GD.Print($"🔌 Conectando a {ServerUrl}...");
		webSocket = new WebSocketPeer();
		
		Error error = webSocket.ConnectToUrl(ServerUrl);
		if (error != Error.Ok)
		{
			GD.PrintErr($"❌ Error conectando WebSocket: {error}");
			HandleDisconnect();
		}
		else
		{
			GD.Print($"⏳ WebSocket en estado Connecting a {ServerUrl}");
		}
	}
	
	private void ProcessMessage(string json)
	{
		try
		{
			var jsonParser = new Json();
			Error parseError = jsonParser.Parse(json);
			if (parseError != Error.Ok)
			{
				GD.PrintErr($"❌ JSON inválido: {json}");
				return;
			}
			
			var message = (Godot.Collections.Dictionary)jsonParser.Data;
			string type = message["type"].ToString();
			GD.Print($"💬 Mensaje recibido: type={type}");
			
			switch (type)
			{
				case "ack":
					HandleAck(message);
					break;
					
				case "error":
					HandleError(message);
					break;
					
				case "snapshot":
					HandleSnapshot(json);
					break;
					
				default:
					GD.PrintErr($"❌ Tipo de mensaje desconocido: {type}");
					break;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"❌ Error procesando mensaje: {e.Message}");
		}
	}
	
	private void HandleAck(Godot.Collections.Dictionary message)
	{
		bool ok = message["ok"].AsBool();
		string msg = message.ContainsKey("msg") ? message["msg"].ToString() : "";
		
		if (ok && !isJoined)
		{
			isJoined = true;
			GD.Print("✅ Join aceptado por servidor");
			SendReady(true);
		}
		else if (ok && !isReady)
		{
			isReady = true;
			GD.Print("✅ Listo para jugar");
			EmitSignal(SignalName.OnConnected);
		}
		
		EmitSignal(SignalName.OnAckReceived, ok, msg);
	}
	
	private void HandleError(Godot.Collections.Dictionary message)
	{
		string error = message["error"].ToString();
		GD.PrintErr($"❌ Error del servidor: {error}");
		EmitSignal(SignalName.OnErrorReceived, error);
	}
	
	private void HandleSnapshot(string json)
	{
		try
		{
			var snapshot = snapshotParser.ParseSnapshot(json);
			GD.Print($"📦 Snapshot #{snapshot.Tick}: {snapshot.Units.Count} unidades, {snapshot.Buildings.Count} edificios, {snapshot.Resources.Count} recursos, {snapshot.Events.Count} eventos");
			GameState state = GetGameState();
			if (state != null)
			{
				GD.Print($"   Recursos del jugador: Wood={state.PlayerResources.Wood} Gold={state.PlayerResources.Gold} Stone={state.PlayerResources.Stone} Food={state.PlayerResources.Food}");
			}
			gameState.ApplySnapshot(snapshot);
			EmitSignal(SignalName.OnSnapshotReceived);
		}
		catch (Exception e)
		{
			GD.PrintErr($"❌ Error parseando snapshot: {e.Message}\n{e.StackTrace}");
		}
	}
	
	private void HandleDisconnect()
	{
		if (webSocket != null)
		{
			webSocket = null;
		}
		
		isJoined = false;
		isReady = false;
		reconnectAttempts++;
		
		GD.PrintErr($"❌ Desconectado. Intentando reconectar ({reconnectAttempts}/{MaxReconnectAttempts})...");
		EmitSignal(SignalName.OnDisconnected);
	}
	
	// API pública para enviar comandos
	
	public void SendMove(int unitId, float x, float y)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "move" },
			{ "unitId", unitId },
			{ "x", x },
			{ "y", y }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendAttack(int unitId, int targetId)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "attack" },
			{ "unitId", unitId },
			{ "targetId", targetId }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendGather(int unitId, int resourceId)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "gather" },
			{ "unitId", unitId },
			{ "resourceId", resourceId }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendBuild(int unitId, int buildingType, float x, float y)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "build" },
			{ "unitId", unitId },
			{ "buildingType", buildingType },
			{ "x", x },
			{ "y", y }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendTrain(int buildingId, int unitType)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "train" },
			{ "buildingId", buildingId },
			{ "unitType", unitType }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendDeposit(int unitId, int buildingId)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "deposit" },
			{ "unitId", unitId },
			{ "buildingId", buildingId }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	public void SendHunt(int unitId, int animalId)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "hunt" },
			{ "unitId", unitId },
			{ "animalId", animalId }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	private void SendReady(bool ready)
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "ready" },
			{ "ready", ready }
		};
		SendMessage(Json.Stringify(msg));
	}
	
	private void SendMessage(string json)
	{
		if (webSocket != null && webSocket.GetReadyState() == WebSocketPeer.State.Open)
		{
			webSocket.SendText(json);
		}
		else
		{
			GD.PrintErr("❌ WebSocket no está conectado");
		}
	}
	
	private void SendJoin()
	{
		var msg = new Godot.Collections.Dictionary
		{
			{ "type", "join" },
			{ "playerId", PlayerId },
			{ "name", PlayerName }
		};
		GD.Print($"📤 Enviando Join: playerId={PlayerId}, name={PlayerName}");
		SendMessage(Json.Stringify(msg));
	}
	
	// Getters
	public GameState GetGameState() => gameState;
	public bool IsConnected() => webSocket != null && webSocket.GetReadyState() == WebSocketPeer.State.Open;
	public bool IsReady() => isReady;
	
	public override void _ExitTree()
	{
		if (webSocket != null)
		{
			webSocket.Close();
		}
	}
}
