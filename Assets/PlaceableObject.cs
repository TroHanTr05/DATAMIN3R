using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on every prefab that can be placed on the grid.
/// Controls its type/category and how many grid cells it occupies.
/// </summary>
public class PlaceableObject : MonoBehaviour
{
    // ========== ID / CATEGORY ==========
    public enum PlaceableType
    {
        Generic,
        Building,
        Conveyor,
        Miner,
        Storage,
        Power,
        Decoration
    }

    [Header("Identity")]
    [Tooltip("Unique ID for this placeable type (used with BuildInventory).")]
    public string id;

    [Tooltip("Human-readable name shown in UI, build menu, etc.")]
    public string displayName = "Placeable";

    [Tooltip("High-level category that can drive which options / UI are available.")]
    public PlaceableType type = PlaceableType.Generic;

    // ========== GRID FOOTPRINT ==========
    [Header("Grid Footprint")]
    [Tooltip("Size in grid cells (width = X, height = Y). 1x1 is a single tile.")]
    public Vector2Int sizeInCells = Vector2Int.one;

    [Tooltip(
        "Which cell in the footprint is considered the 'anchor' / clicked cell.\n" +
        "For example, (0,0) = bottom-left of the footprint, (1,0) on a 2x1 is the right cell.\n" +
        "The GridPlacementSystem uses this to align multi-cell objects correctly."
    )]
    public Vector2Int anchorCell = Vector2Int.zero;

    // ========== FLAGS / BEHAVIOR HINTS ==========
    [Header("Behavior Flags")]
    [Tooltip("If true, this object blocks other placeables from using the same cells.")]
    public bool blocksPlacement = true;

    [Tooltip("If true, this object blocks movement/pathing (if you later add pathfinding).")]
    public bool blocksMovement = true;

    [Tooltip("If true, allow this object to overlap other placeables (for purely visual stuff, decals, etc.).")]
    public bool canOverlapOtherPlaceables = false;

    [Tooltip("If true, this object can be rotated in 90° steps on the grid (not used yet).")]
    public bool canRotate = true;

    // ========== HELPER METHODS (for the grid system) ==========

    /// <summary>
    /// Returns all local footprint cells (relative to footprint origin at 0,0).
    /// </summary>
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

    /// <summary>
    /// Returns the world-grid cells this object would occupy if its anchor
    /// were placed at 'anchorGridCell'. No rotation support yet.
    /// </summary>
    public IEnumerable<Vector2Int> GetOccupiedCells(Vector2Int anchorGridCell)
    {
        foreach (var local in GetLocalFootprint())
        {
            // Shift so that 'anchorCell' in the footprint maps to the grid cell you clicked on.
            Vector2Int offset = local - anchorCell;
            yield return anchorGridCell + offset;
        }
    }
}
