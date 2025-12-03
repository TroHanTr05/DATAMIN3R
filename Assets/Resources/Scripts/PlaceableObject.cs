using System.Collections.Generic;
using UnityEngine;

public class PlaceableObject : MonoBehaviour
{
    // ========== LINK TO BLOCK DEFINITION ==========
    [Header("Block Definition")]
    [Tooltip("Which BuildBlock definition this instance represents (used for refunds, UI, etc).")]
    public BuildBlock blockDefinition;

    // ========== ID / CATEGORY ==========
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
    
    // Conveyor System
    public enum ConveyorDirection
    {
        Up,
        Down,
        Left,
        Right
    }
    
    [Header("Conveyor Settings")]
    public ConveyorDirection conveyorDirection;
    public float conveyorSpeed = 1f;  // tiles per second


    [Header("Identity")]
    [Tooltip("Human-readable name for debugging / overrides; usually comes from BuildBlock.")]
    public string displayNameOverride;

    [Tooltip("High-level category that can drive which options / UI are available.")]
    public PlaceableType type = PlaceableType.Generic;

    // ========== GRID FOOTPRINT ==========
    [Header("Grid Footprint")]
    [Tooltip("Size in grid cells (width = X, height = Y). 1x1 is a single tile.")]
    public Vector2Int sizeInCells = Vector2Int.one;

    [Tooltip(
        "Which cell in the footprint is considered the 'anchor' / clicked cell.\n" +
        "For example, (0,0) = bottom-left of the footprint, (1,0) on a 2x1 is the right cell."
    )]
    public Vector2Int anchorCell = Vector2Int.zero;

    // ========== FLAGS / BEHAVIOR HINTS ==========
    [Header("Behavior Flags")]
    [Tooltip("If true, this object blocks other placeables from using the same cells.")]
    public bool blocksPlacement = true;

    [Tooltip("If true, this object blocks movement/pathing (if you later add pathfinding).")]
    public bool blocksMovement = true;

    [Tooltip("If true, allow this object to overlap other placeables (for decals, pipes over belts, etc.).")]
    public bool canOverlapOtherPlaceables = false;

    [Tooltip("If true, this object can be rotated in 90� steps on the grid (not wired up yet).")]
    public bool canRotate = true;

    // ========== FOOTPRINT HELPERS (no rotation yet) ==========

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
    
    // Allows buildings to recieve items
    [Header("Item IO")]
    public ItemStack inputBuffer;   // holds 0 or 1 item
    public ItemStack outputBuffer;  // used by machines to output items
    
    public bool CanReceiveItem(Item item)
    {
        // Simple rule: can accept item if input buffer is empty
        return inputBuffer == null;
    }
    
    public void ReceiveItem(Item item)
    {
        inputBuffer = new ItemStack(item, 1);
    }
    
    public bool TryConsumeInput(out Item item)
    {
        if (inputBuffer != null)
        {
            item = inputBuffer.item;
            inputBuffer = null;
            return true;
        }
    
        item = null;
        return false;
    }

    // Conveyor transportation on mined materials    
    public void TryOutputToConveyor()
    {
        if (outputBuffer == null)
            return;
            
        Conveyor c = FindAdjacentConveyor();
        if (c != null && c.carriedItem == null)
        {
            c.carriedItem = outputBuffer;
            outputBuffer = null;
        }
    }
    
    private Conveyor FindAdjacentConveyor()
    {
        Vector2Int myCell = GridPlacementSystem.Instance.CellFromWorld(transform.position);

        // Check 4 directions
        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        foreach (var d in dirs)
        {
            Vector2Int c = myCell + d;
            if (!GridPlacementSystem.Instance.IsCellInsideGrid(c)) continue;

            GameObject obj = GridPlacementSystem.Instance.GetObjectAtCell(c);
            if (obj == null) continue;

            Conveyor conv = obj.GetComponent<Conveyor>();
            if (conv != null) return conv;
        }

        return null;
    }
    
}
