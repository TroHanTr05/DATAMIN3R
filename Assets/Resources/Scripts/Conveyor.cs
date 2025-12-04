using UnityEngine;

public class Conveyor : MonoBehaviour
{
    public PlaceableObject.ConveyorDirection direction = PlaceableObject.ConveyorDirection.Up;
    public GameObject carriedBlock;

    GridPlacementSystem grid;
    
    // --------------------
// ITEM MODE (normal game items)
// --------------------
    [Header("Item Conveyor")]
    public ItemStack carriedItem = null;
    
    void Start()
    {
        grid = GridPlacementSystem.Instance;
    }

    void Update()
    {
        MoveBlock();
    }

    void MoveBlock()
    {
        Vector2Int myCell = grid.WorldToCellPosition(transform.position);
        Vector2Int dir = Dir();
        Vector2Int behind = myCell - dir;
        Vector2Int ahead  = myCell + dir;

        // 1) If not carrying a block, try to pull one from behind
        if (carriedBlock == null)
        {
            GameObject b = grid.GetObjectAtCell(behind);
            if (b != null && b.GetComponent<Conveyor>() == null)
            {
                carriedBlock = b;
                grid.ClearCell(behind);
                carriedBlock.transform.position = transform.position;
            }
            else return;
        }

        // 2) Try to move carriedBlock forward
        GameObject aheadObj = grid.GetObjectAtCell(ahead);

        // If next space is empty → move block into that cell
        if (aheadObj == null)
        {
            grid.MovePlacedObject(carriedBlock, ahead);
            carriedBlock = null;
            return;
        }

        // If next is conveyor → hand block off if it’s free
        Conveyor next = aheadObj.GetComponent<Conveyor>();
        if (next != null && next.carriedBlock == null)
        {
            next.carriedBlock = carriedBlock;
            carriedBlock = null;
        }
    }

    Vector2Int Dir()
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
