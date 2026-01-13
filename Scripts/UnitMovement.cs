using Godot;
using System;

/// <summary>
/// Rastrea el movimiento de una unidad hacia un destino.
/// Maneja la velocidad y la parada cuando llega a rango.
/// </summary>
public class UnitMovement
{
	public int UnitId { get; set; }
	public Vector2 CurrentPos { get; set; }
	public Vector2 TargetPos { get; set; }
	public float RequiredDistance { get; set; }
	public float Speed { get; set; } = 2.0f; // Tiles por segundo
	public bool IsActive { get; set; } = false;

	// Callback cuando llega a rango
	public Action<int> OnReachedTarget { get; set; }

	public UnitMovement(int unitId, Vector2 start, Vector2 target, float requiredDist)
	{
		UnitId = unitId;
		CurrentPos = start;
		TargetPos = target;
		RequiredDistance = requiredDist;
		IsActive = true;
	}

	/// <summary>
	/// Actualiza la posición de la unidad en dirección al destino.
	/// Retorna true si llegó al destino (dentro de RequiredDistance).
	/// </summary>
	public bool Update(double delta, PathfindingManager pathfinding)
	{
		if (!IsActive)
			return false;

		float distance = CurrentPos.DistanceTo(TargetPos);

		// Ya llegó a rango
		if (distance <= RequiredDistance)
		{
			IsActive = false;
			OnReachedTarget?.Invoke(UnitId);
			return true;
		}

		// Obtener siguiente waypoint del pathfinding
		var nextWaypoint = pathfinding?.GetNextWaypoint(CurrentPos, TargetPos);

		if (nextWaypoint == null || nextWaypoint == CurrentPos)
		{
			// No hay ruta o estamos bloqueados
			// Intentar movimiento directo como fallback
			nextWaypoint = TargetPos;
		}

		// Mover hacia el siguiente waypoint
		Vector2 direction = ((Vector2)nextWaypoint - CurrentPos).Normalized();
		float moveDistance = Speed * (float)delta;
		Vector2 newPos = CurrentPos + (direction * moveDistance);

		// Si pasamos el waypoint, quedarse en él
		float distToWaypoint = CurrentPos.DistanceTo((Vector2)nextWaypoint);
		if (moveDistance >= distToWaypoint)
		{
			newPos = (Vector2)nextWaypoint;
		}

		CurrentPos = newPos;
		return false;
	}

	public void Cancel()
	{
		IsActive = false;
	}
}
