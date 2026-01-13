using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

/// <summary>
/// Parsea JSON de snapshots y eventos del servidor.
/// </summary>
public class SnapshotParser
{
    public Snapshot ParseSnapshot(string json)
    {
        var jsonParser = new Json();
        Error parseError = jsonParser.Parse(json);
        if (parseError != Error.Ok)
            throw new Exception($"JSON inválido: {parseError}");
        
        var data = (Godot.Collections.Dictionary)jsonParser.Data;
        var snapshot = new Snapshot();
        
        snapshot.Tick = (int)data["tick"];
        snapshot.MapWidth = data.ContainsKey("mapWidth") ? (int)data["mapWidth"] : 256;
        snapshot.MapHeight = data.ContainsKey("mapHeight") ? (int)data["mapHeight"] : 256;
        
        // Parsear tus unidades
        if (data.ContainsKey("units"))
        {
            var unitsList = (Godot.Collections.Array)data["units"];
            foreach (Godot.Collections.Dictionary unitData in unitsList)
            {
                snapshot.Units.Add(ParseUnit(unitData));
            }
        }
        
        // Parsear tus edificios
        if (data.ContainsKey("buildings"))
        {
            var buildingsList = (Godot.Collections.Array)data["buildings"];
            foreach (Godot.Collections.Dictionary buildingData in buildingsList)
            {
                snapshot.Buildings.Add(ParseBuilding(buildingData));
            }
        }
        
        // Parsear unidades enemigas en vista
        if (data.ContainsKey("enemyUnitsInView"))
        {
            var enemyUnitsList = (Godot.Collections.Array)data["enemyUnitsInView"];
            foreach (Godot.Collections.Dictionary unitData in enemyUnitsList)
            {
                snapshot.EnemyUnitsInView.Add(ParseUnit(unitData));
            }
        }
        
        // Parsear edificios enemigos en vista
        if (data.ContainsKey("enemyBuildingsInView"))
        {
            var enemyBuildingsList = (Godot.Collections.Array)data["enemyBuildingsInView"];
            foreach (Godot.Collections.Dictionary buildingData in enemyBuildingsList)
            {
                snapshot.EnemyBuildingsInView.Add(ParseBuilding(buildingData));
            }
        }
        
        // Parsear recursos
        if (data.ContainsKey("resources"))
        {
            var resourcesList = (Godot.Collections.Array)data["resources"];
            foreach (Godot.Collections.Dictionary resourceData in resourcesList)
            {
                snapshot.Resources.Add(ParseResource(resourceData));
            }
        }
        
        // Parsear eventos
        if (data.ContainsKey("events"))
        {
            var eventsList = (Godot.Collections.Array)data["events"];
            foreach (Godot.Collections.Dictionary eventData in eventsList)
            {
                snapshot.Events.Add(ParseEvent(eventData));
            }
        }
        
        // Parsear playerResources
        if (data.ContainsKey("playerResources"))
        {
            var playerResData = (Godot.Collections.Dictionary)data["playerResources"];
            snapshot.PlayerResources = ParsePlayerResources(playerResData);
        }
        
        // Parsear tiles visibles
        if (data.ContainsKey("visibleTiles"))
        {
            var tilesList = (Godot.Collections.Array)data["visibleTiles"];
            foreach (Godot.Collections.Dictionary tileData in tilesList)
            {
                snapshot.VisibleTiles.Add(new Tile
                {
                    X = (int)tileData["x"],
                    Y = (int)tileData["y"]
                });
            }
        }
        
        // Parsear tiles vistos
        if (data.ContainsKey("seenTiles"))
        {
            var tilesList = (Godot.Collections.Array)data["seenTiles"];
            foreach (Godot.Collections.Dictionary tileData in tilesList)
            {
                snapshot.SeenTiles.Add(new Tile
                {
                    X = (int)tileData["x"],
                    Y = (int)tileData["y"]
                });
            }
        }
        
        // Parsear colas de entrenamiento
        if (data.ContainsKey("trainingQueues"))
        {
            var queuesList = (Godot.Collections.Array)data["trainingQueues"];
            foreach (Godot.Collections.Dictionary queueData in queuesList)
            {
                var queue = new TrainingQueue
                {
                    BuildingId = (int)queueData["buildingId"],
                    BuildingType = (int)queueData["buildingType"],
                    CurrentTime = (int)queueData["currentTime"],
                    CurrentMax = (int)queueData["currentMax"]
                };
                
                // Parsear items (array de unitTypes)
                if (queueData.ContainsKey("items"))
                {
                    var itemsList = (Godot.Collections.Array)queueData["items"];
                    foreach (var item in itemsList)
                    {
                        queue.Items.Add((int)item);
                    }
                }
                
                snapshot.TrainingQueues.Add(queue);
            }
        }
        
        return snapshot;
    }
    
    private Unit ParseUnit(Godot.Collections.Dictionary data)
    {
        return new Unit
        {
            Id = (int)data["id"],
            Owner = (int)data["owner"],
            Type = (int)data["type"],
            X = (float)data["x"],
            Y = (float)data["y"],
            HP = (int)data["hp"],
            MaxHP = data.ContainsKey("maxHp") ? (int)data["maxHp"] : 100,
            Carrying = data.ContainsKey("carrying") ? (int)data["carrying"] : 0,
            CarryType = data.ContainsKey("carryType") ? (int)data["carryType"] : 0
        };
    }
    
    private Building ParseBuilding(Godot.Collections.Dictionary data)
    {
        return new Building
        {
            Id = (int)data["id"],
            Owner = (int)data["owner"],
            Type = (int)data["type"],
            X = (float)data["x"],
            Y = (float)data["y"],
            State = (int)data["state"],
            Progress = (int)data["progress"],
            HP = data.ContainsKey("hp") ? (int)data["hp"] : 200,
            MaxHp = data.ContainsKey("maxHp") ? (int)data["maxHp"] : 200
        };
    }
    
    private Resource ParseResource(Godot.Collections.Dictionary data)
    {
        return new Resource
        {
            Id = (int)data["id"],
            Type = (int)data["type"],
            Amount = (int)data["amount"],
            X = (float)data["x"],
            Y = (float)data["y"]
        };
    }
    
    private GameEvent ParseEvent(Godot.Collections.Dictionary data)
    {
        var evt = new GameEvent
        {
            Type = (int)data["type"],
            Tick = (int)data["tick"],
            EntityId = (int)data["entityId"],
            Data = new System.Collections.Generic.Dictionary<string, object>()
        };
        
        if (data.ContainsKey("data"))
        {
            var eventData = (Godot.Collections.Dictionary)data["data"];
            if (eventData != null)
            {
                foreach (var key in eventData.Keys)
                {
                    evt.Data[key.ToString()] = eventData[key];
                }
            }
        }
        
        return evt;
    }
    
    private PlayerResources ParsePlayerResources(Godot.Collections.Dictionary data)
    {
        return new PlayerResources
        {
            Food = (int)data["food"],
            Gold = (int)data["gold"],
            Stone = (int)data["stone"],
            Wood = (int)data["wood"],
            Pop = (int)data["pop"],
            PopCap = (int)data["popCap"]
        };
    }
}
