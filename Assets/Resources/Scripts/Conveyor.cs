using UnityEngine;

public class Conveyor : MonoBehaviour
{
    public PlaceableObject.ConveyorDirection direction = PlaceableObject.ConveyorDirection.Up;

    [Header("Block Conveyor (debug)")]
    public GameObject carriedBlock;

    [Header("Item Conveyor")]
    public ItemStack carriedItem = null;

    private GridPlacementSystem grid;

    void Start()
    {
        grid = GridPlacementSystem.Instance;
        
        // Ensure the conveyor direction matches its actual rotation in the world.
        SyncDirectionWithRotation();
    }

    void Update()
    {
        MoveBlock();

        if (carriedBlock != null)
            PositionCarriedBlock();
    }

    private void SyncDirectionWithRotation()
    {
        float angle = transform.eulerAngles.z; // 0, 90, 180, 270

        // Normalize
        angle = Mathf.Round(angle / 90f) * 90f;
        angle = (angle + 360f) % 360f;

        switch (angle)
        {
            case 0: direction = PlaceableObject.ConveyorDirection.Up; break;
            case 90: direction = PlaceableObject.ConveyorDirection.Right; break;
            case 180: direction = PlaceableObject.ConveyorDirection.Down; break;
            case 270: direction = PlaceableObject.ConveyorDirection.Left; break;
        }
    }

    
    private void MoveBlock()
    {
        Vector2Int myCell = grid.WorldToCellPosition(transform.position);
        Vector2Int dir = Dir();
        Vector2Int behind = myCell - dir;
        Vector2Int ahead = myCell + dir;

        // 1) Pull a block from behind if empty
        if (carriedBlock == null)
        {
            if (grid.IsInsideGrid(behind))
            {
                GameObject b = grid.GetObjectAtCell(behind);
                if (b != null)
                {
                    // Do NOT pick up conveyors
                    if (b.GetComponent<Conveyor>() == null)
                    {
                        // Optional filter: do not pick up Storage buildings
                        var po = b.GetComponent<PlaceableObject>();
                        if (po == null || po.type != PlaceableObject.PlaceableType.Storage)
                        {
                            carriedBlock = b;
                            grid.ClearCell(behind);
                        }
                    }
                }
            }

            if (carriedBlock == null)
                return;
        }


        // 2) Try to push into the next cell
        if (!grid.IsInsideGrid(ahead))
            return;

        GameObject aheadObj = grid.GetObjectAtCell(ahead);

        // Case A: empty grid cell → drop block into grid
        if (aheadObj == null)
        {
            grid.PlaceObjectSingleCell(carriedBlock, ahead);
            carriedBlock = null;
            return;
        }

        // Case B: conveyor → pass block
        Conveyor next = aheadObj.GetComponent<Conveyor>();
        if (next != null)
        {
            if (next.carriedBlock == null)
            {
                next.carriedBlock = carriedBlock;
                carriedBlock = null;

                next.PositionCarriedBlock();
            }
            return; // <-- IMPORTANT CLEAN EXIT
        }
        
        // CASE C — Next is storage → store block
        Storage storage = aheadObj.GetComponent<Storage>();
        if (storage != null)
        {
            if (storage.HasSpace())
            {
                storage.AcceptBlock(carriedBlock);
                carriedBlock = null;   // remove from conveyor inventory
            }

            return; // whether stored or not, conveyor should stop here
        }

    }

    private void PositionCarriedBlock()
    {
        if (carriedBlock == null) return;

        // Offset to avoid being detected by grid
        Vector3 pos = transform.position;

        pos.y += 0.18f;   // Lift block visually above conveyor
        pos.z -= 0.25f;   // Ensure render order, avoid grid detection

        carriedBlock.transform.position = pos;
    }

    private Vector2Int Dir()
    {
        return direction switch
        {
            PlaceableObject.ConveyorDirection.Up => new Vector2Int(0, 1),
            PlaceableObject.ConveyorDirection.Down => new Vector2Int(0, -1),
            PlaceableObject.ConveyorDirection.Left => new Vector2Int(-1, 0),
            PlaceableObject.ConveyorDirection.Right => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
    }
}
