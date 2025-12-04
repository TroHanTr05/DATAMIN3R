using System.Collections.Generic;
using UnityEngine;

public class PlaceableObject : MonoBehaviour
{
    // ----------------------------------------------------------
    // EXISTING FIELDS (UNCHANGED)
    // ----------------------------------------------------------

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

    [Tooltip("If true, this object can be rotated in 90° steps on the grid (not wired up yet).")]
    public bool canRotate = true;

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

    // ----------------------------------------------------------
    // NEW SECTION #1 — CONVEYOR SETTINGS
    // (Required for conveyor logic, safe for all other blocks)
    // ----------------------------------------------------------

    [Header("Conveyor Settings")]
    public ConveyorDirection conveyorDirection = ConveyorDirection.Right;
    public float conveyorSpeed = 1f;

    public enum ConveyorDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    // ----------------------------------------------------------
    // NEW SECTION #2 — ITEM IO BUFFERS (for miners / machines)
    // Does not affect conveyors unless explicitly used.
    // ----------------------------------------------------------

    [Header("Item IO Buffers")]
    public ItemStack inputBuffer;
    public ItemStack outputBuffer;

    // ----------------------------------------------------------
    // NEW SECTION #3 — OPTIONAL ITEM RENDERER (not required)
    // Conveyor.cs uses its own child sprite ("ItemSprite")
    // This simply provides a place to store a reference if needed.
    // ----------------------------------------------------------

    [Header("Visuals (Optional)")]
    public SpriteRenderer itemRenderer;   // safe optional reference
    
    public void TryOutputToConveyor()
    {
        if (outputBuffer == null)
            return;

        // Get my grid cell
        Vector2Int myCell = GridPlacementSystem.Instance.WorldToCellPosition(transform.position);

        // Check 4 directions around me
        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        foreach (var d in dirs)
        {
            Vector2Int neighbor = myCell + d;

            GameObject obj = GridPlacementSystem.Instance.GetPlacedObject(neighbor);
            if (obj == null) continue;

            Conveyor conv = obj.GetComponent<Conveyor>();
            if (conv == null) continue;

            // Only output if conveyor is empty
            if (conv.carriedItem == null)
            {
                conv.carriedItem = outputBuffer;
                outputBuffer = null;
                return;
            }
        }
    }

    
}
