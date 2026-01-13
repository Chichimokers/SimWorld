using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Mantiene el estado local del juego sincronizado con snapshots del servidor.
/// Source of truth en cliente: mapa de entidades por ID.
/// </summary>
public class GameState
{
	public int CurrentTick { get; set; } = 0;
	public int PlayerId { get; set; } = -1;
	public string PlayerName { get; set; } = "";
	public int MapWidth { get; set; } = 256;
	public int MapHeight { get; set; } = 256;
	
	public PlayerResources PlayerResources { get; set; } = new PlayerResources();
	
	// Colecciones de entidades por ID
	private Dictionary<int, Unit> units = new Dictionary<int, Unit>();
	private Dictionary<int, Unit> enemyUnits = new Dictionary<int, Unit>();
	private Dictionary<int, Building> buildings = new Dictionary<int, Building>();
	private Dictionary<int, Building> enemyBuildings = new Dictionary<int, Building>();
	private Dictionary<int, Resource> resources = new Dictionary<int, Resource>();
	
	// Tiles descubiertos (FoW)
	private HashSet<(int, int)> visibleTiles = new HashSet<(int, int)>();
	private HashSet<(int, int)> seenTiles = new HashSet<(int, int)>();
	
	// Colas de entrenamiento
	private Dictionary<int, TrainingQueue> trainingQueues = new Dictionary<int, TrainingQueue>();
	
	// Entidad seleccionada
	public int? SelectedEntityId { get; set; } = null;
	
	// Selección múltiple
	public List<int> SelectedUnitIds { get; set; } = new List<int>();
	
	public GameState() { }
	
	/// <summary>
	/// Aplica un snapshot completo desde el servidor.
	/// Actualiza o crea entidades, elimina las que no están en el snapshot.
	/// </summary>
	public void ApplySnapshot(Snapshot snapshot)
	{
		CurrentTick = snapshot.Tick;
		MapWidth = snapshot.MapWidth;
		MapHeight = snapshot.MapHeight;
		PlayerResources = snapshot.PlayerResources;
		
		// Actualizar unidades propias
		var serverUnitIds = new HashSet<int>();
		foreach (var unit in snapshot.Units)
		{
			serverUnitIds.Add(unit.Id);
			units[unit.Id] = unit;
		}
		
		// Eliminar unidades propias que no están en el snapshot
		var toRemoveUnits = units.Keys.Where(id => !serverUnitIds.Contains(id)).ToList();
		foreach (var id in toRemoveUnits)
			units.Remove(id);
		
		// Actualizar unidades enemigas en vista
		var serverEnemyUnitIds = new HashSet<int>();
		foreach (var unit in snapshot.EnemyUnitsInView)
		{
			serverEnemyUnitIds.Add(unit.Id);
			enemyUnits[unit.Id] = unit;
		}
		
		var toRemoveEnemyUnits = enemyUnits.Keys.Where(id => !serverEnemyUnitIds.Contains(id)).ToList();
		foreach (var id in toRemoveEnemyUnits)
			enemyUnits.Remove(id);
		
		// Actualizar edificios propios
		var serverBuildingIds = new HashSet<int>();
		foreach (var building in snapshot.Buildings)
		{
			serverBuildingIds.Add(building.Id);
			buildings[building.Id] = building;
		}
		
		var toRemoveBuildings = buildings.Keys.Where(id => !serverBuildingIds.Contains(id)).ToList();
		foreach (var id in toRemoveBuildings)
			buildings.Remove(id);
		
		// Actualizar edificios enemigos en vista
		var serverEnemyBuildingIds = new HashSet<int>();
		foreach (var building in snapshot.EnemyBuildingsInView)
		{
			serverEnemyBuildingIds.Add(building.Id);
			enemyBuildings[building.Id] = building;
		}
		
		var toRemoveEnemyBuildings = enemyBuildings.Keys.Where(id => !serverEnemyBuildingIds.Contains(id)).ToList();
		foreach (var id in toRemoveEnemyBuildings)
			enemyBuildings.Remove(id);
		
		// Actualizar recursos
		var serverResourceIds = new HashSet<int>();
		foreach (var resource in snapshot.Resources)
		{
			serverResourceIds.Add(resource.Id);
			resources[resource.Id] = resource;
		}
		
		var toRemoveResources = resources.Keys.Where(id => !serverResourceIds.Contains(id)).ToList();
		foreach (var id in toRemoveResources)
			resources.Remove(id);
		
		// Actualizar visibilidad
		visibleTiles.Clear();
		foreach (var tile in snapshot.VisibleTiles)
		{
			visibleTiles.Add((tile.X, tile.Y));
		}
		
		foreach (var tile in snapshot.SeenTiles)
		{
			seenTiles.Add((tile.X, tile.Y));
		}
		
		// Actualizar colas de entrenamiento
		trainingQueues.Clear();
		foreach (var queue in snapshot.TrainingQueues)
		{
			trainingQueues[queue.BuildingId] = queue;
		}
		
		// Procesar eventos
		ProcessEvents(snapshot.Events);
	}
	
