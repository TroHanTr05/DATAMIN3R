using UnityEngine;

public class Conveyor : MonoBehaviour
{
    public PlaceableObject po;
    public ItemStack carriedItem;  // 0 or 1 item on belt
    public SpriteRenderer itemRenderer; // Sprite for what's on Conveyors

    private float moveProgress = 0f; // 0 → 1 tile progress

    private void Awake()
    {
        po = GetComponent<PlaceableObject>();
    }

    private void Update()
    {
        ProcessConveyor();
    }

    private void ProcessConveyor()
    {
        if (carriedItem == null)
            return;

        moveProgress += po.conveyorSpeed * Time.deltaTime;

        // When progress >= 1 tile, attempt to move to neighbor
        if (moveProgress >= 1f)
        {
            if (TryTransferToNext())
            {
                carriedItem = null;
                moveProgress = 0f;
            }
        }
    }

    private bool TryTransferToNext()
    {
        Vector2Int myCell = GridPlacementSystem.Instance.WorldToCellPosition(transform.position);
        Vector2Int next = GetNextCell(myCell);

        if (!GridPlacementSystem.Instance.IsCellInsideGrid(next))
            return false;

        GameObject neighbor = GridPlacementSystem.Instance.GetObjectAtCell(next);

        if (neighbor == null)
            return false;

        var neighborPO = neighbor.GetComponent<PlaceableObject>();

        if (neighborPO == null)
            return false;

        // If next is conveyor → hand off item
        if (neighborPO.type == PlaceableObject.PlaceableType.Conveyor)
        {
            Conveyor nextConv = neighbor.GetComponent<Conveyor>();
            if (nextConv != null && nextConv.carriedItem == null)
            {
                nextConv.carriedItem = carriedItem;
                return true;
            }
        }

        // If next is machine / chest → try delivering
        if (neighborPO.CanReceiveItem(carriedItem.item))
        {
            neighborPO.ReceiveItem(carriedItem.item);
            return true;
        }

        return false;
    }

    private Vector2Int GetNextCell(Vector2Int cell)
    {
        switch (po.conveyorDirection)
        {
            case PlaceableObject.ConveyorDirection.Up:
                return cell + new Vector2Int(0, +1);
            case PlaceableObject.ConveyorDirection.Down:
                return cell + new Vector2Int(0, -1);
            case PlaceableObject.ConveyorDirection.Left:
                return cell + new Vector2Int(-1, 0);
            case PlaceableObject.ConveyorDirection.Right:
                return cell + new Vector2Int(+1, 0);
        }
        return cell;
    }

    
    private void LateUpdate()
    {
        if (carriedItem != null)
        {
            itemRenderer.sprite = carriedItem.item.icon;
            itemRenderer.enabled = true;
        }
        else
        {
            itemRenderer.enabled = false;
        }
    }
}
