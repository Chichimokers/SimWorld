# Setup del cliente Godot para RTS

## Estructura de scripts creados

```
Scripts/
├── EntityModels.cs         # Definiciones de Unit, Building, Resource, etc.
├── GameState.cs           # Estado local del juego (sincronización de entidades)
├── SnapshotParser.cs      # Parser de JSON del servidor
├── NetworkManager.cs      # Gestor de conexión WebSocket y comandos
├── GameRenderer.cs        # Renderizado básico (canvas 2D)
└── GameManager.cs         # Orquestador principal
```

## Pasos para integrar en tu proyecto

### 1. Crear una escena principal (Main.tscn)
```
Main (Node)
├── GameManager (Script: GameManager.cs)
│   ├── NetworkManager (Script: NetworkManager.cs, será creado automáticamente)
│   ├── GameRenderer (Script: GameRenderer.cs, será creado automáticamente)
│   └── ... (UI nodes aquí más adelante)
```

### 2. Configurar NetworkManager
En `NetworkManager.cs`, editar los exports:
```gdscript
@export var ServerUrl = "ws://localhost:8080/ws"      # URL del servidor
@export var PlayerId = 1                               # ID único del jugador
@export var PlayerName = "Player1"                     # Nombre del jugador
@export var ReconnectDelay = 2.0                       # Tiempo entre reintentos
@export var MaxReconnectAttempts = 5                   # Máx intentos reconexión
```

### 3. Flujo de conexión
1. GameManager → _Ready()
2. NetworkManager → _Ready()
   - Crea WebSocketPeer
   - Conecta a ServerUrl
3. En _Process(), detecta conexión exitosa
   - Envía mensaje de Join
   - Servidor responde con ack
   - Cliente envía Ready
4. Servidor comienza a enviar snapshots cada ~50ms
5. GameRenderer recibe snapshots y dibuja

### 4. Flujo de entrada de usuario
```
GameRenderer._Input() 
  → Click en unidad: selectedUnitId = unitId
  → Click en terreno: SendMove(selectedUnitId, pos)
    → NetworkManager.SendMove() 
      → WebSocket.SendText(JSON)
```

### 5. Flujo de actualización de estado
```
NetworkManager._Process()
  → webSocket.Poll()
  → Recibe JSON del servidor
  → ProcessMessage()
    → HandleSnapshot() 
      → SnapshotParser.ParseSnapshot()
      → gameState.ApplySnapshot()
      → EmitSignal(OnSnapshotReceived)
        → GameRenderer.UpdateFromSnapshot()
          → QueueRedraw()
```

## Comandos disponibles (por implementar)

Ya están listos los métodos en NetworkManager:

- `SendMove(unitId, x, y)` - Mover unidad
- `SendAttack(unitId, targetId)` - Atacar
- `SendGather(unitId, resourceId)` - Recolectar
- `SendBuild(unitId, buildingType, x, y)` - Construir
- `SendTrain(buildingId, unitType)` - Entrenar unidad
- `SendDeposit(unitId, buildingId)` - Depositar recursos
- `SendHunt(unitId, animalId)` - Cazar animal

## Pruebas

### Test 1: Conexión y snapshot
```
1. Ejecutar servidor Go: go run ./cmd/server
2. Ejecutar cliente Godot (play scene Main)
3. Ver logs:
   - "🔌 Conectando a ws://localhost:8080/ws..."
   - "✅ Join aceptado por servidor"
   - "✅ Listo para jugar"
   - "📦 Snapshot recibido: tick=X, units=Y, buildings=Z"
```

### Test 2: Movimiento
```
1. Click en unidad (debe aparecer selecta en amarillo)
2. Click en terreno vacío
3. Ver en consola: "📍 Moviendo unidad X a (pos.x, pos.y)"
4. Unidad se mueve suavemente en siguiente snapshot
```

### Test 3: Múltiples snapshots
```
Cada 50ms el servidor envía un snapshot nuevo.
El cliente debe actualizar positions sin lag.
```

## Próximas mejoras (Sprint 2)

- [ ] Interpolación/extrapolación de movimiento
- [ ] UI HUD (recursos, pop, colas)
- [ ] Selección múltiple de unidades
- [ ] Comandos de ataque y recolección
- [ ] Visibilidad (fog of war visual)
- [ ] Mini-map
- [ ] Sonidos y animaciones
- [ ] Input handling (atajos de teclado)

## Notas sobre autoridad

- **Servidor es autoritativo**: todas las decisiones finales las toma el servidor.
- **Cliente es optimista**: predice para UX, pero acepta correcciones del servidor.
- **Validación**: el servidor valida todos los comandos y rechaza los inválidos.

## Debugging

Habilitar logs adicionales:
```csharp
// En NetworkManager.cs, descomentar logs en ProcessMessage()
GD.Print($"📤 Enviado: {json}");
GD.Print($"📥 Recibido: {json}");
```

## Solución de problemas

| Problema | Solución |
|----------|----------|
| WebSocket no conecta | Verificar ServerUrl y que servidor esté corriendo en :8080 |
| Snapshots no llegan | Revisar logs del servidor, check `DEV_BOT=true` mode |
| Comandos no funcionan | Validar que gameState.PlayerId coincida con servidor |
| Lag visual | Implementar interpolación (Sprint 2) |

---

¡Listo para empezar! 🚀
