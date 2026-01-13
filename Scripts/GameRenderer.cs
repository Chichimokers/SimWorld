using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Renderiza el mapa y entidades usando Canvas2D.
/// Muestra FoW (visibleTiles en verde, seenTiles en gris, unexplored en negro).
/// </summary>
public partial class GameRenderer : Node2D
{
	// --- Caché de entidades para renderizado en niebla ---
	private Dictionary<int, Unit> cachedUnits = new();
	private Dictionary<int, Building> cachedBuildings = new();

	// --- Pathfinding y movimiento ---
	private PathfindingManager pathfindingManager;
	private Dictionary<int, UnitMovement> activeMovements = new(); // unitId -> UnitMovement
	private Dictionary<int, GatherTask> gatherTasks = new(); // unitId -> tarea de recolección activa
	private const float GatherRange = 1.5f;
	private const float GatherCommandInterval = 0.6f; // Segundos entre comandos gather para no saturar al servidor

	// --- Animaciones de eventos ---
	private List<(Vector2 pos, float timer, string type)> eventAnimations = new();

	public void PlayEventAnimation(string type, float x, float y)
	{
		eventAnimations.Add((new Vector2(x * TileSize, y * TileSize) - cameraOffset, 0.7f, type));
		QueueRedraw();
	}

	private GameState gameState;
	private const int TileSize = 16; // 16px por tile

	private Dictionary<int, Vector2> unitPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> enemyUnitPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> buildingPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> enemyBuildingPositions = new Dictionary<int, Vector2>();
	private Dictionary<int, Vector2> resourcePositions = new Dictionary<int, Vector2>();

	// --- Cámara ---
	private Vector2 cameraOffset = Vector2.Zero;
	private Vector2 dragStart = Vector2.Zero;
	private bool dragging = false;
	private float cameraSpeed = 400f;

	// --- Selección múltiple ---
	private bool isBoxSelecting = false;
	private Vector2 boxSelectStart = Vector2.Zero;
	private Vector2 boxSelectEnd = Vector2.Zero;
	private List<int> selectedUnits = new List<int>();

	// --- Menú contextual ---
	private bool showContextMenu = false;
	private Vector2 contextMenuPos = Vector2.Zero;
	private int contextMenuBuildingId = -1;

	public override void _Ready()
	{
		// Crear PathfindingManager
		pathfindingManager = new PathfindingManager();
		AddChild(pathfindingManager);
		GD.Print("✅ GameRenderer ready - PathfindingManager initialized");
	}

	public override void _Process(double delta)
	{
		// Movimiento de cámara con WASD
		Vector2 move = Vector2.Zero;
		if (Input.IsActionPressed("ui_left")) move.X -= 1;
		if (Input.IsActionPressed("ui_right")) move.X += 1;
		if (Input.IsActionPressed("ui_up")) move.Y -= 1;
		if (Input.IsActionPressed("ui_down")) move.Y += 1;
		if (move != Vector2.Zero)
		{
			cameraOffset += move.Normalized() * cameraSpeed * (float)delta;
			ClampCamera();
			QueueRedraw();
		}

		// Actualizar movimientos de unidades
		foreach (var kvp in activeMovements.ToList())
		{
			int unitId = kvp.Key;
			var movement = kvp.Value;

			if (movement.Update(delta, pathfindingManager))
			{
				// Llegó al destino
				activeMovements.Remove(unitId);
				QueueRedraw();
			}
			else
			{
				QueueRedraw();
			}
		}

		UpdateGatherTasks(delta);

		// Actualizar timers de animaciones
		for (int i = eventAnimations.Count - 1; i >= 0; i--)
		{
			var anim = eventAnimations[i];
			anim.timer -= (float)delta;
			if (anim.timer <= 0)
				eventAnimations.RemoveAt(i);
			else
				eventAnimations[i] = (anim.pos, anim.timer, anim.type);
		}
	}

	private void ClampCamera()
	{
		// Limitar la cámara a los bordes del mapa
		int mapWidthPx = (gameState?.MapWidth ?? 256) * TileSize;
		int mapHeightPx = (gameState?.MapHeight ?? 256) * TileSize;
		Vector2 viewport = GetViewportRect().Size;
		cameraOffset.X = Mathf.Clamp(cameraOffset.X, 0, Math.Max(0, mapWidthPx - viewport.X));
		cameraOffset.Y = Mathf.Clamp(cameraOffset.Y, 0, Math.Max(0, mapHeightPx - viewport.Y));
	}

	public override void _Input(InputEvent @event)
	{
		if (gameState == null)
		{
			GD.PrintErr("❌ GameState is NULL in _Input!");
			return;
		}
		
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Middle)
			{
				if (mouseEvent.Pressed)
				{
					dragging = true;
					dragStart = mouseEvent.Position + cameraOffset;
				}
				else
				{
					dragging = false;
				}
			}
			else if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				Vector2 mousePos = GetGlobalMousePosition() + cameraOffset;

