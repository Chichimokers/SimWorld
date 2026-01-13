# 🔄 Arquitectura de Comunicación Cliente-Servidor RTS

## 📋 Índice
1. [Diagrama de Flujo General](#diagrama-de-flujo-general)
2. [Clases de Mensajes (C#)](#clases-de-mensajes-c)
3. [Ciclo de Vida de una Partida](#ciclo-de-vida-de-una-partida)
4. [Flujos de Interacción Detallados](#flujos-de-interacción-detallados)
5. [Ejemplos Prácticos](#ejemplos-prácticos)
6. [Manejo de Errores](#manejo-de-errores)

---

## Diagrama de Flujo General

```
┌─────────────┐                              ┌──────────────┐
│   CLIENTE   │          WebSocket           │   SERVIDOR   │
│   (C#)      │◄────────────────────────────►│   (Go)       │
└─────────────┘                              └──────────────┘
      │                                             │
      │ 1. Conecta                                 │
      ├─────────────────────────────────────────►│ (handleWS)
      │                                             │
      │ 2. Envía "join"                           │
      ├─────────────────────────────────────────►│ (AddPlayer)
      │◄─────────────────────────────────────────┤ {"type":"ack", "ok":true}
      │                                             │
      │ 3. Envía "ready"                          │
      ├─────────────────────────────────────────►│ (SetPlayerReady)
      │◄─────────────────────────────────────────┤ {"type":"ack"}
      │                                             │
      │ 4. Recibe snapshots cada 50ms            │ (broadcastSnapshots)
      │◄─────────────────────────────────────────┤ {"type":"snapshot", ...}
      │                                             │
      │ 5. Envía comandos (move, build, etc)     │
      ├─────────────────────────────────────────►│ (handleMessage)
      │◄─────────────────────────────────────────┤ {"type":"ack"} o {"type":"error"}
      │                                             │
```

---

## Clases de Mensajes (C#)

### 🔹 Clase Base: Mensaje

```csharp
/// <summary>
/// Envelope base para todos los mensajes entre cliente y servidor.
/// Se envía como JSON sobre WebSocket.
/// </summary>
public class GameMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } // "join", "ready", "move", "build", etc.

    // Campos opcionales según el tipo de mensaje
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("unitId")]
    public ulong UnitID { get; set; }

    [JsonPropertyName("buildingId")]
    public ulong BuildingID { get; set; }

    [JsonPropertyName("buildingType")]
    public int BuildingType { get; set; }

    [JsonPropertyName("unitType")]
    public int UnitType { get; set; }

    [JsonPropertyName("resourceId")]
    public ulong ResourceID { get; set; }

    [JsonPropertyName("targetId")]
    public ulong TargetID { get; set; }

    [JsonPropertyName("animalId")]
    public ulong AnimalID { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
```

### 🔹 Respuesta del Servidor: ACK

```csharp
/// <summary>
/// Respuesta de confirmación del servidor.
/// Indicaindicates si la acción fue exitosa.
/// </summary>
public class AckMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ack"; // Siempre "ack"

    [JsonPropertyName("ok")]
    public bool Ok { get; set; } // true = éxito, false = error

    [JsonPropertyName("msg")]
    public string Message { get; set; } // Mensaje descriptivo
}
```

### 🔹 Respuesta del Servidor: Error

```csharp
/// <summary>
/// Mensaje de error del servidor.
/// Se envía cuando hay un problema con el comando.
/// </summary>
public class ErrorMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("error")]
    public string Error { get; set; } // Descripción del error
}
```

### 🔹 Snapshot: Estado del Juego

```csharp
/// <summary>
/// Snapshot completo del estado del juego.
/// Se envía cada 50ms (20 ticks por segundo).
/// Solo incluye lo visible para el jugador (filtrado por LoS).
/// </summary>
public class GameSnapshot
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "snapshot";

    [JsonPropertyName("tick")]
    public ulong Tick { get; set; } // Número del tick actual del servidor

    // Propias entidades (siempre visible para ti)
    [JsonPropertyName("units")]
    public List<UnitView> OwnUnits { get; set; }

    [JsonPropertyName("buildings")]
    public List<BuildingView> OwnBuildings { get; set; }

    // Entidades enemigas (solo si están en rango de visión)
    [JsonPropertyName("enemyUnitsInView")]
    public List<UnitView> EnemyUnits { get; set; }

    [JsonPropertyName("enemyBuildingsInView")]
    public List<BuildingView> EnemyBuildings { get; set; }

    // Recursos disponibles
    [JsonPropertyName("resources")]
    public List<ResourceView> Resources { get; set; }

    // Eventos ocurridos en este tick
    [JsonPropertyName("events")]
    public List<EventView> Events { get; set; }

    // Recursos del jugador
    [JsonPropertyName("playerResources")]
    public PlayerResourcesView? PlayerResources { get; set; }

    // Información del mapa
    [JsonPropertyName("visibleTiles")]
    public List<TileView> VisibleTiles { get; set; } // Rango de visión actual

    [JsonPropertyName("seenTiles")]
    public List<TileView> SeenTiles { get; set; } // Fog of war (explorado antes)

    [JsonPropertyName("mapWidth")]
    public int MapWidth { get; set; }

    [JsonPropertyName("mapHeight")]
    public int MapHeight { get; set; }

    // Colas de entrenamiento
    [JsonPropertyName("trainingQueues")]
    public List<TrainingQueueView> TrainingQueues { get; set; }
}
```

### 🔹 Vistas: Entidades en el Snapshot

```csharp
/// <summary>
/// Vista de una unidad en el snapshot.
/// </summary>
public class UnitView
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("owner")]
    public int Owner { get; set; } // ID del jugador propietario

    [JsonPropertyName("type")]
    public int Type { get; set; } // 0=Villager, 1=Militia, 2=Animal

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("hp")]
    public int HP { get; set; }
}

/// <summary>
/// Vista de un edificio en el snapshot.
/// </summary>
public class BuildingView
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("owner")]
    public int Owner { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; } // 0=TownCenter, 1=Barracks, 2=House

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("state")]
    public int State { get; set; } // 0=Construyendo, 1=Completado

    [JsonPropertyName("progress")]
    public int Progress { get; set; } // 0-100 durante construcción

    [JsonPropertyName("hp")]
    public int HP { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHP { get; set; }
}

/// <summary>
/// Vista de un recurso (árbol, animal, etc).
/// </summary>
public class ResourceView
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; } // 0=Árbol/Madera

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

/// <summary>
/// Evento que ocurrió en el servidor.
/// </summary>
public class EventView
{
    [JsonPropertyName("type")]
    public int Type { get; set; } // Tipo de evento

    [JsonPropertyName("tick")]
    public ulong Tick { get; set; } // Tick cuando ocurrió

    [JsonPropertyName("entityId")]
    public ulong EntityId { get; set; } // Entidad afectada

    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Recursos actuales del jugador.
/// </summary>
public class PlayerResourcesView
{
    [JsonPropertyName("food")]
    public int Food { get; set; }

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("stone")]
    public int Stone { get; set; }

    [JsonPropertyName("wood")]
    public int Wood { get; set; }

    [JsonPropertyName("pop")]
    public int PopulationUsed { get; set; }

    [JsonPropertyName("popCap")]
    public int PopulationCap { get; set; }
}

/// <summary>
/// Tile visible en el mapa.
/// </summary>
public class TileView
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

/// <summary>
/// Cola de entrenamiento de un edificio.
/// </summary>
public class TrainingQueueView
{
    [JsonPropertyName("buildingId")]
    public ulong BuildingId { get; set; }

    [JsonPropertyName("buildingType")]
    public int BuildingType { get; set; }

    [JsonPropertyName("items")]
    public List<int> Items { get; set; } // Unidades en cola

    [JsonPropertyName("currentTime")]
    public int CurrentTime { get; set; }

    [JsonPropertyName("currentMax")]
    public int CurrentMax { get; set; }
}
```

---

## Ciclo de Vida de una Partida

### Estado 1: WAITING_FOR_PLAYERS

```
Servidor espera jugadores
└─ Jugador 1 envía "join"
   └─ Servidor: AddPlayer("Jugador1")
      └─ PlayerID = 1 asignado
      └─ En modo dev: Bot creado automáticamente
      └─ Transición a WAITING_FOR_READY
```

### Estado 2: WAITING_FOR_READY

```
Servidor espera que ambos jugadores marquen ready
├─ Jugador 1 envía "ready"
│  └─ Servidor: SetPlayerReady(1, true)
│
└─ Jugador 2 (o bot) ya está ready
   └─ Servidor: tryStart()
      └─ Todos ready? SÍ
         └─ Transición a RUNNING
```

### Estado 3: RUNNING

```
Juego activo, se ejecutan ticks cada 50ms
├─ Cada tick:
│  ├─ Match.Tick()
│  │  ├─ Actualizar posiciones
│  │  ├─ Procesar construcciones
│  │  ├─ Procesar entrenamientos
│  │  └─ Actualizar visibilidad
│  │
│  └─ broadcastSnapshots()
│     └─ Para cada cliente:
│        └─ BuildSnapshot(playerID)
│           └─ Enviar snapshot filtrado
│
├─ Cliente recibe snapshot cada 50ms
│
└─ Cliente puede enviar comandos:
   ├─ move
   ├─ build
   ├─ train
   ├─ gather
   ├─ attack
   └─ hunt
```

---

## Flujos de Interacción Detallados

### 🔹 Flujo 1: Conexión e Inicio de Partida

```
CLIENTE                              SERVIDOR
  │                                     │
  ├──── WebSocket Upgrade ────────────►│ handleWS()
  │◄──── Conexión aceptada ────────────┤
  │                                     │
  ├──── {"type":"join","name":"Player1"}────►│ handleMessage()
  │                                     │     AddPlayer("Player1")
  │                                     │     └─ playerID = 1 asignado
  │◄──── {"type":"ack","ok":true,"msg":"joined with playerID 1"}┤
  │                                     │
  ├──── {"type":"ready","ready":true}───►│ SetPlayerReady(1, true)
  │                                     │ tryStart()
  │◄──── {"type":"ack","ok":true}───────┤ → State = RUNNING
  │                                     │
  │◄──── {"type":"snapshot", tick:1...}┤ broadcastSnapshots()
  │◄──── {"type":"snapshot", tick:2...}┤ [cada 50ms]
  │◄──── {"type":"snapshot", tick:3...}┤
  │      ...                           │
```

### 🔹 Flujo 2: Mover Unidad

```csharp
// CLIENTE
var moveCommand = new GameMessage
{
    Type = "move",
    UnitID = 42,
    X = 10.5f,
    Y = 15.3f
};
SendToServer(moveCommand);

// Espera respuesta
// → Recibe: {"type":"ack","ok":true,"msg":"moving"}
// → En snapshots posteriores, verá la unidad moviéndose
```

```
CLIENTE                              SERVIDOR
  │                                     │
  ├──── {"type":"move","unitId":42,...}────►│ handleMessage()
  │                                     │     MoveUnit(1, 42, Vec2{X,Y})
  │                                     │     └─ Valida: ¿es tu unidad?
  │                                     │     └─ Valida: ¿rango válido?
  │                                     │     └─ Agrega comando a cola
  │◄──── {"type":"ack","ok":true}───────┤
  │                                     │
  │  [siguiente tick]                  │ Tick()
  │  ├─ Unidad se mueve               │ ├─ MovementSystem.Update()
  │  └─ Snapshot actualizado          │ └─ GenerateSnapshot()
  │◄──── {"type":"snapshot"...}────────┤
```

### 🔹 Flujo 3: Construir Edificio

```csharp
// CLIENTE
var buildCommand = new GameMessage
{
    Type = "build",
    UnitID = 10,           // Villager que construye
    BuildingType = 0,      // TownCenter
    X = 20.0f,
    Y = 25.0f
};
SendToServer(buildCommand);

// Respuesta inmediata:
// → {"type":"ack","ok":true,"msg":"building"}

// En snapshots posteriores:
// → Buildings contiene nuevo edificio con state=0, progress=0
// → Cada tick: progress += 1
// → Al alcanzar 100: state = 1 (completado)
```

```
CLIENTE                              SERVIDOR
  │                                     │
  ├──── {"type":"build","unitId":10...}────►│ handleMessage()
  │                                     │     BuildBuilding(1, 10, TownCenter, Vec2)
  │                                     │     └─ Valida recursos
  │                                     │     └─ Valida posición
  │                                     │     └─ Crea edificio state=0
  │◄──── {"type":"ack"}──────────────────┤
  │                                     │
  │  [Ticks subsecuentes]              │
  │◄──── snapshot: Buildings[].progress=1─┤ Tick() → Incrementa progreso
  │◄──── snapshot: Buildings[].progress=2─┤
  │      ...                            │
  │◄──── snapshot: Buildings[].progress=100│
  │                                     │ → state = 1 (Completado!)
```

### 🔹 Flujo 4: Entrenar Unidad

```csharp
// CLIENTE
var trainCommand = new GameMessage
{
    Type = "train",
    BuildingID = 5,   // ID del TownCenter
    UnitType = 0      // Villager
};
SendToServer(trainCommand);

// Respuesta:
// → {"type":"ack","ok":true,"msg":"queued"}

// En snapshots posteriores:
// → TrainingQueues[0].Items = [0] (Villager en cola)
// → TrainingQueues[0].CurrentTime incrementa cada tick
// → Al alcanzar CurrentMax: Nueva unidad spawneada
```

```
CLIENTE                              SERVIDOR
  │                                     │
  ├──── {"type":"train","buildingId":5...}──►│ handleMessage()
  │                                     │     Queue.Enqueue(Match, Building, UnitType)
  │                                     │     └─ Resta recursos
  │                                     │     └─ Agrega a cola
  │◄──── {"type":"ack","ok":true}────────┤
  │                                     │
  │  [Ticks]                           │ TrainingQueue.Update()
  │◄──── snapshot: trainingQueues[0]───┤ ├─ currentTime += 1
  │      currentTime=10/100            │ └─ Si currentTime >= currentMax:
  │◄──── snapshot: trainingQueues[0]───┤    └─ Pop unit, spawn it
  │      currentTime=20/100            │    └─ Eventos: ENTITY_CREATE
  │      ...                            │
  │◄──── snapshot: trainingQueues[0]───┤
  │      Items=[] (cola vacía)         │ Nueva unidad en snapshot!
  │◄──── snapshot: units=[...nuevaUnidad]┤
```

### 🔹 Flujo 5: Recolectar Recurso

```csharp
// CLIENTE
var gatherCommand = new GameMessage
{
    Type = "gather",
    UnitID = 12,       // Villager
    ResourceID = 99    // ID del árbol/recurso
};
SendToServer(gatherCommand);

// En snapshots:
// → Villager se mueve al recurso
// → Resurso.Amount disminuye
// → Villager.Carrying aumenta
// → Cuando está lleno, necesita depositSource
```

### 🔹 Flujo 6: Depositar Recursos

```csharp
// CLIENTE
var depositCommand = new GameMessage
{
    Type = "deposit",
    UnitID = 12,       // Villager
    BuildingID = 5     // TownCenter
};
SendToServer(depositCommand);

// En snapshots:
// → PlayerResources.Food aumenta
// → Villager.Carrying se vacía
```

### 🔹 Flujo 7: Atacar Enemigo

```csharp
// CLIENTE
var attackCommand = new GameMessage
{
    Type = "attack",
    UnitID = 50,       // Militia propia
    TargetID = 35      // Unidad enemiga en vista
};
SendToServer(attackCommand);

// En snapshots:
// → Unidad enemiga: HP disminuye cada tick
// → Si HP ≤ 0: ENTITY_DESTROY en eventos
// → Unidad desaparece del snapshot
```

---

## Ejemplos Prácticos

### Ejemplo Completo: Inicio de Partida en C#

```csharp
public class RTSGameClient
{
    private ClientWebSocket? _socket;
    private int _playerID = 0;
    private GameSnapshot? _lastSnapshot;
    private bool _isReady = false;

    /// <summary>
    /// Conectar al servidor y unirse a una partida.
    /// </summary>
    public async Task ConnectAndJoin(string playerName)
    {
        // 1. Conectar WebSocket
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(
            new Uri("ws://localhost:8080/ws"),
            CancellationToken.None
        );
        Console.WriteLine("✅ Conectado al servidor");

        // 2. Empezar a escuchar mensajes
        _ = ReadMessagesLoop();

        // 3. Enviar join
        await SendMessage(new GameMessage
        {
            Type = "join",
            Name = playerName
        });
        Console.WriteLine($"📤 Enviando join: {playerName}");

        // 4. Esperar a que se asigne playerID (viene en el ack)
        await Task.Delay(100);
    }

    /// <summary>
    /// Marcar como listo para iniciar el juego.
    /// </summary>
    public async Task SetReady()
    {
        if (_playerID == 0)
        {
            Console.WriteLine("❌ No estás unido aún (playerID = 0)");
            return;
        }

        await SendMessage(new GameMessage
        {
            Type = "ready",
            Ready = true
        });
        _isReady = true;
        Console.WriteLine("📤 Marcado como listo");
    }

    /// <summary>
    /// Mover una unidad.
    /// </summary>
    public async Task MoveUnit(ulong unitId, float x, float y)
    {
        await SendMessage(new GameMessage
        {
            Type = "move",
            UnitID = unitId,
            X = x,
            Y = y
        });
    }

    /// <summary>
    /// Construir un edificio.
    /// </summary>
    public async Task BuildBuilding(ulong villagerID, int buildingType, float x, float y)
    {
        await SendMessage(new GameMessage
        {
            Type = "build",
            UnitID = villagerID,
            BuildingType = buildingType,
            X = x,
            Y = y
        });
    }

    /// <summary>
    /// Entrenar una unidad en un edificio.
    /// </summary>
    public async Task TrainUnit(ulong buildingID, int unitType)
    {
        await SendMessage(new GameMessage
        {
            Type = "train",
            BuildingID = buildingID,
            UnitType = unitType
        });
    }

    /// <summary>
    /// Enviar un mensaje al servidor.
    /// </summary>
    private async Task SendMessage(GameMessage message)
    {
        if (_socket == null || _socket.State != WebSocketState.Open)
        {
            Console.WriteLine("❌ Conexión cerrada");
            return;
        }

        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    /// <summary>
    /// Loop que escucha mensajes del servidor.
    /// </summary>
    private async Task ReadMessagesLoop()
    {
        if (_socket == null) return;

        var buffer = new byte[4096];

        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None
                );

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(
                        buffer,
                        0,
                        result.Count
                    );

                    // Intentar parsear como snapshot
                    try
                    {
                        var snap = JsonSerializer.Deserialize<GameSnapshot>(json);
                        if (snap?.Type == "snapshot")
                        {
                            _lastSnapshot = snap;
                            OnSnapshotReceived(snap);
                            continue;
                        }
                    }
                    catch { }

                    // Intentar parsear como ack/error
                    try
                    {
                        var ack = JsonSerializer.Deserialize<AckMessage>(json);
                        if (ack?.Type == "ack")
                        {
                            OnAckReceived(ack);
                            continue;
                        }
                    }
                    catch { }

                    try
                    {
                        var err = JsonSerializer.Deserialize<ErrorMessage>(json);
                        if (err?.Type == "error")
                        {
                            OnErrorReceived(err);
                            continue;
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en ReadMessagesLoop: {ex}");
        }
    }

    /// <summary>
    /// Callback cuando se recibe un snapshot.
    /// </summary>
    private void OnSnapshotReceived(GameSnapshot snapshot)
    {
        Console.Clear();
        Console.WriteLine($"📊 TICK {snapshot.Tick} | Mapa: {snapshot.MapWidth}x{snapshot.MapHeight}");
        Console.WriteLine();

        if (snapshot.PlayerResources != null)
        {
            Console.WriteLine(
                $"💰 Food={snapshot.PlayerResources.Food} " +
                $"Gold={snapshot.PlayerResources.Gold} " +
                $"Stone={snapshot.PlayerResources.Stone} " +
                $"Wood={snapshot.PlayerResources.Wood} " +
                $"| Pop {snapshot.PlayerResources.PopulationUsed}/{snapshot.PlayerResources.PopulationCap}"
            );
        }

        Console.WriteLine($"\n🛡️ Tus Unidades: {snapshot.OwnUnits.Count}");
        foreach (var unit in snapshot.OwnUnits)
        {
            Console.WriteLine(
                $"  ID={unit.Id} Type={unit.Type} Pos=({unit.X:F1},{unit.Y:F1}) HP={unit.HP}"
            );
        }

        Console.WriteLine($"\n🏗️ Tus Edificios: {snapshot.OwnBuildings.Count}");
        foreach (var building in snapshot.OwnBuildings)
        {
            Console.WriteLine(
                $"  ID={building.Id} Type={building.Type} " +
                $"Pos=({building.X:F1},{building.Y:F1}) " +
                $"State={building.State} Progress={building.Progress}% " +
                $"HP={building.HP}/{building.MaxHP}"
            );
        }

        Console.WriteLine($"\n👹 Enemigos en Vista: {snapshot.EnemyUnits.Count + snapshot.EnemyBuildings.Count}");
        foreach (var unit in snapshot.EnemyUnits)
        {
            Console.WriteLine(
                $"  🗡️ ID={unit.Id} Type={unit.Type} Pos=({unit.X:F1},{unit.Y:F1}) HP={unit.HP}"
            );
        }

        Console.WriteLine($"\n📋 Escribe un comando o 'help' para ayuda");
    }

    private void OnAckReceived(AckMessage ack)
    {
        if (ack.Message.Contains("playerID"))
        {
            // Extraer playerID del mensaje "joined with playerID X"
            if (int.TryParse(
                ack.Message.Split(' ').Last(),
                out var id))
            {
                _playerID = id;
                Console.WriteLine($"✅ PlayerID asignado: {id}");
            }
        }
        else
        {
            Console.WriteLine($"✅ {ack.Message}");
        }
    }

    private void OnErrorReceived(ErrorMessage error)
    {
        Console.WriteLine($"❌ {error.Error}");
    }
}
```

### Uso del Cliente

```csharp
class Program
{
    static async Task Main()
    {
        var client = new RTSGameClient();

        // Conectarse
        await client.ConnectAndJoin("MiJugador");

        // Esperar un poco
        await Task.Delay(1000);

        // Marcar ready
        await client.SetReady();

        // Ejecutar comandos
        while (true)
        {
            var line = Console.ReadLine();
            var parts = line?.Split(' ') ?? Array.Empty<string>();

            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "move":
                    if (parts.Length >= 4 &&
                        ulong.TryParse(parts[1], out var uid) &&
                        float.TryParse(parts[2], out var x) &&
                        float.TryParse(parts[3], out var y))
                    {
                        await client.MoveUnit(uid, x, y);
                    }
                    break;

                case "build":
                    if (parts.Length >= 5 &&
                        ulong.TryParse(parts[1], out var villageID) &&
                        int.TryParse(parts[2], out var buildType) &&
                        float.TryParse(parts[3], out var bx) &&
                        float.TryParse(parts[4], out var by))
                    {
                        await client.BuildBuilding(villageID, buildType, bx, by);
                    }
                    break;

                case "train":
                    if (parts.Length >= 3 &&
                        ulong.TryParse(parts[1], out var buildID) &&
                        int.TryParse(parts[2], out var unitType))
                    {
                        await client.TrainUnit(buildID, unitType);
                    }
                    break;

                case "exit":
                    return;
            }
        }
    }
}
```

---

## Manejo de Errores

### Errores Comunes y Respuestas

| Error | Causa | Solución |
|-------|-------|----------|
| `"join first"` | Intentaste un comando sin unirte | Primero ejecuta `join <nombre>` |
| `"match full"` | Dos jugadores ya conectados | Espera o reconecta |
| `"player already exists"` | Ya estás unido con ese ID | (No debería ocurrir con auto-assign) |
| `"insufficient resources"` | No tienes recursos para construir/entrenar | Recolecta más recursos |
| `"building not found or not trainable"` | El edificio no existe o no entrena | Verifica ID del edificio |
| `"unit not found"` | La unidad no existe o fue destruida | Verifica ID de unidad |
| `"invalid position"` | Posición fuera del mapa u ocupada | Intenta otra posición |
| `"out of range"` | La acción está fuera de rango | Acércate más |

### Manejo en C#

```csharp
try
{
    await client.BuildBuilding(unitId, buildingType, x, y);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error al construir: {ex.Message}");
}

// O esperar el mensaje de error del servidor
private void OnErrorReceived(ErrorMessage error)
{
    switch (error.Error)
    {
        case "insufficient resources":
            Console.WriteLine("❌ No tienes suficientes recursos");
            // Lógica de UI para mostrar recursos faltantes
            break;

        case "match full":
            Console.WriteLine("❌ La partida está llena");
            // Reconectar a otra partida
            break;

        default:
            Console.WriteLine($"❌ Error: {error.Error}");
            break;
    }
}
```

---

## Constantes de Tipos

```csharp
/// <summary>
/// Tipos de unidades.
/// </summary>
public static class UnitTypes
{
    public const int Villager = 0;  // Recolecta recursos, construye
    public const int Militia = 1;   // Combate
    public const int Animal = 2;    // Neutral, puede ser cazado
}

/// <summary>
/// Tipos de edificios.
/// </summary>
public static class BuildingTypes
{
    public const int TownCenter = 0;   // Entrena villagers, acepta depósitos
    public const int Barracks = 1;     // Entrena militia
    public const int House = 2;        // Incrementa population cap
}

/// <summary>
/// Tipos de recursos.
/// </summary>
public static class ResourceTypes
{
    public const int Wood = 0;
    public const int Food = 1;
    public const int Gold = 2;
    public const int Stone = 3;
}

/// <summary>
/// Estados de edificio.
/// </summary>
public static class BuildingStates
{
    public const int Constructing = 0;
    public const int Completed = 1;
}

/// <summary>
/// Tipos de eventos.
/// </summary>
public static class EventTypes
{
    public const int EntityCreate = 0;
    public const int EntityDestroy = 1;
    public const int EntityHidden = 2;
    public const int ResourceDepleted = 3;
    public const int BuildingCompleted = 4;
    public const int UnitTrained = 5;
}
```

---

## Diagrama de Secuencia Completo

```
┌──────────┐                                              ┌──────────┐
│ Cliente  │                                              │ Servidor │
└────┬─────┘                                              └────┬─────┘
     │                                                          │
     │ 1. WebSocket Upgrade                                    │
     ├─────────────────────────────────────────────────────────►│
     │                                                          │
     │ 2. {"type":"join","name":"Player1"}                     │
     ├─────────────────────────────────────────────────────────►│
     │                                                   AddPlayer()
     │                                                   playerID=1
     │                                               Bot created (999)
     │                                             WAITING_FOR_READY
     │◄──────────{"type":"ack","ok":true}──────────────────────┤
     │                                                          │
     │ 3. {"type":"ready","ready":true}                        │
     ├─────────────────────────────────────────────────────────►│
     │                                                  SetPlayerReady()
     │                                                   tryStart()
     │                                                   RUNNING!
     │◄──────────{"type":"ack","ok":true}──────────────────────┤
     │                                                          │
     │ 4. {"type":"move","unitId":1,"x":5,"y":5}              │
     ├─────────────────────────────────────────────────────────►│
     │                                                  MoveUnit()
     │                                               Queue command
     │◄──────────{"type":"ack","ok":true}──────────────────────┤
     │                                                          │
     │ [Ticks running every 50ms]                             │
     │ Tick 1  ◄──────────Snapshot(tick:1)──────────────────────┤
     │ Tick 2  ◄──────────Snapshot(tick:2)──────────────────────┤
     │ Tick 3  ◄──────────Snapshot(tick:3)──────────────────────┤
     │         [Unit has moved, visible in snapshot]            │
     │                                                          │
     │ 5. {"type":"build","unitId":1,"buildingType":0,"x":10,"y":10}
     ├─────────────────────────────────────────────────────────►│
     │                                               BuildBuilding()
     │                                            Building created
     │◄──────────{"type":"ack"}─────────────────────────────────┤
     │                                                          │
     │ Ticks running...                                        │
     │ Tick 5  ◄──────────Snapshot(buildings:[{progress:0}])───┤
     │ Tick 6  ◄──────────Snapshot(buildings:[{progress:1}])───┤
     │ Tick 7  ◄──────────Snapshot(buildings:[{progress:2}])───┤
     │ ...     ◄──────────Snapshot(buildings:[{progress:100}])─┤
     │                                                          │
     │ 6. {"type":"train","buildingId":99,"unitType":0}        │
     ├─────────────────────────────────────────────────────────►│
     │                                               Queue.Enqueue()
     │                                                          │
     │◄──────────{"type":"ack"}─────────────────────────────────┤
     │                                                          │
     │ Ticks...                                                │
     │ ◄──────────Snapshot(trainingQueues:[...progress...])────┤
     │ ◄──────────Snapshot(units:[...new unit spawned...])─────┤
     │                                                          │
     │ JUEGO CONTINÚA...                                       │
     │                                                          │
```

---

## Resumen

La arquitectura de comunicación es:

1. **Cliente** → **Servidor**: Comandos de jugador (move, build, train, etc.)
2. **Servidor** → **Cliente**: Snapshots cada 50ms + ACK/Error
3. **Estado**: Se mantiene 100% en el servidor (autoridad única)
4. **Validación**: Todos los comandos se validan en servidor
5. **Sincronización**: Cliente siempre ve el estado actual vía snapshots
6. **Escalabilidad**: Se pueden agregar más clientes sin cambiar lógica

El servidor es la **fuente de verdad absoluta** ⚖️
