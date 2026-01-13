# Referencia de Entidades - Tipos y IDs

## 🎮 Tipos de Unidades (UnitType)

| ID | Nombre | Descripción | HP | Velocidad | Visión | Ataque | Rango |
|----|--------|-------------|----|-----------|----|--------|-------|
| 0 | **Villager** (Aldeano) | Recolecta recursos, construye edificios | 40 | 0.1 | 6 | 5 | 1.5 |
| 1 | **Militia** (Milicia) | Unidad militar básica | 60 | 0.15 | 6 | 10 | 1.5 |
| 2 | **Animal** | Neutral, puede ser cazado por comida | 30 | 0.05 | 0 | 0 | 0 |

### Capacidades por Unidad
- **Villager (0)**: 
  - Puede recolectar recursos (`gather`)
  - Puede construir edificios (`build`)
  - Puede depositar recursos (`deposit`)
  - Puede cazar animales (`hunt`)
  - Capacidad de carga: variable (configurable en cliente)
  
- **Militia (1)**:
  - Puede atacar (`attack`)
  - Puede moverse (`move`)
  - NO puede recolectar ni construir

- **Animal (2)**:
  - Neutral (owner=0)
  - Se mueve aleatoriamente (roaming)
  - Puede ser cazado para obtener comida

---

## 🏗️ Tipos de Edificios (BuildingType)

| ID | Nombre | Descripción | HP Max | Tamaño | Costo | Training |
|----|--------|-------------|--------|--------|-------|----------|
| 0 | **TownCenter** | Centro urbano, entrena aldeanos | 2000 | 4x4 | - | Sí |
| 1 | **Barracks** | Cuartel, entrena milicia | 1000 | 3x3 | 100 wood, 50 stone | Sí |
| 2 | **House** | Casa, aumenta límite de población (+5) | 500 | 2x2 | 30 wood | No |

### Estados de Edificio (BuildingState)
- **0 = BuildingConstructing** (En construcción)
- **1 = BuildingCompleted** (Completado)

### Lógica de Construcción
Cuando un aldeano ejecuta el comando `build`:
1. Se verifica que el aldeano esté a distancia ≤2.0 del sitio
2. Se deduce el costo de recursos del jugador
3. Se crea el edificio con:
   - `state: 0` (BuildingConstructing)
   - `progress: 0`
   - `constructionMax: 100`
   
**Cada tick del servidor (50ms):**
- Si `state == BuildingConstructing`, se incrementa `progress++`
- Cuando `progress >= constructionMax` (100 ticks), el edificio se completa:
  - `state = 1` (BuildingCompleted)
  - Si es House, se añade +5 al `popCap` del jugador

**Tiempo de construcción:**
- 100 ticks × 50ms = **5 segundos** para completar cualquier edificio

**Nota importante:**
- El aldeano NO necesita quedarse cerca para que la construcción avance
- La construcción es automática una vez iniciada
- Si quieres que el aldeano deba "construir activamente", debes modificar la lógica en `game/types.go`

---

## 🌲 Tipos de Recursos (ResourceType)

| ID | Nombre | Descripción | Cantidad Inicial |
|----|--------|-------------|------------------|
| 0 | **Wood** (Madera) | Árboles, recolectables | 200 por árbol |
| 1 | **Gold** (Oro) | Minas de oro | Variable |
| 2 | **Stone** (Piedra) | Canteras de piedra | Variable |
| 3 | **Food** (Comida) | Animales, granjas | Variable |

### Recolección de Recursos
- Cada comando `gather` recolecta **10 unidades** del recurso
- Los recursos se acumulan en el campo `carrying` del aldeano
- Cuando el aldeano tiene recursos, debe usar `deposit` en un TownCenter para transferirlos al jugador

---

## 📊 Ejemplo de Snapshot con Tipos Identificados

