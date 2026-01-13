using Godot;

/// <summary>
/// Manager principal del juego. Orquesta NetworkManager, GameState y renderizado.
/// </summary>
public partial class GameManager : Node
{
	private NetworkManager networkManager;
	private GameState gameState;
	private GameRenderer gameRenderer;
	
	public override void _Ready()
	{
		GD.Print("🎮 GameManager._Ready() iniciado");
		
		// Crear NetworkManager automáticamente
		networkManager = new NetworkManager();
		networkManager.Name = "NetworkManager";
		AddChild(networkManager);
		
		GD.Print("🎮 NetworkManager creado");
		
		// Obtener gameState desde networkManager
		gameState = networkManager.GetGameState();
		
		// Crear renderer
		gameRenderer = new GameRenderer();
		gameRenderer.Name = "GameRenderer";
		AddChild(gameRenderer);
		
		GD.Print("🎮 GameRenderer creado");
		
		// Conectar señales
		networkManager.OnConnected += HandleConnected;
		networkManager.OnDisconnected += HandleDisconnected;
		networkManager.OnSnapshotReceived += HandleSnapshotReceived;
		networkManager.OnErrorReceived += HandleErrorReceived;
		
		GD.Print("🎮 Señales conectadas");
	}
	
	private void HandleConnected()
	{
		GD.Print("🎮 Conectado al servidor. ¡Iniciando juego!");
	}
	
	private void HandleDisconnected()
	{
		GD.PrintErr("🎮 Desconectado del servidor.");
		// Mostrar mensaje visual de reconexión
		var popup = new AcceptDialog();
		popup.DialogText = "Conexión perdida. Intentando reconectar... Si el problema persiste, reinicia el juego.";
		AddChild(popup);
		popup.PopupCentered();
	}
	
	private void HandleSnapshotReceived()
	{
		GD.Print($"🎮 Snapshot recibido - Aplicando a renderer");
		gameRenderer.UpdateFromSnapshot(gameState);
	}
	
	private void HandleErrorReceived(string error)
	{
		GD.PrintErr($"🎮 Error: {error}");
	}
	
	public NetworkManager GetNetworkManager() => networkManager;
	public GameState GetGameState() => gameState;
	public GameRenderer GetGameRenderer() => gameRenderer;
}
