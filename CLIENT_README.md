# 🎮 SupaWorld - Cliente Godot (C#)

## 📋 Descripción

Cliente roguelike top-down para **SupaWorld**, un juego multijugador en tiempo real.
Construido con **Godot 4.5** usando **C#** y comunicación **WebSocket**.

---

## 🏗️ Estructura del Proyecto

```
Scripts/
├── GameManager.cs          # Orquestador principal del juego
├── GameConfig.cs           # Configuración global
├── Network/
│   └── GameServerClient.cs # Cliente WebSocket con servidor
├── World/
│   └── ChunkManager.cs     # Gestión de chunks y renderizado
├── Player/
│   └── PlayerCharacter.cs  # Lógica del jugador
└── UI/
    └── GameHUD.cs         # HUD e información del jugador

Scenes/
└── (Escenas adicionales serán creadas aquí)

Assets/
├── Player/                 # Sprites del jugador
└── Worlds/                 # Tilesets y assets del mundo

Crudos/
└── (Assets sin procesar - referencias visuales)
```

---

## 🚀 Primeros Pasos

### 1. Requisitos
- **Godot 4.5+** con soporte C#
- **Servidor dedicado** en Go (en otra carpeta)
- Sistema de red **WebSocket**

### 2. Configuración

Edita `Scripts/GameConfig.cs` para ajustar:
```csharp
SERVER_URL = "ws://localhost:8080"  // Cambiar IP/puerto según necesario
```

### 3. Ejecución

```bash
# Desde Godot Editor
- Presiona F5 para ejecutar
- El juego debería conectarse al servidor
- Usa flechas para mover al jugador
```

---

## 📡 Sistema de Red

### Flujo de Conexión

```
Cliente ──WebSocket──> Servidor
  │                       │
  ├─ /ws/world    (chunks, mapas)
  └─ /ws/player   (acciones jugador)
```

### Mensajes Principales

**Solicitar Chunks:**
```csharp
_serverClient.RequestChunks(playerX, playerY);
```

**Movimiento del Jugador:**
```csharp
_serverClient.SendPlayerMovement(newX, newY);
```

---

## 🎮 Controles

| Tecla | Acción |
|-------|--------|
| ↑ ↓ ← → | Movimiento |
| (Próximamente) | Atacar |
| (Próximamente) | Usar objeto |

---

## 🛠️ Próximos Pasos

- [ ] Cargar tilesets reales desde `Assets/Worlds/`
- [ ] Renderizar NPCs en el mapa
- [ ] Sistema de inventario completo
- [ ] Animaciones de personaje
- [ ] Sistema de combate
- [ ] UI de menú principal
- [ ] Persistencia local
- [ ] Efectos de sonido y música

---

## 📝 Notas de Desarrollo

### Assets Disponibles
- **32rogues-0.5.0**: Sprites de personajes y enemigos (32x32px)
- **Raven Fantasy**: Tileset de bosque (16x16px)
- **Mana Seed**: Personajes base con múltiples variaciones
- **Epic RPG World**: Assets de aldea e interiores

### Arquitectura del Cliente

```
GameManager (orquestador)
    ├─ GameServerClient (red)
    ├─ ChunkManager (mundo visual)
    ├─ PlayerCharacter (jugador)
    └─ GameHUD (interfaz)
```

### Sistemas Implementados
✅ Conexión WebSocket  
✅ Gestión de chunks (infraestructura)  
✅ Movimiento del jugador (local)  
✅ HUD básico  
⏳ Renderizado de chunks  
⏳ Sincronización con servidor  
⏳ Sistema de entidades  

---

## 🐛 Debugging

Activa `DEBUG_MODE` en `GameConfig.cs` para ver logs adicionales:
```csharp
public const bool DEBUG_MODE = true;
```

---

## 📞 Contacto & Colaboración

Este proyecto es un trabajo en progreso. Mantén el código limpio y bien documentado.

---

**Última actualización:** 11/01/2026  
**Estado:** Early Development (v0.1.0)