				if (mouseEvent.Pressed)
				{
					// Verificar si se clickeó en el menú contextual
					if (showContextMenu)
					{
						HandleContextMenuClick(mouseEvent.Position);
						return;
					}

					// Iniciar box selection
					isBoxSelecting = true;
					boxSelectStart = mousePos;
					boxSelectEnd = mousePos;
				}
				else // Released
				{
					if (isBoxSelecting)
					{
						isBoxSelecting = false;
						HandleBoxSelection();
						QueueRedraw();
					}
				}
			}
			else if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
			{
				Vector2 mousePos = GetGlobalMousePosition() + cameraOffset;
				float mouseWorldX = mousePos.X / TileSize;
				float mouseWorldY = mousePos.Y / TileSize;
				Vector2 targetWorldPos = new Vector2(mouseWorldX, mouseWorldY);

				if (selectedUnits.Count == 0 && !gameState.SelectedEntityId.HasValue)
					return;

				var networkManager = GetParent<GameManager>()?.GetNetworkManager();
				if (networkManager == null)
					return;

				// Obtener unidades seleccionadas
				var unitsToCommand = new List<Unit>();
				if (selectedUnits.Count > 0)
				{
					foreach (var unitId in selectedUnits)
					{
						var u = gameState.GetUnit(unitId);
						if (u != null) unitsToCommand.Add(u);
					}
				}
				else if (gameState.SelectedEntityId.HasValue)
				{
					var u = gameState.GetSelectedUnit();
					if (u != null) unitsToCommand.Add(u);
				}

				// Procesar cada unidad
				foreach (var unit in unitsToCommand)
				{
					// 1. ¿Hay un recurso en el click?
					bool foundTarget = false;
					foreach (var resource in gameState.GetVisibleResources())
					{
						if (Mathf.Abs(resource.X - mouseWorldX) < 0.5f && Mathf.Abs(resource.Y - mouseWorldY) < 0.5f)
						{
							// Recurso encontrado
							float distance = new Vector2(unit.X, unit.Y).DistanceTo(new Vector2(resource.X, resource.Y));
							float gatherRange = GatherRange;

							if (distance <= gatherRange)
							{
								// Ya está en rango - registrar tarea y enviar primer comando
								RegisterGatherTask(unit, resource);
								networkManager.SendGather(unit.Id, resource.Id);
								GD.Print($"📦 GATHER (in range): Unit {unit.Id} -> Resource {resource.Id}");
							}
							else
							{
								// Fuera de rango - registrar tarea y mover hacia el recurso
								RegisterGatherTask(unit, resource);
								var targetPos = new Vector2(resource.X, resource.Y);
								InitiateUnitMovement(unit.Id, targetPos, gatherRange, () => {
									var task = gatherTasks.ContainsKey(unit.Id) ? gatherTasks[unit.Id] : null;
									SendGatherCommand(networkManager, unit.Id, resource.Id, task);
									GD.Print($"📦 GATHER (arrived): Unit {unit.Id} -> Resource {resource.Id}");
								});
								GD.Print($"📦 GATHER (moving): Unit {unit.Id} moving to Resource {resource.Id}");
							}
							foundTarget = true;
							break;
						}
					}
					if (foundTarget) continue;

					// 2. ¿Hay un enemigo en el click?
					foreach (var enemy in gameState.GetVisibleEnemyUnits())
					{
						if (Mathf.Abs(enemy.X - mouseWorldX) < 0.5f && Mathf.Abs(enemy.Y - mouseWorldY) < 0.5f)
						{
							CancelAutoGather(unit.Id);
							float distance = new Vector2(unit.X, unit.Y).DistanceTo(new Vector2(enemy.X, enemy.Y));
							float attackRange = 1.5f; // Por defecto

							if (distance <= attackRange)
							{
								networkManager.SendAttack(unit.Id, enemy.Id);
								GD.Print($"⚔️ ATTACK (in range): Unit {unit.Id} -> Enemy {enemy.Id}");
							}
							else
							{
								var targetPos = new Vector2(enemy.X, enemy.Y);
								InitiateUnitMovement(unit.Id, targetPos, attackRange, () => {
									networkManager.SendAttack(unit.Id, enemy.Id);
									GD.Print($"⚔️ ATTACK (arrived): Unit {unit.Id} -> Enemy {enemy.Id}");
								});
								GD.Print($"⚔️ ATTACK (moving): Unit {unit.Id} moving to Enemy {enemy.Id}");
							}
							foundTarget = true;
							break;
						}
					}
					if (foundTarget) continue;

					// 3. ¿Hay un edificio enemigo en el click?
					foreach (var building in gameState.GetVisibleEnemyBuildings())
					{
						if (Mathf.Abs(building.X - mouseWorldX) < 0.5f && Mathf.Abs(building.Y - mouseWorldY) < 0.5f)
						{
							CancelAutoGather(unit.Id);
							float distance = new Vector2(unit.X, unit.Y).DistanceTo(new Vector2(building.X, building.Y));
							float attackRange = 1.5f;

							if (distance <= attackRange)
							{
								networkManager.SendAttack(unit.Id, building.Id);
								GD.Print($"🏰 ATTACK BUILDING (in range): Unit {unit.Id} -> Building {building.Id}");
							}
							else
							{
								var targetPos = new Vector2(building.X, building.Y);
								InitiateUnitMovement(unit.Id, targetPos, attackRange, () => {
									networkManager.SendAttack(unit.Id, building.Id);
									GD.Print($"🏰 ATTACK BUILDING (arrived): Unit {unit.Id} -> Building {building.Id}");
								});
								GD.Print($"🏰 ATTACK BUILDING (moving): Unit {unit.Id} moving to Building {building.Id}");
							}
							foundTarget = true;
							break;
						}
					}
					if (foundTarget) continue;

					// 4. ¿Hay un animal en el click?
					foreach (var animal in gameState.GetVisibleUnits())
					{
						if (animal.Type == 2 && Mathf.Abs(animal.X - mouseWorldX) < 0.5f && Mathf.Abs(animal.Y - mouseWorldY) < 0.5f)
						{
							CancelAutoGather(unit.Id);
							float distance = new Vector2(unit.X, unit.Y).DistanceTo(new Vector2(animal.X, animal.Y));
							float huntRange = 1.5f;

							if (distance <= huntRange)
							{
								networkManager.SendHunt(unit.Id, animal.Id);
								GD.Print($"🦌 HUNT (in range): Unit {unit.Id} -> Animal {animal.Id}");
							}
							else
							{
								var targetPos = new Vector2(animal.X, animal.Y);
								InitiateUnitMovement(unit.Id, targetPos, huntRange, () => {
									networkManager.SendHunt(unit.Id, animal.Id);
									GD.Print($"🦌 HUNT (arrived): Unit {unit.Id} -> Animal {animal.Id}");
								});
								GD.Print($"🦌 HUNT (moving): Unit {unit.Id} moving to Animal {animal.Id}");
							}
							foundTarget = true;
							break;
						}
					}
					if (foundTarget) continue;

					// 5. ¿Hay un TC (edificio propio tipo 0) en el click? -> Depositar
					if (unit.Type == 0 && unit.Carrying > 0)
					{
						foreach (var building in gameState.GetVisibleBuildings())
						{
							if (building.Type == 0 && Mathf.Abs(building.X - mouseWorldX) < 0.5f && Mathf.Abs(building.Y - mouseWorldY) < 0.5f)
							{
								CancelAutoGather(unit.Id);
								float distance = new Vector2(unit.X, unit.Y).DistanceTo(new Vector2(building.X, building.Y));
								float depositRange = 2.0f;

								if (distance <= depositRange)
								{
									networkManager.SendDeposit(unit.Id, building.Id);
									GD.Print($"💰 DEPOSIT (in range): Unit {unit.Id} -> TC {building.Id}");
								}
								else
								{
									var targetPos = new Vector2(building.X, building.Y);
									InitiateUnitMovement(unit.Id, targetPos, depositRange, () => {
										networkManager.SendDeposit(unit.Id, building.Id);
										GD.Print($"💰 DEPOSIT (arrived): Unit {unit.Id} -> TC {building.Id}");
									});
									GD.Print($"💰 DEPOSIT (moving): Unit {unit.Id} moving to TC {building.Id}");
								}
								foundTarget = true;
								break;
							}
						}
					}
					if (foundTarget) continue;

					// 6. Por defecto: MOVE a la posición
					CancelAutoGather(unit.Id);
					InitiateUnitMovement(unit.Id, targetWorldPos, 0.5f, () => {
						// Cuando llega, nada - solo se quedó en la posición
					});
					GD.Print($"👣 MOVE: Unit {unit.Id} moving to ({mouseWorldX:F1}, {mouseWorldY:F1})");
				}
			}
		}

		if (@event is InputEventMouseMotion mouseMotion)
		{
			if (dragging)
			{
				cameraOffset = dragStart - mouseMotion.Position;
				ClampCamera();
				QueueRedraw();
			}
			else if (isBoxSelecting)
			{
				boxSelectEnd = GetGlobalMousePosition() + cameraOffset;
				QueueRedraw();
			}
		}
	}

	private void HandleBoxSelection()
	{
		// Seleccionar todas las unidades propias dentro del rectángulo
		selectedUnits.Clear();
		gameState.SelectedEntityId = null;

		float minX = Mathf.Min(boxSelectStart.X, boxSelectEnd.X);
		float maxX = Mathf.Max(boxSelectStart.X, boxSelectEnd.X);
		float minY = Mathf.Min(boxSelectStart.Y, boxSelectEnd.Y);
		float maxY = Mathf.Max(boxSelectStart.Y, boxSelectEnd.Y);

		// Si el box es muy pequeño, seleccionar unidad en punto
		if ((maxX - minX) < 5 && (maxY - minY) < 5)
		{
			foreach (var kvp in unitPositions)
			{
				if (boxSelectStart.DistanceTo(kvp.Value) < 16)
				{
					gameState.SelectedEntityId = kvp.Key;
					GD.Print($"✅ Unidad {kvp.Key} seleccionada");
					break;
				}
			}
			return;
		}

		// Selección de área (buscar en todas las unidades propias)
		foreach (var unit in gameState.GetVisibleUnits())
		{
			Vector2 unitWorldPos = new Vector2(unit.X * TileSize, unit.Y * TileSize);
			if (unitWorldPos.X >= minX && unitWorldPos.X <= maxX && unitWorldPos.Y >= minY && unitWorldPos.Y <= maxY)
			{
				selectedUnits.Add(unit.Id);
			}
		}

		if (selectedUnits.Count > 0)
		{
			GD.Print($"✅ {selectedUnits.Count} unidades seleccionadas");
			gameState.SelectedUnitIds = selectedUnits;
		}
	}

	/// <summary>
	/// Inicia el movimiento de una unidad hacia un destino con pathfinding.
	/// Cuando llegue a RequiredDistance, ejecuta onArrived.
	/// </summary>
	private void InitiateUnitMovement(int unitId, Vector2 targetPos, float requiredDistance, Action onArrived)
	{
		var unit = gameState.GetUnit(unitId);
		if (unit == null)
		{
			GD.PrintErr($"❌ Unit {unitId} not found");
			return;
		}

		// Cancelar movimiento previo si existe
		if (activeMovements.ContainsKey(unitId))
		{
			activeMovements[unitId].Cancel();
			activeMovements.Remove(unitId);
		}

		// Crear nuevo movimiento
		var movement = new UnitMovement(unitId, new Vector2(unit.X, unit.Y), targetPos, requiredDistance)
		{
			Speed = unit.Speed > 0 ? unit.Speed : 2.0f
		};
		movement.OnReachedTarget = (id) => {
			onArrived?.Invoke();
		};
		activeMovements[unitId] = movement;

		GD.Print($"🚶 Unit {unitId} initiated movement to ({targetPos.X:F1}, {targetPos.Y:F1}), required distance: {requiredDistance:F1}");
	}

	private void HandleContextMenuClick(Vector2 screenPos)
	{
		// Verificar si clickeó alguna opción del menú
		if (contextMenuBuildingId < 0)
		{
			showContextMenu = false;
			return;
		}

		var building = gameState.GetBuilding(contextMenuBuildingId);
		if (building == null)
		{
			showContextMenu = false;
			return;
		}

		var networkManager = GetParent<GameManager>()?.GetNetworkManager();
		if (networkManager == null)
		{
			showContextMenu = false;
			return;
		}

		Rect2 menuRect = new Rect2(contextMenuPos, new Vector2(150, 80));
		GD.Print($"👆 Click en ({screenPos.X}, {screenPos.Y}), menu en ({contextMenuPos.X}, {contextMenuPos.Y})");
		
		if (!menuRect.HasPoint(screenPos))
		{
			GD.Print("❌ Click fuera del menú, cerrando");
			showContextMenu = false;
			return;
		}

		// Detectar qué opción se clickeó (basado en la posición Y)
		float relativeY = screenPos.Y - contextMenuPos.Y;
		GD.Print($"📍 RelativeY: {relativeY}, Building Type: {building.Type}");

		if (building.Type == 0) // Town Center
		{
			if (relativeY >= 30 && relativeY <= 50)
			{
				GD.Print($"🏠 Entrenando Aldeano en TC {building.Id}");
				networkManager.SendTrain(building.Id, 0); // 0 = Villager
				showContextMenu = false;
				return;
			}
		}
		else if (building.Type == 1) // Barracks
		{
			if (relativeY >= 30 && relativeY <= 50)
			{
				GD.Print($"⚔️ Entrenando Milicia en Barracks {building.Id}");
				networkManager.SendTrain(building.Id, 1); // 1 = Militia
				showContextMenu = false;
				return;
			}
		}

		GD.Print("⚠️ Click dentro del menú pero fuera del botón");
		showContextMenu = false;
	}

	public override void _Draw()
	{
		if (gameState == null)
			return;

		unitPositions.Clear();
		enemyUnitPositions.Clear();
		buildingPositions.Clear();
		enemyBuildingPositions.Clear();
		resourcePositions.Clear();

		int mapWidth = gameState.MapWidth;
		int mapHeight = gameState.MapHeight;

		Vector2 viewport = GetViewportRect().Size;
		int minX = Mathf.Max(0, (int)(cameraOffset.X / TileSize));
		int minY = Mathf.Max(0, (int)(cameraOffset.Y / TileSize));
		int maxX = Mathf.Min(mapWidth, (int)((cameraOffset.X + viewport.X) / TileSize) + 2);
		int maxY = Mathf.Min(mapHeight, (int)((cameraOffset.Y + viewport.Y) / TileSize) + 2);

		for (int y = minY; y < maxY; y++)
		{
			for (int x = minX; x < maxX; x++)
			{
				Vector2 tilePos = new Vector2(x * TileSize, y * TileSize) - cameraOffset;
				Color tileColor;
				if (gameState.IsVisibleTile(x, y))
				{
					tileColor = Colors.DarkGreen;
				}
				else if (gameState.IsSeenTile(x, y))
				{
					tileColor = new Color(0.3f, 0.3f, 0.3f);
				}
				else
				{
					tileColor = Colors.Black;
				}
				DrawRect(new Rect2(tilePos, Vector2.One * TileSize), tileColor);
			}
		}

		foreach (var resource in gameState.GetVisibleResources())
		{
			Vector2 pos = new Vector2(resource.X * TileSize, resource.Y * TileSize) - cameraOffset;
			resourcePositions[resource.Id] = pos;
			
			// Color y letra según tipo de recurso
			Color resourceColor = resource.Type switch
			{
				0 => new Color(0.6f, 0.4f, 0.2f), // Wood - marrón
				1 => Colors.Gold,                  // Gold - dorado
				2 => Colors.Gray,                  // Stone - gris
				3 => Colors.Pink,                  // Food - rosado
				_ => Colors.White
			};
			
			string resourceLabel = resource.Type switch
			{
				0 => "W",  // Wood
				1 => "G",  // Gold
				2 => "S",  // Stone
				3 => "F",  // Food
				_ => "?"
			};
			
			DrawCircle(pos, 8, resourceColor);
			DrawString(new SystemFont(), pos + Vector2.Down * 12, resourceLabel, HorizontalAlignment.Center, -1, 8, Colors.White);
		}

		foreach (var building in gameState.GetVisibleBuildings())
		{
			Vector2 pos = new Vector2(building.X * TileSize, building.Y * TileSize) - cameraOffset;
			buildingPositions[building.Id] = pos;
			
			// Renderizar todos los tiles ocupados
			if (building.OccupiedTiles.Count > 0)
			{
				foreach (var tile in building.OccupiedTiles)
				{
					Vector2 tilePos = new Vector2(tile.X * TileSize, tile.Y * TileSize) - cameraOffset;
					DrawRect(new Rect2(tilePos, Vector2.One * TileSize), Colors.DodgerBlue);
				}
			}
			else
			{
				// Fallback si no hay tiles ocupados
				DrawRect(new Rect2(pos - Vector2.One * 12, Vector2.One * 24), Colors.DodgerBlue);
			}
			
			// Letra según tipo de edificio
			string buildingLabel = building.Type switch
			{
				0 => "TC",  // TownCenter
				1 => "B",   // Barracks
				2 => "H",   // House
				_ => "?"
			};
			DrawString(new SystemFont(), pos + Vector2.Down * 16, buildingLabel, HorizontalAlignment.Center, -1, 8, Colors.White);
		}

		foreach (var building in gameState.GetVisibleEnemyBuildings())
		{
			Vector2 pos = new Vector2(building.X * TileSize, building.Y * TileSize) - cameraOffset;
			enemyBuildingPositions[building.Id] = pos;
			
			// Renderizar todos los tiles ocupados
			if (building.OccupiedTiles.Count > 0)
			{
				foreach (var tile in building.OccupiedTiles)
				{
					Vector2 tilePos = new Vector2(tile.X * TileSize, tile.Y * TileSize) - cameraOffset;
					DrawRect(new Rect2(tilePos, Vector2.One * TileSize), Colors.Red);
				}
			}
			else
			{
				// Fallback si no hay tiles ocupados
				DrawRect(new Rect2(pos - Vector2.One * 12, Vector2.One * 24), Colors.Red);
			}
			
			// Letra según tipo de edificio
			string buildingLabel = building.Type switch
			{
				0 => "TC",  // TownCenter
				1 => "B",   // Barracks
				2 => "H",   // House
				_ => "?"
			};
			DrawString(new SystemFont(), pos + Vector2.Down * 16, buildingLabel, HorizontalAlignment.Center, -1, 8, Colors.White);
		}

		foreach (var unit in gameState.GetVisibleUnits())
		{
			Vector2 unitPos = activeMovements.TryGetValue(unit.Id, out var localMovement)
				? localMovement.CurrentPos
				: new Vector2(unit.X, unit.Y);
			Vector2 pos = new Vector2(unitPos.X * TileSize, unitPos.Y * TileSize) - cameraOffset;
			unitPositions[unit.Id] = pos;
			
			// Color según tipo
			Color unitColor = unit.Type switch
			{
				0 => Colors.LightBlue,    // Villager
				1 => Colors.Blue,         // Militia
				2 => Colors.LightGreen,   // Animal
				_ => Colors.White
			};
			
			// Letra según tipo
			string unitLabel = unit.Type switch
			{
				0 => "V",  // Villager
				1 => "M",  // Militia
				2 => "A",  // Animal
				_ => "?"
			};
			
			DrawCircle(pos, 8, unitColor);
			DrawString(new SystemFont(), pos + Vector2.Down * 12, unitLabel, HorizontalAlignment.Center, -1, 8, Colors.White);
		}

		foreach (var unit in gameState.GetVisibleEnemyUnits())
		{
			Vector2 unitPos = activeMovements.TryGetValue(unit.Id, out var localMovement)
				? localMovement.CurrentPos
				: new Vector2(unit.X, unit.Y);
			Vector2 pos = new Vector2(unitPos.X * TileSize, unitPos.Y * TileSize) - cameraOffset;
			enemyUnitPositions[unit.Id] = pos;
			
			// Color enemigo siempre rojo
			Color unitColor = Colors.OrangeRed;
			
			// Letra según tipo
			string unitLabel = unit.Type switch
			{
				0 => "V",  // Villager
				1 => "M",  // Militia
				2 => "A",  // Animal
				_ => "?"
			};
			
			DrawCircle(pos, 8, unitColor);
			DrawString(new SystemFont(), pos + Vector2.Down * 12, unitLabel, HorizontalAlignment.Center, -1, 8, Colors.White);
		}

		if (gameState.SelectedEntityId.HasValue)
		{
			if (unitPositions.ContainsKey(gameState.SelectedEntityId.Value))
			{
				Vector2 selectedPos = unitPositions[gameState.SelectedEntityId.Value];
				DrawCircle(selectedPos, 12, Colors.Yellow);
			}
		}

		// Dibujar selección múltiple
		foreach (var unitId in selectedUnits)
		{
			if (unitPositions.ContainsKey(unitId))
			{
				Vector2 selectedPos = unitPositions[unitId];
				DrawCircle(selectedPos, 12, Colors.Yellow);
			}
		}

		// Dibujar box selection en progreso
		if (isBoxSelecting)
		{
			Vector2 startScreen = boxSelectStart - cameraOffset;
			Vector2 endScreen = boxSelectEnd - cameraOffset;
			Rect2 boxRect = new Rect2(startScreen, endScreen - startScreen);
			DrawRect(boxRect, new Color(0, 1, 0, 0.2f));
			DrawRect(boxRect, Colors.Green, false, 2);
		}

		for (int i = eventAnimations.Count - 1; i >= 0; i--)
		{
			var (pos, timer, type) = eventAnimations[i];
			float alpha = Mathf.Clamp(timer / 0.7f, 0, 1);
			Color c = type switch
			{
				"destroy" => new Color(1, 0, 0, alpha),
				"gather" => new Color(1, 1, 0, alpha),
				"create" => new Color(0, 1, 0, alpha),
				_ => new Color(1, 1, 1, alpha)
			};
			DrawCircle(pos - cameraOffset, 24 * alpha, c);
		}

		foreach (var unit in cachedUnits.Values)
		{
			int tileX = (int)unit.X;
			int tileY = (int)unit.Y;
			if (!gameState.IsVisibleTile(tileX, tileY) && gameState.IsSeenTile(tileX, tileY))
			{
				Vector2 pos = new Vector2(unit.X * TileSize, unit.Y * TileSize) - cameraOffset;
				Color c = unit.Owner == gameState.PlayerId ? new Color(0.2f, 0.2f, 1f, 0.4f) : new Color(1f, 0.2f, 0.2f, 0.4f);
				DrawCircle(pos, 8, c);
			}
		}
		foreach (var building in cachedBuildings.Values)
		{
			// Renderizar silueta usando tiles ocupados si están disponibles
			if (building.OccupiedTiles.Count > 0)
			{
				foreach (var tile in building.OccupiedTiles)
				{
					if (!gameState.IsVisibleTile(tile.X, tile.Y) && gameState.IsSeenTile(tile.X, tile.Y))
					{
						Vector2 tilePos = new Vector2(tile.X * TileSize, tile.Y * TileSize) - cameraOffset;
						Color c = building.Owner == gameState.PlayerId ? new Color(0.2f, 0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f, 0.3f);
						DrawRect(new Rect2(tilePos, Vector2.One * TileSize), c);
					}
				}
			}
			else
			{
				// Fallback para edificios sin tiles ocupados
				int tileX = (int)building.X;
				int tileY = (int)building.Y;
				if (!gameState.IsVisibleTile(tileX, tileY) && gameState.IsSeenTile(tileX, tileY))
				{
					Vector2 pos = new Vector2(building.X * TileSize, building.Y * TileSize) - cameraOffset;
					Color c = building.Owner == gameState.PlayerId ? new Color(0.2f, 0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f, 0.3f);
					DrawRect(new Rect2(pos - Vector2.One * 12, Vector2.One * 24), c);
				}
			}
		}

		int minimapSize = 180;
		int minimapMargin = 16;
		float scaleX = (float)minimapSize / mapWidth;
		float scaleY = (float)minimapSize / mapHeight;
		Vector2 minimapPos = new Vector2(GetViewportRect().Size.X - minimapSize - minimapMargin, GetViewportRect().Size.Y - minimapSize - minimapMargin);

		DrawRect(new Rect2(minimapPos, new Vector2(minimapSize, minimapSize)), new Color(0, 0, 0, 0.7f));

		for (int y = 0; y < mapHeight; y++)
		{
			for (int x = 0; x < mapWidth; x++)
			{
				Color c = Colors.Black;
				if (gameState.IsVisibleTile(x, y)) c = Colors.LimeGreen;
				else if (gameState.IsSeenTile(x, y)) c = new Color(0.3f, 0.3f, 0.3f);
				DrawRect(new Rect2(minimapPos + new Vector2(x * scaleX, y * scaleY), new Vector2(scaleX, scaleY)), c);
			}
		}

		foreach (var unit in gameState.GetVisibleUnits())
		{
			Vector2 p = minimapPos + new Vector2(unit.X * scaleX, unit.Y * scaleY);
			DrawCircle(p, 2, Colors.DodgerBlue);
		}
		foreach (var unit in gameState.GetVisibleEnemyUnits())
		{
			Vector2 p = minimapPos + new Vector2(unit.X * scaleX, unit.Y * scaleY);
			DrawCircle(p, 2, Colors.Red);
		}
		foreach (var building in gameState.GetVisibleBuildings())
		{
			Vector2 p = minimapPos + new Vector2(building.X * scaleX, building.Y * scaleY);
			DrawRect(new Rect2(p - Vector2.One, Vector2.One * 3), Colors.DodgerBlue);
		}
		foreach (var building in gameState.GetVisibleEnemyBuildings())
		{
			Vector2 p = minimapPos + new Vector2(building.X * scaleX, building.Y * scaleY);
			DrawRect(new Rect2(p - Vector2.One, Vector2.One * 3), Colors.Red);
		}

		float camW = viewport.X * scaleX / TileSize;
		float camH = viewport.Y * scaleY / TileSize;
		float camX = cameraOffset.X * scaleX / TileSize;
		float camY = cameraOffset.Y * scaleY / TileSize;
		DrawRect(new Rect2(minimapPos + new Vector2(camX, camY), new Vector2(camW, camH)), Colors.Yellow, false, 2);

		// HUD - Título
		var hudFont = new SystemFont();
		Vector2 hudPos = Vector2.One * 10;
		DrawString(hudFont, hudPos, "🎮 RTS Game", HorizontalAlignment.Left, -1, 10, Colors.White);

		// Mostrar unidades seleccionadas
		if (selectedUnits.Count > 0)
		{
			DrawString(hudFont, hudPos + Vector2.Down * 20, $"Unidades seleccionadas: {selectedUnits.Count}",
					   HorizontalAlignment.Left, -1, 8, Colors.LightYellow);
		}

		Vector2 resPos = new Vector2(GetViewportRect().Size.X - 250, 10);
		DrawString(hudFont, resPos, $"🍖 Food: {gameState.PlayerResources.Food}", HorizontalAlignment.Left, -1, 9, Colors.LimeGreen);
		DrawString(hudFont, resPos + Vector2.Down * 15, $"🪵 Wood: {gameState.PlayerResources.Wood}", HorizontalAlignment.Left, -1, 9, Colors.Orange);
		DrawString(hudFont, resPos + Vector2.Down * 30, $"⭐ Gold: {gameState.PlayerResources.Gold}", HorizontalAlignment.Left, -1, 9, Colors.Gold);
		DrawString(hudFont, resPos + Vector2.Down * 45, $"🪨 Stone: {gameState.PlayerResources.Stone}", HorizontalAlignment.Left, -1, 9, Colors.Gray);
		DrawString(hudFont, resPos + Vector2.Down * 60, $"👥 Pop: {gameState.PlayerResources.Pop}/{gameState.PlayerResources.PopCap}", HorizontalAlignment.Left, -1, 9, Colors.Cyan);

		// Menú contextual para edificios
		if (showContextMenu && contextMenuBuildingId >= 0)
		{
			var building = gameState.GetBuilding(contextMenuBuildingId);
			if (building != null)
			{
				Rect2 menuRect = new Rect2(contextMenuPos, new Vector2(150, 80));
				DrawRect(menuRect, new Color(0.1f, 0.1f, 0.1f, 0.95f));
				DrawRect(menuRect, Colors.White, false, 2);

				DrawString(hudFont, contextMenuPos + Vector2.Down * 15, building.Type == 0 ? "Town Center" : "Barracks",
						   HorizontalAlignment.Left, -1, 9, Colors.Yellow);

				// Botón de entrenamiento
				Rect2 buttonRect = new Rect2(contextMenuPos + Vector2.Down * 30, new Vector2(140, 20));
				DrawRect(buttonRect, Colors.DarkGreen);
				DrawRect(buttonRect, Colors.LightGreen, false, 1);
				
				string buttonText = building.Type == 0 ? "Train Villager" : "Train Militia";
				DrawString(hudFont, contextMenuPos + Vector2.Down * 45, buttonText,
						   HorizontalAlignment.Left, -1, 8, Colors.White);
			}
		}
	}

	public void UpdateFromSnapshot(GameState newGameState)
	{
		// El servidor se encarga de todo (movimiento, gather, hunt, etc)
		// El cliente solo envía comandos y recibe snapshots

		// Actualizar tiles ocupados en pathfinding cuando edificios cambien
		if (pathfindingManager != null)
		{
			pathfindingManager.UpdateOccupiedTiles(newGameState);
		}

		// Sincronizar posiciones de unidades con movimiento cliente-side
		foreach (var kvp in activeMovements.ToList())
		{
			int unitId = kvp.Key;
			var movement = kvp.Value;
			var unit = newGameState.GetUnit(unitId);

			if (unit != null)
			{
				// Actualizar posición actual desde el servidor (puede haber discrepancias)
				movement.CurrentPos = new Vector2(unit.X, unit.Y);
			}
		}

		foreach (var unit in newGameState.GetUnits().Concat(newGameState.GetEnemyUnits()))
		{
			cachedUnits[unit.Id] = unit;
		}
		foreach (var building in newGameState.GetBuildings().Concat(newGameState.GetEnemyBuildings()))
		{
			cachedBuildings[building.Id] = building;
		}

		gameState = newGameState;
		QueueRedraw();
	}

	// --- Auto-gather helpers ---
	private void RegisterGatherTask(Unit unit, Resource resource)
	{
		gatherTasks[unit.Id] = new GatherTask
		{
			ResourceId = resource.Id,
			TargetPos = new Vector2(resource.X, resource.Y),
			Range = GatherRange,
			CommandCooldown = GatherCommandInterval,
			CooldownRemaining = 0f,
			LastKnownAmount = resource.Amount
		};
	}

	private void CancelAutoGather(int unitId)
	{
		if (gatherTasks.ContainsKey(unitId))
			gatherTasks.Remove(unitId);
	}

	private void UpdateGatherTasks(double delta)
	{
		if (gameState == null || gatherTasks.Count == 0)
			return;

		var networkManager = GetParent<GameManager>()?.GetNetworkManager();
		if (networkManager == null)
			return;

		foreach (var kvp in gatherTasks.ToList())
		{
			int unitId = kvp.Key;
			var task = kvp.Value;
			task.CooldownRemaining = Math.Max(0f, task.CooldownRemaining - (float)delta);

			var unit = gameState.GetUnit(unitId);
			var resource = gameState.GetResource(task.ResourceId);

			if (unit == null || resource == null || !resource.Visible || resource.Amount <= 0)
			{
				gatherTasks.Remove(unitId);
				continue;
			}

			// Actualizar última cantidad conocida; si hay cambio, permitimos reenviar de inmediato
			if (resource.Amount != task.LastKnownAmount)
			{
				task.LastKnownAmount = resource.Amount;
				task.CooldownRemaining = 0f;
			}

			Vector2 unitPos = new Vector2(unit.X, unit.Y);
			float distance = unitPos.DistanceTo(task.TargetPos);

			if (distance > task.Range + 0.1f)
			{
				// Reasignar movimiento si se salió de rango
				if (!activeMovements.ContainsKey(unitId))
				{
					InitiateUnitMovement(unitId, task.TargetPos, task.Range, () =>
					{
						SendGatherCommand(networkManager, unitId, resource.Id, task);
					});
				}
				continue;
			}

			// En rango: enviar comando si cooldown terminó
			if (task.CooldownRemaining <= 0f)
			{
				SendGatherCommand(networkManager, unitId, resource.Id, task);
			}
		}
	}

	private void SendGatherCommand(NetworkManager networkManager, int unitId, int resourceId, GatherTask task)
	{
		networkManager.SendGather(unitId, resourceId);
		if (task != null)
			task.CooldownRemaining = GatherCommandInterval;
	}

	private class GatherTask
	{
		public int ResourceId { get; set; }
		public Vector2 TargetPos { get; set; }
		public float Range { get; set; }
		public float CommandCooldown { get; set; }
		public float CooldownRemaining { get; set; }
		public int LastKnownAmount { get; set; }
	}
}
