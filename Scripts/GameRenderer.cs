using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Renderiza el mapa y entidades usando Canvas2D.
/// Muestra FoW (visibleTiles en verde, seenTiles en gris, unexplored en negro).
/// </summary>
public partial class GameRenderer : Node2D
{
	private GameState gameState;
	private const int TileSize = 16; // 16px por tile
	
	private Dictionary<int, Vector2> unitPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> enemyUnitPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> buildingPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> enemyBuildingPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> resourcePositions = new Dictionary<int, Vector2>();
	
	// Estado de modo de acción
	private string actionMode = ""; // "", "attack", "gather", "build", "deposit", "hunt", "train"
	private int buildingTypeToConstruct = 0; // Para modo construcción
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			Vector2 mousePos = GetGlobalMousePosition();
			
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				// Click izquierdo: Seleccionar unidad O ejecutar acción
				bool targetFound = false;
				
				// Si estamos en modo acción, buscar objetivo
				if (!string.IsNullOrEmpty(actionMode))
				{
					var networkManager = GetParent<GameManager>()?.GetNetworkManager();
					if (networkManager != null && gameState.SelectedEntityId.HasValue)
					{
						switch (actionMode)
						{
							case "attack":
								// Buscar unidad enemiga
								foreach (var kvp in enemyUnitPositions)
								{
									if (mousePos.DistanceTo(kvp.Value) < 16)
									{
										var selectedUnit = gameState.GetSelectedUnit();
										if (selectedUnit != null)
										{
											GD.Print($"🗡️ Unidad {selectedUnit.Id} atacando a {kvp.Key}");
											networkManager.SendAttack(selectedUnit.Id, kvp.Key);
											actionMode = "";
											targetFound = true;
										}
										break;
									}
								}
								break;
								
							case "gather":
								// Buscar recurso
								foreach (var kvp in resourcePositions)
								{
									if (mousePos.DistanceTo(kvp.Value) < 16)
									{
										var gatherUnit = gameState.GetSelectedUnit();
										if (gatherUnit != null)
										{
											GD.Print($"🪨 Unidad {gatherUnit.Id} recolectando recurso {kvp.Key}");
											networkManager.SendGather(gatherUnit.Id, kvp.Key);
											actionMode = "";
											targetFound = true;
										}
										break;
									}
								}
								break;
								
							case "hunt":
								// Buscar animal
								foreach (var kvp in unitPositions)
								{
									if (mousePos.DistanceTo(kvp.Value) < 16)
									{
										var target = gameState.GetUnit(kvp.Key);
										if (target != null && target.Type == 2) // Animal
										{
											var huntUnit = gameState.GetSelectedUnit();
											if (huntUnit != null)
											{
												GD.Print($"🦌 Unidad {huntUnit.Id} cazando animal {kvp.Key}");
												networkManager.SendHunt(huntUnit.Id, kvp.Key);
												actionMode = "";
												targetFound = true;
											}
											break;
										}
									}
								}
								break;
								
							case "deposit":
								// Buscar edificio (TownCenter) - Puede ser propio o enemigo
								foreach (var kvp in buildingPositions)
								{
									if (mousePos.DistanceTo(kvp.Value) < 20)
									{
										var building = gameState.GetBuilding(kvp.Key);
										if (building != null && building.Type == 0) // TownCenter
										{
											var depositUnit = gameState.GetSelectedUnit();
											if (depositUnit != null)
											{
												GD.Print($"💰 Unidad {depositUnit.Id} depositando en {kvp.Key}");
												networkManager.SendDeposit(depositUnit.Id, kvp.Key);
												actionMode = "";
												targetFound = true;
											}
											break;
										}
									}
								}
								// También revisar edificios enemigos
								if (!targetFound)
								{
									foreach (var kvp in enemyBuildingPositions)
									{
										if (mousePos.DistanceTo(kvp.Value) < 20)
										{
											var building = gameState.GetBuilding(kvp.Key);
											if (building != null && building.Type == 0) // TownCenter
											{
												var depositUnit = gameState.GetSelectedUnit();
												if (depositUnit != null)
												{
													GD.Print($"💰 Unidad {depositUnit.Id} depositando en {kvp.Key}");
													networkManager.SendDeposit(depositUnit.Id, kvp.Key);
													actionMode = "";
													targetFound = true;
												}
												break;
											}
										}
									}
								}
								break;
								
							case "build":
								// Colocar edificio en posición
								float worldX = mousePos.X / TileSize;
								float worldY = mousePos.Y / TileSize;
								var buildUnit = gameState.GetSelectedUnit();
								if (buildUnit != null)
								{
									GD.Print($"🏗️ Unidad {buildUnit.Id} construyendo {buildingTypeToConstruct} en ({worldX:F1}, {worldY:F1})");
									networkManager.SendBuild(buildUnit.Id, buildingTypeToConstruct, worldX, worldY);
									actionMode = "";
									targetFound = true;
								}
								break;
								
							case "train":
								// Seleccionar edificio para entrenar - Buscar en propios
								foreach (var kvp in buildingPositions)
								{
									if (mousePos.DistanceTo(kvp.Value) < 20)
									{
										var building = gameState.GetBuilding(kvp.Key);
										if (building != null && building.Type == 1) // Barracks
										{
											GD.Print($"🪖 Entrenando Villager en {kvp.Key}");
											networkManager.SendTrain(kvp.Key, 0); // 0 = Villager
											actionMode = "";
											targetFound = true;
										}
										break;
									}
								}
								break;
						}
					}
					
					if (!targetFound && !string.IsNullOrEmpty(actionMode))
					{
						GD.Print($"⚠️ No se encontró objetivo para {actionMode}");
					}
				}
				else
				{
					// Modo normal: seleccionar unidad
					bool unitFound = false;
					foreach (var kvp in unitPositions)
					{
						if (mousePos.DistanceTo(kvp.Value) < 16) // 16px radius
						{
							gameState.SelectedEntityId = kvp.Key;
							GD.Print($"✅ Unidad {kvp.Key} seleccionada");
							QueueRedraw();
							unitFound = true;
							break;
						}
					}
					
					// Si no hay unidad, deseleccionar
					if (!unitFound)
					{
						gameState.SelectedEntityId = null;
						QueueRedraw();
					}
				}
			}
			else if (mouseEvent.ButtonIndex == MouseButton.Right)
			{
				// Click derecho: Mover unidad seleccionada
				if (gameState.SelectedEntityId.HasValue && string.IsNullOrEmpty(actionMode))
				{
					var unit = gameState.GetSelectedUnit();
					if (unit != null)
					{
						// Convertir coordenadas de pantalla a coordenadas del mundo
						float worldX = mousePos.X / TileSize;
						float worldY = mousePos.Y / TileSize;
						
						GD.Print($"🎯 Moviendo unidad {unit.Id} a ({worldX:F1}, {worldY:F1})");
						
						// Enviar comando de movimiento al servidor
						var networkManager = GetParent<GameManager>()?.GetNetworkManager();
						if (networkManager != null)
						{
							networkManager.SendMove(unit.Id, worldX, worldY);
						}
					}
				}
				else
				{
					// Cancelar modo de acción
					actionMode = "";
					QueueRedraw();
					GD.Print("❌ Acción cancelada");
				}
			}
		}
		
		// Controles de teclado
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (!gameState.SelectedEntityId.HasValue)
			{
				// Algunos comandos no necesitan unidad seleccionada
				if (keyEvent.Keycode == Key.Escape)
				{
					actionMode = "";
					QueueRedraw();
					return;
				}
				return;
			}
			
			var unit = gameState.GetSelectedUnit();
			if (unit == null)
				return;
			
			var networkManager = GetParent<GameManager>()?.GetNetworkManager();
			if (networkManager == null)
				return;
			
			switch (keyEvent.Keycode)
			{
				case Key.A:
					// 'A' - Ataque
					actionMode = "attack";
					GD.Print($"🗡️ Modo ATAQUE: Haz clic en una unidad enemiga");
					QueueRedraw();
					break;
					
				case Key.G:
					// 'G' - Recolectar recursos
					actionMode = "gather";
					GD.Print($"🪨 Modo RECOLECTA: Haz clic en un recurso");
					QueueRedraw();
					break;
					
				case Key.H:
					// 'H' - Cazar animales
					actionMode = "hunt";
					GD.Print($"🦌 Modo CAZA: Haz clic en un animal");
					QueueRedraw();
					break;
					
				case Key.B:
					// 'B' - Construir edificio
					actionMode = "build";
					buildingTypeToConstruct = 2; // House por defecto
					GD.Print($"🏗️ Modo CONSTRUCCIÓN (1=House, 2=Barracks): Presiona 1 o 2, luego haz clic en el mapa");
					QueueRedraw();
					break;
					
				case Key.D:
					// 'D' - Depositar recursos
					actionMode = "deposit";
					GD.Print($"💰 Modo DEPÓSITO: Haz clic en un TownCenter");
					QueueRedraw();
					break;
					
				case Key.T:
					// 'T' - Entrenar (necesita estar en Barracks)
					actionMode = "train";
					GD.Print($"🪖 Modo ENTRENAMIENTO: Haz clic en un Barracks para entrenar Villager");
					QueueRedraw();
					break;
					
				case Key.Key1:
					// '1' - Construir House
					if (actionMode == "build")
					{
						buildingTypeToConstruct = 2;
						GD.Print($"🏠 Construyendo House");
					}
					break;
					
				case Key.Key2:
					// '2' - Construir Barracks
					if (actionMode == "build")
					{
						buildingTypeToConstruct = 1;
						GD.Print($"🎖️ Construyendo Barracks");
					}
					break;
					
				case Key.Escape:
					// Escape - Cancelar modo de acción
					actionMode = "";
					QueueRedraw();
					GD.Print($"❌ Acción cancelada");
					break;
			}
		}
	}
	
	public override void _Draw()
	{
		if (gameState == null)
			return;
		
		// Limpiar diccionarios de posiciones
		unitPositions.Clear();
		enemyUnitPositions.Clear();
		buildingPositions.Clear();
		enemyBuildingPositions.Clear();
		resourcePositions.Clear();
	
		// Fondo y FoW
		int mapWidth = gameState.MapWidth;
		int mapHeight = gameState.MapHeight;
		
		for (int y = 0; y < mapHeight; y++)
		{
			for (int x = 0; x < mapWidth; x++)
			{
				Vector2 tilePos = new Vector2(x * TileSize, y * TileSize);
				Color tileColor;
				
				if (gameState.IsVisibleTile(x, y))
				{
					// Visible ahora
					tileColor = Colors.DarkGreen;
				}
				else if (gameState.IsSeenTile(x, y))
				{
					// Explorado pero no visible (FoW)
					tileColor = new Color(0.3f, 0.3f, 0.3f);
				}
				else
				{
					// Sin explorar
					tileColor = Colors.Black;
				}
				
				DrawRect(new Rect2(tilePos, Vector2.One * TileSize), tileColor);
			}
		}
		
		
		// Recursos
		foreach (var resource in gameState.GetVisibleResources())
		{
			Vector2 pos = new Vector2(resource.X * TileSize, resource.Y * TileSize);
			resourcePositions[resource.Id] = pos;
			
			// Círculo y label según tipo
			string resourceLabel = resource.Type switch
			{
				0 => "W", // Wood
				1 => "G", // Gold
				2 => "S", // Stone
				3 => "F", // Food
				_ => "?"
			};
			
			Color resourceColor = resource.Type switch
			{
				0 => Colors.Orange,      // Wood
				1 => Colors.Gold,        // Gold
				2 => Colors.Gray,        // Stone
				3 => Colors.LimeGreen,   // Food
				_ => Colors.White
			};
			
			DrawCircle(pos, 6, resourceColor);
			var font = new SystemFont();
			DrawString(font, pos - Vector2.One * 4, resourceLabel, HorizontalAlignment.Center, -1, 10, Colors.Black);
		}
		
		// Edificios propios
		foreach (var building in gameState.GetVisibleBuildings())
		{
			Vector2 pos = new Vector2(building.X * TileSize, building.Y * TileSize);
			buildingPositions[building.Id] = pos;
			
			// Rectángulo azul (nuestros edificios)
			DrawRect(new Rect2(pos - Vector2.One * 12, Vector2.One * 24), Colors.DodgerBlue);
			
			// Label del tipo de edificio
			string buildingLabel = building.Type switch
			{
				0 => "TC", // Town Center
				1 => "BA", // Barracks
				2 => "HO", // House
				_ => "?"
			};
			var font = new SystemFont();
			DrawString(font, pos - Vector2.One * 4, buildingLabel, HorizontalAlignment.Center, -1, 10, Colors.White);
			
			// Barra de vida
			if (building.MaxHp > 0)
			{
				float healthPercent = (float)building.HP / building.MaxHp;
				DrawRect(new Rect2(pos - Vector2.One * 12, new Vector2(24 * healthPercent, 3)), Colors.LimeGreen);
			}
		}
		
		// Unidades propias
		foreach (var unit in gameState.GetVisibleUnits())
		{
			Vector2 pos = new Vector2(unit.X * TileSize, unit.Y * TileSize);
			unitPositions[unit.Id] = pos;
			
			// Círculo azul
			DrawCircle(pos, 8, Colors.DodgerBlue);
			
			// Label del tipo de unidad
			string unitLabel = unit.Type switch
			{
				0 => "V",  // Villager
				1 => "M",  // Militia
				2 => "A",  // Animal
				_ => "?"
			};
			var font = new SystemFont();
			DrawString(font, pos - Vector2.One * 3, unitLabel, HorizontalAlignment.Center, -1, 10, Colors.White);
			
			// Barra de vida pequeña
			if (unit.HP > 0)
			{
				DrawRect(new Rect2(pos - Vector2.Right * 8, new Vector2(16, 2)), Colors.LimeGreen);
			}
		}
		
		// Unidades enemigas en vista
		foreach (var unit in gameState.GetVisibleEnemyUnits())
		{
			Vector2 pos = new Vector2(unit.X * TileSize, unit.Y * TileSize);
			enemyUnitPositions[unit.Id] = pos;
			
			// Círculo rojo para enemigos
			DrawCircle(pos, 8, Colors.Red);
			
			// Label del tipo de unidad
			string unitLabel = unit.Type switch
			{
				0 => "V",  // Villager
				1 => "M",  // Militia
				2 => "A",  // Animal
				_ => "?"
			};
			var font2 = new SystemFont();
			DrawString(font2, pos - Vector2.One * 3, unitLabel, HorizontalAlignment.Center, -1, 10, Colors.White);
			
			if (unit.HP > 0)
			{
				DrawRect(new Rect2(pos - Vector2.Right * 8, new Vector2(16, 2)), Colors.LimeGreen);
			}
		}
		
		// Edificios enemigos en vista
		foreach (var building in gameState.GetVisibleEnemyBuildings())
		{
			Vector2 pos = new Vector2(building.X * TileSize, building.Y * TileSize);
			enemyBuildingPositions[building.Id] = pos;
			
			// Rectángulo rojo
			DrawRect(new Rect2(pos - Vector2.One * 12, Vector2.One * 24), Colors.Red);
			
			// Label del tipo de edificio
			string buildingLabel = building.Type switch
			{
				0 => "TC", // Town Center
				1 => "BA", // Barracks
				2 => "HO", // House
				_ => "?"
			};
			var font3 = new SystemFont();
			DrawString(font3, pos - Vector2.One * 4, buildingLabel, HorizontalAlignment.Center, -1, 10, Colors.White);
			
			if (building.MaxHp > 0)
			{
				float healthPercent = (float)building.HP / building.MaxHp;
				DrawRect(new Rect2(pos - Vector2.One * 12, new Vector2(24 * healthPercent, 3)), Colors.LimeGreen);
			}
		}
		
		// Resaltado de selección
		if (gameState.SelectedEntityId.HasValue)
		{
			if (unitPositions.ContainsKey(gameState.SelectedEntityId.Value))
			{
				Vector2 selectedPos = unitPositions[gameState.SelectedEntityId.Value];
				DrawCircle(selectedPos, 12, Colors.Yellow);
				
				// Información de la unidad seleccionada
				var unit = gameState.GetSelectedUnit();
				if (unit != null)
				{
					string unitType = unit.Type switch
					{
						0 => "Villager",
						1 => "Militia",
						2 => "Animal",
						_ => "Unknown"
					};
					
					var font = new SystemFont();
					Vector2 infoPos = selectedPos + Vector2.Down * 20;
					DrawString(font, infoPos, $"ID:{unit.Id} {unitType} HP:{unit.HP}", HorizontalAlignment.Left, -1, 8, Colors.Yellow);
				}
			}
		}
		
		// HUD - Instrucciones de control
		var hudFont = new SystemFont();
		Vector2 hudPos = Vector2.One * 10;
		DrawString(hudFont, hudPos, "🎮 RTS CONTROLS", HorizontalAlignment.Left, -1, 10, Colors.White);
		DrawString(hudFont, hudPos + Vector2.Down * 20, "Click IZQ: Seleccionar | Click DER: Mover | ESC: Cancelar", HorizontalAlignment.Left, -1, 8, Colors.LightGray);
		
		// Mostrar acciones disponibles
		Vector2 actionsPos = hudPos + Vector2.Down * 35;
		DrawString(hudFont, actionsPos, "ACCIONES - [A] Atacar  [G] Recolectar  [H] Cazar  [B] Construir  [D] Depositar  [T] Entrenar", 
				   HorizontalAlignment.Left, -1, 7, Colors.LightYellow);
		
		// Mostrar modo de acción actual
		if (!string.IsNullOrEmpty(actionMode))
		{
			string modeText = actionMode switch
			{
				"attack" => "🗡️ ATACAR - Haz clic en enemigo",
				"gather" => "🪨 RECOLECTAR - Haz clic en recurso",
				"hunt" => "🦌 CAZAR - Haz clic en animal",
				"build" => $"🏗️ CONSTRUIR - [1] House  [2] Barracks - Haz clic en el mapa",
				"deposit" => "💰 DEPOSITAR - Haz clic en TownCenter",
				"train" => "🪖 ENTRENAR - Haz clic en Barracks",
				_ => ""
			};
			
			if (!string.IsNullOrEmpty(modeText))
			{
				DrawString(hudFont, actionsPos + Vector2.Down * 15, modeText, 
						   HorizontalAlignment.Left, -1, 8, Colors.Orange);
			}
		}
		
		// Mostrar recursos
		Vector2 resPos = new Vector2(GetViewportRect().Size.X - 250, 10);
		DrawString(hudFont, resPos, $"🍖 Food: {gameState.PlayerResources.Food}", HorizontalAlignment.Left, -1, 9, Colors.LimeGreen);
		DrawString(hudFont, resPos + Vector2.Down * 15, $"🪵 Wood: {gameState.PlayerResources.Wood}", HorizontalAlignment.Left, -1, 9, Colors.Orange);
		DrawString(hudFont, resPos + Vector2.Down * 30, $"⭐ Gold: {gameState.PlayerResources.Gold}", HorizontalAlignment.Left, -1, 9, Colors.Gold);
		DrawString(hudFont, resPos + Vector2.Down * 45, $"🪨 Stone: {gameState.PlayerResources.Stone}", HorizontalAlignment.Left, -1, 9, Colors.Gray);
		DrawString(hudFont, resPos + Vector2.Down * 60, $"👥 Pop: {gameState.PlayerResources.Pop}/{gameState.PlayerResources.PopCap}", HorizontalAlignment.Left, -1, 9, Colors.Cyan);
	}
	
	public void UpdateFromSnapshot(GameState newGameState)
	{
		gameState = newGameState;
		int unitCount = gameState.GetVisibleUnits().ToList().Count;
		int buildingCount = gameState.GetVisibleBuildings().ToList().Count;
		int resourceCount = gameState.GetVisibleResources().ToList().Count;
		GD.Print($"🎨 Renderer actualizado - Dibujando {unitCount} unidades, {buildingCount} edificios, {resourceCount} recursos");
		QueueRedraw();
	}
}
