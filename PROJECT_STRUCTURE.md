# 📝 ESTRUCTURA DEL PROYECTO - SupaWorld Cliente Godot

## 🎯 Estado Actual: v0.1.0 (Fundamentos Implementados)

### ✅ Implementado
- [x] Cliente WebSocket (conexión dual: world + player)
- [x] Gestor de Chunks (carga/descarga dinámica)
- [x] Sistema de Jugador (movimiento, posición)
- [x] Gestor de NPCs (creación, actualización)
- [x] HUD básico (posición, stats)
- [x] Consola de Debug (comandos in-game)
- [x] Logger centralizado (archivo de logs)
- [x] Gestor de Assets (precarga y caché)
- [x] Configuración global (GameConfig.cs)
- [x] Parser de datos del servidor

### ⏳ Por Hacer (Próximos Pasos)
- [ ] Cargar tilesets reales desde Assets/
- [ ] Sincronizar completamente con servidor
- [ ] Sistema de combate
- [ ] Animaciones de personaje
- [ ] Inventario visual
- [ ] Menú principal
- [ ] Persistencia local (guardado)
- [ ] Efectos de sonido

---

## 📁 Árbol de Carpetas Completo

```
d:\Proyectos\GameGodotnewRoguelike\
│
├── Scripts/                          # Código fuente C#
│   ├── GameManager.cs                # 🎮 Orquestador principal
│   ├── GameConfig.cs                 # ⚙️ Configuración global
│   ├── GameLogger.cs                 # 📋 Sistema de logging
│   ├── AssetManager.cs               # 🖼️ Gestor de recursos
│   │
│   ├── Network/
│   │   ├── GameServerClient.cs       # 📡 Cliente WebSocket
│   │   └── ServerDataParser.cs       # 🔍 Parser de JSON
│   │
│   ├── World/
│   │   ├── ChunkManager.cs           # 🗺️ Gestión de chunks
│   │   ├── Chunk.cs (dentro)         # Estructura de datos
│   │   ├── NPC.cs                    # 🧟 Entidad NPC
│   │   └── NPCManager.cs             # 🧟‍♂️ Gestor de NPCs
│   │
│   ├── Player/
│   │   └── PlayerCharacter.cs        # 🎯 Lógica del jugador
│   │
│   └── UI/
│       ├── GameHUD.cs                # 📊 HUD principal
│       └── DebugConsole.cs           # 🐛 Consola de debug
│
├── Scenes/                            # Escenas (para futuro)
│
├── Assets/                            # ✨ Assets finales
│   ├── Player/                        # Sprites del jugador
│   └── Worlds/                        # Tilesets del mundo
│
├── Crudos/                            # 📦 Assets sin procesar
│   ├── 32rogues-0.5.0/
│   ├── Raven Fantasy - Pixel Art...
│   ├── Mana Seed...
│   └── Epic RPG World...
│
├── doc/                               # 📚 Documentación
│   ├── ARCHITECTURE.md                # Diagrama del servidor
│   ├── API.md                         # Especificación WebSocket
│   └── (client-example.js)
│
├── main.tscn                          # 🎬 Escena principal
├── main.cs                            # Script de Main
├── project.godot                      # Configuración Godot
├── SupaWorld.sln                      # Solución Visual Studio
├── SupaWorld.csproj                   # Proyecto C#
│
└── CLIENT_README.md                   # 📖 Este archivo
```

---

## 🔄 Flujo de Datos

### Inicialización
```
Game Start
    ↓
GameManager._Ready()
    ├─ GameLogger.Initialize()
    ├─ AssetManager.Initialize()
    ├─ GameServerClient.ConnectToServer()
    ├─ Crear PlayerCharacter
    ├─ Crear ChunkManager
    ├─ Crear NPCManager
    └─ RequestChunksUpdate()
```

### Loop de Juego
```
GameManager._Process()
    ├─ Actualizar posición cámara
    ├─ Solicitar chunks (cada 0.5s)
    │   ├─ GameServerClient.RequestChunks()
    │   └─ GameServerClient.RequestNearbyEntities()
    │
    └─ PlayerCharacter._Process()
        ├─ HandleInput() (flechas)
        ├─ MoveToward()
        │   ├─ Enviar al servidor
        │   └─ Mover localmente (feedback)
        └─ UpdatePosition() (interpolación)
```