```json
{
  "type": "snapshot",
  "tick": 1234,
  "units": [
    {
      "id": 101,
      "owner": 1,
      "type": 0,        // Villager (Aldeano)
      "x": 12.0,
      "y": 8.0,
      "hp": 40,
      "carrying": 20,
      "carryType": 0    // Wood (Madera)
    },
    {
      "id": 102,
      "owner": 1,
      "type": 1,        // Militia (Milicia)
      "x": 15.0,
      "y": 9.0,
      "hp": 60
    }
  ],
  "buildings": [
    {
      "id": 201,
      "owner": 1,
      "type": 0,        // TownCenter
      "x": 10.0,
      "y": 10.0,
      "state": 1,       // Completado
      "progress": 100,
      "hp": 2000,
      "maxHp": 2000
    },
    {
      "id": 202,
      "owner": 1,
      "type": 2,        // House
      "x": 5.0,
      "y": 5.0,
      "state": 0,       // En construcción
      "progress": 45,   // 45% completado (45 ticks de 100)
      "hp": 500,
      "maxHp": 500
    }
  ],
  "resources": [
    {
      "id": 301,
      "type": 0,        // Wood (Árbol)
      "amount": 180,    // 180 madera restante
      "x": 15.5,
      "y": 7.5
    },
    {
      "id": 302,
      "type": 1,        // Gold (Mina de oro)
      "amount": 300,
      "x": 18.5,
      "y": 12.5
    }
  ],
  "enemyUnitsInView": [
    {
      "id": 201,
      "owner": 2,
      "type": 1,        // Militia enemiga
      "x": 20.0,
      "y": 10.0,
      "hp": 60
    }
  ],
  "enemyBuildingsInView": [
    {
      "id": 401,
      "owner": 2,
      "type": 1,        // Barracks enemigo
      "x": 25.0,
      "y": 15.0,
      "state": 1,       // Completado
      "progress": 100,
      "hp": 1000,
      "maxHp": 1000
    }
  ],
  "events": [
    {
      "type": 6,        // ResourceUpdate (recolección)
      "tick": 1234,
      "entityId": 301,
      "data": {
        "gathered": 10  // 10 unidades de madera recolectadas
      }
    },
    {
      "type": 2,        // EntityDestroy (destrucción)
      "tick": 1230,
      "entityId": 199,
      "data": {
        "destroyed": true,
        "killer": 201   // Quién lo destruyó
      }
    },
    {
      "type": 5,        // QueueProgress (progreso de entrenamiento)
      "tick": 1234,
      "entityId": 201,
      "data": {
        "progress": 50,
        "unitType": 1   // Militia en entrenamiento
      }
    }
  ],
  "playerResources": {
    "food": 100,
    "gold": 50,
    "wood": 200,
    "stone": 0,
    "pop": 3,         // 3 unidades creadas
    "popCap": 15      // Límite: 10 base + 5 (1 House)
  },
  "visibleTiles": [
    { "x": 10, "y": 10 },
    { "x": 10, "y": 11 },
    { "x": 11, "y": 10 },
    { "x": 11, "y": 11 }
  ],
  "seenTiles": [
    { "x": 5, "y": 5 },
    { "x": 5, "y": 6 },
    { "x": 6, "y": 5 }
  ],
  "mapWidth": 256,
  "mapHeight": 256,
  "trainingQueues": [
    {
      "buildingId": 201,
      "buildingType": 0,     // TownCenter
      "items": [0, 0],       // 2 Villagers en cola
      "currentTime": 50,     // 50 ticks transcurridos
      "currentMax": 100      // 100 ticks para completar
    }
  ]
}
```

---

## 🔨 Flujo Completo de Construcción

### Paso 1: Aldeano inicia construcción
```json
{
  "type": "build",
  "unitId": 101,
  "buildingType": 2,    // House
  "x": 5,
  "y": 5
}
```

### Paso 2: Servidor valida y crea edificio
- Verifica que el aldeano esté cerca (dist ≤ 2.0)
- Verifica recursos: House cuesta 30 wood
- Deduce recursos del jugador
- Crea edificio con `state: 0`, `progress: 0`, `constructionMax: 100`

### Paso 3: Construcción automática (cada tick)
```
Tick 100: progress = 0/100  (0%)
Tick 101: progress = 1/100  (1%)
Tick 102: progress = 2/100  (2%)
...
Tick 199: progress = 99/100 (99%)
Tick 200: progress = 100/100 → state = 1 (Completado!)
```

### Paso 4: Edificio completado
- El edificio cambia a `state: 1`
- Si es House, se añade +5 a `popCap`
- El edificio puede usarse (entrenar unidades si tiene queue)

**Tiempo real:** 100 ticks × 50ms/tick = **5 segundos**

