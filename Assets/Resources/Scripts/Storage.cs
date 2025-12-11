using System.Collections.Generic;
using UnityEngine;

public class Storage : MonoBehaviour
{
    [Header("Storage Settings")]
    public int capacity = 10;  // editable in inspector

    [Header("Debug View (read only)")]
    public List<GameObject> storedBlocks = new List<GameObject>();

    private GridPlacementSystem grid;

    void Start()
    {
        grid = GridPlacementSystem.Instance;
    }

    /// <summary>
    /// Returns true if storage can accept a block.
    /// </summary>
    public bool HasSpace()
    {
        return storedBlocks.Count < capacity;
    }

    /// <summary>
    /// Accept a block from a conveyor.
    /// Removes it from the world and stores it internally.
    /// </summary>
    public void AcceptBlock(GameObject block)
    {
        if (block == null) return;

        // 1. CLEAR THE STORAGE CELL, NOT THE BLOCK CELL
        Vector2Int storageCell = grid.WorldToCellPosition(transform.position);
        grid.ClearCell(storageCell);

        // 2. STRIP GAMEPLAY COMPONENTS
        var placeable = block.GetComponent<PlaceableObject>();
        if (placeable != null)
            Destroy(placeable);

        var collider = block.GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        var rb = block.GetComponent<Rigidbody2D>();
        if (rb != null)
            Destroy(rb);

        // 3. HIDE THE BLOCK AS STORED
        block.SetActive(false);

        storedBlocks.Add(block);

        // 4. MARK THE STORAGE CELL AS OCCUPIED BY THE STORAGE ITSELF
        grid.SetObjectAtCell(storageCell, this.gameObject);
    }



    /// <summary>
    /// Optional: Retrieve a stored block (if you want output later)
    /// </summary>
    public GameObject PopBlock()
    {
        if (storedBlocks.Count == 0)
            return null;

        GameObject b = storedBlocks[0];
        storedBlocks.RemoveAt(0);
        b.SetActive(true);
        return b;
    }
}