
# ¿Qué es este servidor?

Este proyecto es un **servidor dedicado autoritativo** para juegos RTS 2D (tipo Age of Empires), escrito en Go, pensado para partidas multijugador con validación estricta, alta concurrencia y sincronización en tiempo real vía WebSocket.

**Características principales:**
- Simulación de partidas, recursos, unidades y edificios en tiempo real.
- Mapa procedural, visibilidad (fog of war) y validación de comandos.
- Snapshots periódicos filtrados por jugador (solo ves lo que te corresponde).
- Lógica de combate, recolección, construcción, entrenamiento y eventos.
- API WebSocket simple y extensible, fácil de integrar desde Godot, Unity, JS, etc.
- Test suite exhaustiva y arquitectura modular lista para escalar y extender.

Ideal para usar como backend de un RTS propio, prototipos, IA, o como base para un juego comercial.

# Dedicated RTS Server (completo)

Servidor dedicado autoritativo para un RTS 2D estilo AoE con todas las mecánicas core implementadas.

## Ejecutar

```bash
go mod tidy
go run ./cmd/server  # expone WebSocket en :8080/ws
```

## Características implementadas

### Estados de partida
- `WAITING_FOR_PLAYERS` → `WAITING_FOR_READY` → `RUNNING` → `FINISHED`
- Solo permite comandos de juego cuando `RUNNING`

### Sistema de unidades
- **Villagers**: Recolectar recursos (madera, oro, piedra, comida), construir edificios, cazar animales, depositar recursos
- **Militia**: Combate cuerpo a cuerpo con cooldowns
- **Animales neutrales**: Roaming autónomo, pueden ser cazados por villagers

### Sistema de edificios
- **Town Center**: Entrena villagers, depósito de recursos
- **House**: Incrementa population cap (+5)
- **Barracks**: Entrena militia
- Construcción con progreso por ticks
- Validaciones de recursos para construir

### Colas de entrenamiento
- Por edificio (TC, Barracks)
- Validación previa: recursos, pop, estado completado
- Recursos se reservan al encolar
- Progreso solo en `RUNNING`

### Mapa procedural
- Generación automática con árboles (recurso madera)
- Densidad: ~1 árbol por 20 tiles
- Spawn de 10 animales neutrales

### Visibilidad y Fog of War (LoS)
- Cálculo por jugador en cada tick
- `visionRadius` por unidad/edificio
- Tiles descubiertos permanecen en memoria
- Solo se envían entidades visibles al cliente
- Eventos `ENTITY_CREATE`/`ENTITY_HIDDEN` cuando entran/salen de visión

### Sistema de combate
- Validación de rango y visibilidad
- Daño con cooldowns
- Muerte de unidades con evento `ENTITY_DESTROY`
- Solo militia puede atacar

### Movimiento
- Pathfinding lineal simple
- Move speed configurable por unidad
- Validación de ownership

### Ciclo de recursos
1. Gather (villager cerca de recurso)
2. Carry (transporta hasta 10 unidades)
3. Deposit (en Town Center)
4. Hunt (animales → comida directa)

### Persistencia
- Save/Load completo de estado de partida
- Fog descubierto por jugador
- JSON en directorio `saves/`

### Eventos detallados
- `ENTITY_CREATE`: spawn o entrada en visión
- `ENTITY_UPDATE`: cambios de estado
- `ENTITY_DESTROY`: muerte/eliminación
- `ENTITY_HIDDEN`: salida de visión
- `FOG_UPDATE`: tiles descubiertos
- `QUEUE_PROGRESS`: progreso de entrenamiento (solo propios)

### Command Pattern
- Validación previa de todos los comandos
- Rechazo con error si inválido
- Move, Attack, Gather, Build, Train, Deposit, Hunt

## Protocolo WebSocket

### Cliente → Servidor

```json
// Join
{"type":"join","playerId":1,"name":"Alice"}

// Ready
{"type":"ready","ready":true}

// Move
{"type":"move","unitId":10,"x":25.5,"y":30.2}

// Attack
{"type":"attack","unitId":12,"targetId":45}

// Gather
{"type":"gather","unitId":10,"resourceId":5}

// Build
{"type":"build","unitId":10,"buildingType":2,"x":20,"y":25}
// buildingType: 0=TownCenter, 1=Barracks, 2=House

// Train
{"type":"train","buildingId":1,"unitType":0}
// unitType: 0=Villager, 1=Militia

// Deposit
{"type":"deposit","unitId":10,"buildingId":1}

// Hunt
{"type":"hunt","unitId":10,"animalId":50}
```

### Servidor → Cliente

```json
// ACK
{"type":"ack","ok":true,"msg":"..."}

// Error
{"type":"error","error":"..."}

// Snapshot (cada 50ms)
{
  "type":"snapshot",
  "tick":1234,
  "units":[{"id":10,"owner":1,"type":0,"x":12.5,"y":10.3,"hp":100}],
  "buildings":[{"id":1,"owner":1,"type":0,"x":10,"y":10,"state":1,"progress":0}],
  "resources":[{"id":5,"type":0,"amount":150,"x":15.5,"y":8.2}],
  "events":[{"type":0,"tick":1234,"entityId":10,"data":{}}],
  "playerResources":{"food":500,"gold":200,"stone":200,"wood":0,"pop":3,"popCap":10}
}
```

### Event Types
- `0`: ENTITY_CREATE
- `1`: ENTITY_UPDATE
- `2`: ENTITY_DESTROY
- `3`: ENTITY_HIDDEN
- `4`: FOG_UPDATE
- `5`: QUEUE_PROGRESS
- `6`: RESOURCE_UPDATE

## Arquitectura

```
/cmd/server       - Entry point del servidor
/game             - Lógica core (Match, Unit, Building, eventos)
/world            - Mapa, recursos, visibilidad
/commands         - Command Pattern con validaciones
/network          - WebSocket server y snapshots filtrados
/persistence      - Save/Load de partida
```

## Siguientes pasos opcionales
- Pathfinding A* para movimiento complejo
- Formaciones de unidades
- Más tipos de unidades/edificios
- Sistema de tecnologías/upgrades
- Replay system con command logs
- Matchmaking y lobby multi-partida