---

## 💡 Consejos para el Cliente

### Renderizado de Unidades por Tipo
```javascript
function drawUnit(ctx, unit) {
  let color, sprite;
  
  switch(unit.type) {
    case 0: // Villager
      color = unit.owner === myPlayerId ? 'blue' : 'red';
      sprite = unit.carrying > 0 ? 'villager_carrying' : 'villager';
      break;
    case 1: // Militia
      color = unit.owner === myPlayerId ? 'green' : 'red';
      sprite = 'militia';
      break;
    case 2: // Animal
      color = 'gray';
      sprite = 'animal';
      break;
  }
  
  drawSprite(ctx, sprite, unit.x, unit.y, color);
  drawHealthBar(ctx, unit.x, unit.y, unit.hp);
}
```

### Renderizado de Edificios con Progreso
```javascript
function drawBuilding(ctx, building) {
  let sprite;
  
  switch(building.type) {
    case 0: sprite = 'town_center'; break;
    case 1: sprite = 'barracks'; break;
    case 2: sprite = 'house'; break;
  }
  
  if (building.state === 0) {
    // En construcción: mostrar progreso
    drawConstructionSite(ctx, building.x, building.y, sprite);
    drawProgressBar(ctx, building.x, building.y, building.progress / 100);
  } else {
    // Completado
    drawSprite(ctx, sprite, building.x, building.y);
  }
  
  drawHealthBar(ctx, building.x, building.y, building.hp / building.maxHp);
}
```

### Mostrar Recursos del Aldeano
```javascript
function showVillagerInfo(villager) {
  if (villager.carrying > 0) {
    const resourceName = ['Wood', 'Gold', 'Stone', 'Food'][villager.carryType];
    return `${resourceName}: ${villager.carrying}`;
  }
  return 'Idle';
}
```

---

## 📝 Códigos de Eventos

| Tipo | Nombre | Descripción |
|------|--------|-------------|
| 0 | EntityCreate | Entidad creada o visible por primera vez |
| 1 | EntityUpdate | Entidad actualizada (movimiento, HP, etc.) |
| 2 | EntityDestroy | Entidad destruida |
| 3 | EntityHidden | Entidad oculta (fuera de LoS) |
| 4 | FogUpdate | Actualización de niebla de guerra |
| 5 | QueueProgress | Progreso en cola de entrenamiento |
| 6 | ResourceUpdate | Recurso actualizado (recolección) |
| 999 | Overflow | Demasiados eventos en un tick |

### Ejemplos de Eventos

**Evento de Recolección (type 6):**
```json
{
  "type": 6,
  "tick": 1234,
  "entityId": 301,
  "data": {
    "gathered": 10,
    "resourceType": 0
  }
}
```

**Evento de Destrucción (type 2):**
```json
{
  "type": 2,
  "tick": 1230,
  "entityId": 199,
  "data": {
    "destroyed": true,
    "killer": 201
  }
}
```

**Evento de Entidad Creada (type 0):**
```json
{
  "type": 0,
  "tick": 1235,
  "entityId": 105,
  "data": {
    "owner": 1,
    "unitType": 0,
    "x": 12.0,
    "y": 8.0
  }
}
```

**Evento de Progreso de Cola (type 5):**
```json
{
  "type": 5,
  "tick": 1234,
  "entityId": 201,
  "data": {
    "progress": 50,
    "unitType": 1,
    "buildingId": 201
  }
}
```

**Evento de Overflow (type 999):**
```json
{
  "type": 999,
  "tick": 1234,
  "entityId": 0,
  "data": {
    "overflow": true,
    "dropped": 150
  }
}
```

---

## 🎯 Resumen Rápido

**Unidades:**
- 0 = Villager (aldeano)
- 1 = Militia (soldado)
- 2 = Animal (neutral)

**Edificios:**
- 0 = TownCenter (4x4, 2000 HP)
- 1 = Barracks (3x3, 1000 HP)
- 2 = House (2x2, 500 HP)

**Recursos:**
- 0 = Wood (madera)
- 1 = Gold (oro)
- 2 = Stone (piedra)
- 3 = Food (comida)

**Construcción:**
- 100 ticks para completar (5 segundos a 50ms/tick)
- Automática una vez iniciada
- No requiere que el aldeano permanezca cerca
