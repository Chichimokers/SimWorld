# SimWorld RTS Client - Control Guide

## Overview
Complete RTS client implementation with 6 action modes and full mouse/keyboard control system.

## Mouse Controls
- **Left Click**: Select unit (no action mode) OR execute action (in action mode)
- **Right Click**: Move selected unit OR cancel current action mode
- **ESC Key**: Cancel current action mode

## Action Modes - Keyboard Activation

| Key | Action | Description | Target |
|-----|--------|-------------|--------|
| **A** | Attack | Select enemy to attack | Enemy Unit |
| **G** | Gather | Collect resources | Resource Tile |
| **H** | Hunt | Hunt animals | Animal (Type 2) |
| **B** | Build | Place building (press 1 or 2, then click) | Terrain |
| **D** | Deposit | Return gathered resources | TownCenter (Type 0) |
| **T** | Train | Train new units | Barracks (Type 1) |

## Building Selection (Build Mode Only)
- **1**: House (Building Type 2)
- **2**: Barracks (Building Type 1)

## Typical Gameplay Flow

### Gathering Resources
1. Select a Villager (Left Click on unit)
2. Press **G** for Gather mode
3. Left Click on a resource tile to gather
4. Press **D** for Deposit mode when carrying resources
5. Left Click on TownCenter to deposit

### Training Units
1. Select TownCenter or click to select Barracks
2. Press **T** for Train mode
3. Left Click on a Barracks to train Militia (Type 1)

### Attacking
1. Select a Militia unit (Left Click)
2. Press **A** for Attack mode
3. Left Click on enemy unit to attack

### Building
1. Select a Villager
2. Press **B** for Build mode
3. Press **1** for House or **2** for Barracks
4. Left Click on terrain to place building

### Hunting
1. Select a Villager
2. Press **H** for Hunt mode
3. Left Click on animal to hunt

## HUD Information Display

**Top Left Corner:**
- Control instructions
- List of available actions
- Current action mode status (when active)

**Top Right Corner:**
- Resource counters: Food, Wood, Gold, Stone
- Population: Current/Cap

**Selected Unit Display:**
- Unit ID
- Unit Type (Villager, Militia, Animal)
- Current HP

## Unit Types
- **0**: Villager (worker)
- **1**: Militia (fighter)
- **2**: Animal (huntable)

## Building Types
- **0**: Town Center (resource dropoff, training)
- **1**: Barracks (military training)
- **2**: House (population capacity)

## Action Mode Indicators
When an action mode is active, the HUD displays:
- The current mode in **orange text**
- Instructions for what to click
- For build mode: which building type is selected

## Tips
- All actions require a selected unit (except where noted)
- Right-click cancels action mode and allows free movement
- The HUD shows all available actions at all times
- Each action mode automatically deactivates after execution
- No unit selected? Left-click anywhere to deselect

---
*This is a complete, playable RTS client implementation with all 6 available actions fully integrated.*
