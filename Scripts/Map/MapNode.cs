using Godot;
using System.Collections.Generic;

namespace OdysseyCards.Map;

public enum NodeOwner
{
    None,
    Player,
    Enemy
}

public class MapNode
{
    public int Id { get; set; }
    public Vector2I GridPosition { get; set; }
    public Vector2 WorldPosition { get; set; }
    public List<int> NeighborIds { get; set; } = new List<int>();
    public NodeOwner Owner { get; set; } = NodeOwner.None;
    public int DistanceToPlayerHQ { get; set; } = -1;
    public int DistanceToEnemyHQ { get; set; } = -1;
    public bool IsDeploymentPoint { get; set; } = false;
    public bool IsPlayerDeploymentPoint { get; set; } = false;
    public bool IsEnemyDeploymentPoint { get; set; } = false;

    public MapNode(int id, Vector2I gridPosition)
    {
        Id = id;
        GridPosition = gridPosition;
    }

    public MapNode(int id, Vector2I gridPosition, Vector2 worldPosition)
    {
        Id = id;
        GridPosition = gridPosition;
        WorldPosition = worldPosition;
    }

    public void AddNeighbor(int neighborId)
    {
        if (!NeighborIds.Contains(neighborId))
        {
            NeighborIds.Add(neighborId);
        }
    }

    public void RemoveNeighbor(int neighborId)
    {
        NeighborIds.Remove(neighborId);
    }

    public bool HasNeighbor(int neighborId)
    {
        return NeighborIds.Contains(neighborId);
    }

    public int GetDistanceTo(NodeOwner owner)
    {
        return owner == NodeOwner.Player ? DistanceToPlayerHQ : DistanceToEnemyHQ;
    }
}