	/// <summary>
	/// Procesa eventos de cambios de entidades.
	/// </summary>
	private void ProcessEvents(List<GameEvent> events)
	{
		foreach (var evt in events)
		{
			switch (evt.Type)
			{
				case 0: // ENTITY_CREATE
				case 1: // ENTITY_UPDATE
					// Ya manejado en ApplySnapshot
					break;
					
				case 2: // ENTITY_DESTROY
					units.Remove(evt.EntityId);
					enemyUnits.Remove(evt.EntityId);
					buildings.Remove(evt.EntityId);
					enemyBuildings.Remove(evt.EntityId);
					resources.Remove(evt.EntityId);
					break;
					
				case 3: // ENTITY_HIDDEN
					if (units.ContainsKey(evt.EntityId))
						units[evt.EntityId].Visible = false;
					if (enemyUnits.ContainsKey(evt.EntityId))
						enemyUnits[evt.EntityId].Visible = false;
					if (buildings.ContainsKey(evt.EntityId))
						buildings[evt.EntityId].Visible = false;
					if (enemyBuildings.ContainsKey(evt.EntityId))
						enemyBuildings[evt.EntityId].Visible = false;
					if (resources.ContainsKey(evt.EntityId))
						resources[evt.EntityId].Visible = false;
					break;
					
				case 4: // FOG_UPDATE
					// Handled in ApplySnapshot
					break;
					
				case 5: // QUEUE_PROGRESS
					// Handled in ApplySnapshot
					break;
					
				case 6: // RESOURCE_UPDATE
					if (evt.Data.ContainsKey("amount") && resources.ContainsKey(evt.EntityId))
					{
						resources[evt.EntityId].Amount = (int)evt.Data["amount"];
					}
					break;
			}
		}
	}
	
	// Getters
	public Unit GetUnit(int id) => units.ContainsKey(id) ? units[id] : null;
	public Building GetBuilding(int id) => buildings.ContainsKey(id) ? buildings[id] : null;
	public Resource GetResource(int id) => resources.ContainsKey(id) ? resources[id] : null;
	
	public IEnumerable<Unit> GetUnits() => units.Values;
	public IEnumerable<Unit> GetEnemyUnits() => enemyUnits.Values;
	public IEnumerable<Building> GetBuildings() => buildings.Values;
	public IEnumerable<Building> GetEnemyBuildings() => enemyBuildings.Values;
	public IEnumerable<Resource> GetResources() => resources.Values;
	
	public IEnumerable<Unit> GetVisibleUnits() => units.Values.Where(u => u.Visible);
	public IEnumerable<Unit> GetVisibleEnemyUnits() => enemyUnits.Values.Where(u => u.Visible);
	public IEnumerable<Building> GetVisibleBuildings() => buildings.Values.Where(b => b.Visible);
	public IEnumerable<Building> GetVisibleEnemyBuildings() => enemyBuildings.Values.Where(b => b.Visible);
	public IEnumerable<Resource> GetVisibleResources() => resources.Values.Where(r => r.Visible);
	
	public bool IsVisibleTile(int x, int y) => visibleTiles.Contains((x, y));
	public bool IsSeenTile(int x, int y) => seenTiles.Contains((x, y));
	
	public Unit GetSelectedUnit() => SelectedEntityId.HasValue ? GetUnit(SelectedEntityId.Value) : null;
	public Building GetSelectedBuilding() => SelectedEntityId.HasValue ? GetBuilding(SelectedEntityId.Value) : null;
}
