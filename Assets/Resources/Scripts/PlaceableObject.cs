using System.Collections.Generic;
using UnityEngine;

public class PlaceableObject : MonoBehaviour
{
    [Header("Block Definition")]
    [Tooltip("Which BuildBlock definition this instance represents (used for refunds, UI, etc).")]
    public BuildBlock blockDefinition;

    public enum PlaceableType
    {
        Generic,
        Building,
        Conveyor,
        Miner,
        Crafter,
        Storage,
        Power,
        Decoration
    }
    
    public enum ConveyorDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    [Header("Identity")]
    [Tooltip("Human-readable name for debugging / overrides; usually comes from BuildBlock.")]
    public string displayNameOverride;

    [Tooltip("High-level category that can drive which options / UI are available.")]
    public PlaceableType type = PlaceableType.Generic;

    [Header("Breaking")]
    [Tooltip("Seconds of continuous erase input required to break/pick up this block.")]
    public float breakTimeSeconds = 0.25f;

    [Header("Grid Footprint")]
    [Tooltip("Size in grid cells (width = X, height = Y). 1x1 is a single tile.")]
    public Vector2Int sizeInCells = Vector2Int.one;

    [Tooltip(
        "Which cell in the footprint is considered the 'anchor' / clicked cell.\n" +
        "For example, (0,0) = bottom-left of the footprint, (1,0) on a 2x1 is the right cell."
    )]
    public Vector2Int anchorCell = Vector2Int.zero;

    [Header("Behavior Flags")]
    [Tooltip("If true, this object blocks other placeables from using the same cells.")]
    public bool blocksPlacement = true;

    [Tooltip("If true, this object blocks movement/pathing (if you later add pathfinding).")]
    public bool blocksMovement = true;

    [Tooltip("If true, allow this object to overlap other placeables (for decals, pipes over belts, etc.).")]
    public bool canOverlapOtherPlaceables = false;

    [Tooltip("If true, this object can be rotated in 90� steps on the grid (not wired up yet).")]
    public bool canRotate = true;

    [Header("Power")]
    [Tooltip("Net power per second this block contributes. Positive = generates, Negative = consumes.")]
    public float powerUsage = 0f;

    public IEnumerable<Vector2Int> GetLocalFootprint()
    {
        for (int x = 0; x < sizeInCells.x; x++)
        {
            for (int y = 0; y < sizeInCells.y; y++)
            {
                yield return new Vector2Int(x, y);
            }
        }
    }

    public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int anchorGridCell)
    {
        foreach (var local in GetLocalFootprint())
        {
            Vector2Int offset = local - anchorCell;
            yield return anchorGridCell + offset;
        }
    }
}
