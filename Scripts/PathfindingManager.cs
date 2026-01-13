using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Maneja pathfinding usando AStar2D para movimiento de unidades.
/// El mapa es 256x256 tiles.
/// </summary>
public partial class PathfindingManager : Node
{
	private AStar2D astar;
	private int gridWidth = 256;
	private int gridHeight = 256;
	private HashSet<Vector2I> occupiedTiles = new();

	public override void _Ready()
	{
		InitializeGrid();
	}

	/// <summary>
	/// Inicializa el grid AStar para pathfinding.
	/// </summary>
	private void InitializeGrid()
	{
		astar = new AStar2D();

		// Crear todos los puntos del grid
		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				long id = GetPointId(x, y);
				astar.AddPoint(id, new Vector2(x, y));
			}
		}

		// Conectar puntos vecinos (8 direcciones para movimiento diagonal)
		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				long id = GetPointId(x, y);

				// Conectar con los 8 vecinos
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue;

						int nx = x + dx;
						int ny = y + dy;

						if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
						{
							long neighborId = GetPointId(nx, ny);
							if (!astar.ArePointsConnected(id, neighborId))
								astar.ConnectPoints(id, neighborId);
						}
					}
				}
			}
		}

		GD.Print("✅ PathfindingManager initialized: 256x256 grid");
	}

	/// <summary>
	/// Actualiza los tiles ocupados por edificios según el snapshot.
	/// </summary>
	public void UpdateOccupiedTiles(GameState gameState)
	{
		// Limpiar tiles ocupados anteriores
		foreach (var tile in occupiedTiles)
		{
			long id = GetPointId(tile.X, tile.Y);
			astar.SetPointDisabled(id, false);
		}
		occupiedTiles.Clear();

		// Marcar nuevos tiles como ocupados (con un margen alrededor de edificios)
		foreach (var building in gameState.GetVisibleBuildings())
		{
			int x = (int)building.X;
			int y = (int)building.Y;

			// El edificio ocupa un tile y su perímetro
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					int nx = x + dx;
					int ny = y + dy;

					if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
					{
						var tile = new Vector2I(nx, ny);
						if (!occupiedTiles.Contains(tile))
						{
							occupiedTiles.Add(tile);
							long id = GetPointId(nx, ny);
							astar.SetPointDisabled(id, true);
						}
					}
				}
			}
		}

		// También marcar edificios enemigos
		foreach (var building in gameState.GetVisibleEnemyBuildings())
		{
			int x = (int)building.X;
			int y = (int)building.Y;

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					int nx = x + dx;
					int ny = y + dy;

					if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
					{
						var tile = new Vector2I(nx, ny);
						if (!occupiedTiles.Contains(tile))
						{
							occupiedTiles.Add(tile);
							long id = GetPointId(nx, ny);
							astar.SetPointDisabled(id, true);
						}
					}
				}
			}
		}

		GD.Print($"🗺️ Occupied tiles updated: {occupiedTiles.Count} tiles blocked");
	}

	/// <summary>
	/// Encuentra una ruta de 'from' a 'to'.
	/// Retorna array de Vector2 con la ruta, o empty si no hay ruta.
	/// </summary>
	public Vector2[] FindPath(Vector2 from, Vector2 to)
	{
		try
		{
			long fromId = GetPointId((int)from.X, (int)from.Y);
			long toId = GetPointId((int)to.X, (int)to.Y);

			// Verificar que los puntos existan y no estén deshabilitados
			if (!astar.HasPoint(fromId) || !astar.HasPoint(toId))
			{
				GD.PrintErr($"❌ Invalid path points: from={from}, to={to}");
				return new Vector2[0];
			}

			var path = astar.GetPointPath(fromId, toId);

			if (path == null || path.Length == 0)
			{
				GD.Print($"⚠️ No path found from ({from.X:F0}, {from.Y:F0}) to ({to.X:F0}, {to.Y:F0})");
				return new Vector2[0];
			}

			return path;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"❌ Pathfinding error: {ex.Message}");
			return new Vector2[0];
		}
	}

	/// <summary>
	/// Obtiene el siguiente punto en la ruta hacia un destino.
	/// </summary>
	public Vector2? GetNextWaypoint(Vector2 current, Vector2 target)
	{
		var path = FindPath(current, target);

		if (path == null || path.Length == 0)
			return null;

		// Retornar el segundo punto (el primero es la posición actual)
		if (path.Length > 1)
			return path[1];

		return path[0];
	}

	/// <summary>
	/// Convierte coordenadas (x, y) a ID único para AStar.
	/// </summary>
	private long GetPointId(int x, int y)
	{
		return y * gridWidth + x;
	}
}
