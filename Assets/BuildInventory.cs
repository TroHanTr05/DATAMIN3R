using System.Collections.Generic;
using UnityEngine;

public class BuildInventory : MonoBehaviour
{
    [Header("Block Catalog (Designer-only)")]
    [Tooltip("All block types the player can build (Factorio-style catalog).")]
    public List<BuildBlock> blocks = new List<BuildBlock>();

    [Tooltip("Index of the currently selected block in the list above.")]
    public int selectedIndex = 0;

    public int BlockCount => blocks != null ? blocks.Count : 0;

    public BuildBlock SelectedBlock
    {
        get
        {
            if (blocks == null || blocks.Count == 0)
                return null;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, blocks.Count - 1);
            return blocks[selectedIndex];
        }
    }

    public BuildBlock GetBlockById(string id)
    {
        if (blocks == null) return null;

        foreach (var b in blocks)
        {
            if (b != null && b.id == id)
                return b;
        }
        return null;
    }

    public void SelectNext()
    {
        if (blocks == null || blocks.Count == 0) return;
        selectedIndex = (selectedIndex + 1) % blocks.Count;
    }

    public void SelectPrevious()
    {
        if (blocks == null || blocks.Count == 0) return;
        selectedIndex--;
        if (selectedIndex < 0) selectedIndex = blocks.Count - 1;
    }

    public void SelectById(string id)
    {
        if (blocks == null) return;

        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null && blocks[i].id == id)
            {
                selectedIndex = i;
                return;
            }
        }
    }
}