### Recepción de Datos
```
Servidor envía mensaje WebSocket
    ↓
GameServerClient.ProcessSocket()
    ├─ Parsear JSON
    ├─ Emitir signal
    │   ├─ ChunksReceived → OnChunksReceived()
    │   │   └─ ServerDataParser.ParseChunks()
    │   │       └─ ChunkManager.LoadChunks()
    │   │
    │   └─ EntitiesReceived → OnEntitiesReceived()
    │       └─ ServerDataParser.ParseEntities()
    │           └─ NPCManager.UpdateNPCs()
    │
    └─ HUD actualizada
```

---

## 💻 Archivos Clave y Sus Responsabilidades

| Archivo | Responsabilidad | Líneas |
|---------|-----------------|--------|
| **GameManager.cs** | Orquestador principal, ciclo del juego | ~130 |
| **GameServerClient.cs** | Conexión WebSocket dual | ~170 |
| **ChunkManager.cs** | Carga/descarga de chunks | ~120 |
| **PlayerCharacter.cs** | Lógica y movimiento del jugador | ~140 |
| **NPCManager.cs** | Gestión de entidades NPC | ~110 |
| **GameConfig.cs** | Valores constantes globales | ~40 |
| **GameLogger.cs** | Sistema de logging a archivo | ~80 |
| **AssetManager.cs** | Precarga y caché de recursos | ~100 |
| **ServerDataParser.cs** | Parseo de respuestas JSON | ~100 |
| **GameHUD.cs** | Interfaz de usuario | ~60 |
| **DebugConsole.cs** | Consola in-game | ~140 |

**Total de código C#: ~1,190 líneas**

---

## 🎮 Cómo Usar

### 1. Editar Configuración
```csharp
// Scripts/GameConfig.cs
public const string SERVER_URL = "ws://localhost:8080";
public const bool DEBUG_MODE = true;
```

### 2. Ejecutar Juego
```
F5 en Godot Editor → Conecta automáticamente al servidor
```

### 3. Comandos Debug (si DEBUG_MODE = true)
```
~ → Abrir consola
help → Ver comandos disponibles
pos → Mostrar posición actual
tp 100 200 → Teletransportar a (100, 200)
clear → Limpiar consola
```

---

## 🔌 Integración con Servidor Go

### Conexiones Esperadas
```
Servidor (Go)
├─ :8080/ws/world  ← ChunksReceived, EntitiesReceived
└─ :8080/ws/player ← PlayerActionResponse
```

### Mensajes Esperados del Servidor

**chunks_loaded:**
```json
{
  "type": "response",
  "action": "chunks_loaded",
  "data": {
    "playerX": 256,
    "playerY": 384,
    "chunks": [
      {
        "chunkX": 0,
        "chunkY": 0,
        "tiles": [[0, 1, 2], [3, 4, 5], ...]
      }
    ]
  }
}
```

**entities_loaded:**
```json
{
  "type": "response",
  "action": "entities_loaded",
  "data": {
    "entities": [
      {
        "id": "npc_123",
        "type": "npc",
        "name": "Goblin",
        "level": 5,
        "x": 150,
        "y": 200
      }
    ]
  }
}
```

---

## 🛠️ Extending el Sistema

### Agregar un Nuevo Sistema
1. Crear carpeta en `Scripts/[SystemName]/`
2. Crear clase principal (ej: `NewSystem.cs`)
3. Agregar en `GameManager._Ready()`:
```csharp
var newSystem = new NewSystem { Name = "NewSystem" };
AddChild(newSystem);
```

### Agregar Comando de Debug
```csharp
// En DebugConsole.cs, switch de ExecuteCommand
case "mynewcmd":
    Log("Mi comando ejecutado");
    break;
```

### Cargar Assets
```csharp
// Usar AssetManager
Texture2D playerSprite = AssetManager.LoadTexture(
    AssetManager.GetPlayerAssetPath("mysprite.png")
);
```

---

## 📊 Estadísticas del Proyecto

- **Lenguaje:** C# (Godot 4.5)
- **Arquitectura:** Cliente-Servidor (WebSocket)
- **Patrón:** MVC simplificado
- **Líneas de Código:** ~1,190 (Scripts/)
- **Clases Principales:** 12
- **Sistemas Implementados:** 7

---

## 🚀 Próximas Tareas (Prioridad)

1. **Cargar tilesets reales** (sin esto, no hay visual)
2. **Sincronización completa** con servidor
3. **Sistema de combate básico**
4. **Animaciones de sprite**
5. **Menú principal**

---

**Última actualización:** 11 de Enero de 2026  
**Versión:** 0.1.0 (Fundamentos)  
**Estado:** 🟡 En Desarrollo Activo
