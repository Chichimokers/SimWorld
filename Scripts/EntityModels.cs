using System.Collections.Generic;

/// <summary>
/// Estructuras de datos que representan entidades del juego.
/// Se sincronizan desde snapshots del servidor.
/// </summary>

public class Unit
{
    public int Id { get; set; }
    public int Owner { get; set; } // 0 = neutral, >0 = player id
    public int Type { get; set; } // 0=Villager, 1=Militia, 2=Animal
    public float X { get; set; }
    public float Y { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; } = 100;
    public float Speed { get; set; } = 0.1f;
    public int Carrying { get; set; } = 0; // Cantidad de recurso que lleva (solo aldeanos)
    public int CarryType { get; set; } = 0; // Tipo de recurso (0=wood, 1=gold, 2=stone, 3=food)
    public bool Visible { get; set; } = true; // Controlado por FOW
}

public class Building
{
    public int Id { get; set; }
    public int Owner { get; set; }
    public int Type { get; set; } // 0=TownCenter, 1=Barracks, 2=House
    public float X { get; set; }
    public float Y { get; set; }
    public int State { get; set; } // 0=construyendo, 1=completado
    public int Progress { get; set; } // 0-100 para construcción
    public int HP { get; set; }
    public int MaxHp { get; set; } = 1500;
    public bool Visible { get; set; } = true;
    public List<Tile> OccupiedTiles { get; set; } = new List<Tile>();
}

public class Resource
{
    public int Id { get; set; }
    public int Type { get; set; } // 0=wood, 1=gold, 2=stone, 3=food
    public int Amount { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool Visible { get; set; } = true;
}

public class PlayerResources
{
    public int Food { get; set; } = 0;
    public int Gold { get; set; } = 0;
    public int Stone { get; set; } = 0;
    public int Wood { get; set; } = 0;
    public int Pop { get; set; } = 0;
    public int PopCap { get; set; } = 10;
}

public class GameEvent
{
    public int Type { get; set; } // 0=CREATE, 1=UPDATE, 2=DESTROY, 3=HIDDEN, 4=FOG_UPDATE, 5=QUEUE_PROGRESS, 6=RESOURCE_UPDATE, 999=OVERFLOW
    public int Tick { get; set; }
    public int EntityId { get; set; }
    public System.Collections.Generic.Dictionary<string, object> Data { get; set; }
}

public class Tile
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class TrainingQueue
{
    public int BuildingId { get; set; }
    public int BuildingType { get; set; } // 0=TownCenter, 1=Barracks, 2=House
    public List<int> Items { get; set; } = new List<int>(); // Array de unitTypes en cola
    public int CurrentTime { get; set; } // Progreso actual (0-currentMax)
    public int CurrentMax { get; set; } // Tiempo total de entrenamiento
}

public class Snapshot
{
    public int Tick { get; set; }
    public int MapWidth { get; set; } = 256;
    public int MapHeight { get; set; } = 256;
    
    // Tus entidades (siempre visibles)
    public List<Unit> Units { get; set; } = new List<Unit>();
    public List<Building> Buildings { get; set; } = new List<Building>();
    
    // Entidades enemigas en rango de visión
    public List<Unit> EnemyUnitsInView { get; set; } = new List<Unit>();
    public List<Building> EnemyBuildingsInView { get; set; } = new List<Building>();
    
    // Recursos y eventos
    public List<Resource> Resources { get; set; } = new List<Resource>();
    public List<GameEvent> Events { get; set; } = new List<GameEvent>();
    
    // Recursos del jugador
    public PlayerResources PlayerResources { get; set; } = new PlayerResources();
    
    // Visibilidad (Fog of War)
    public List<Tile> VisibleTiles { get; set; } = new List<Tile>();
    public List<Tile> SeenTiles { get; set; } = new List<Tile>();
    
    // Colas de entrenamiento
    public List<TrainingQueue> TrainingQueues { get; set; } = new List<TrainingQueue>();
}
