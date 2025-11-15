using System.Collections.Generic;
using UnityEngine;

public class BuildInventory : MonoBehaviour
{
    [Header("Available Blocks")]
    [Tooltip("All block types the player can build (Factorio-style).")]
    public List<BuildBlock> blocks = new List<BuildBlock>();

    [Tooltip("Index of the currently selected block in the list above.")]
    public int selectedIndex = 0;

    // runtime amounts
    private int[] currentAmounts;

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

    private void Awake()
    {
        if (blocks == null)
            blocks = new List<BuildBlock>();

        currentAmounts = new int[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
        {
            currentAmounts[i] = blocks[i].startingAmount;
        }
    }

    // ====== QUERIES / CONSUMPTION ======

    public bool CanPlaceSelected(int needed = 1)
    {
        BuildBlock block = SelectedBlock;
        if (block == null) return false;

        int idx = Mathf.Clamp(selectedIndex, 0, blocks.Count - 1);
        int amt = currentAmounts[idx];

        if (amt < 0) return true; // infinite
        return amt >= needed;
    }

    public bool TryConsumeSelected(int amount)
    {
        BuildBlock block = SelectedBlock;
        if (block == null) return false;

        int idx = Mathf.Clamp(selectedIndex, 0, blocks.Count - 1);
        int amt = currentAmounts[idx];

        if (amt < 0) return true; // infinite inventory

        if (amt < amount) return false;

        currentAmounts[idx] -= amount;
        return true;
    }

    /// <summary>
    /// Add blocks by ID (used when refunding after deletion).
    /// </summary>
    public void AddToBlock(string id, int amount)
    {
        if (amount <= 0) return;

        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].id == id)
            {
                // infinite stock stays infinite
                if (currentAmounts[i] < 0)
                    return;

                currentAmounts[i] += amount;
                return;
            }
        }
    }

    // ====== SELECTION HELPERS (for hotbar / UI) ======

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
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].id == id)
            {
                selectedIndex = i;
                return;
            }
        }
    }
}
