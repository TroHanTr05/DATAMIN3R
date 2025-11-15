using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public string id;
    public int amount;
}

public class PlayerInventory : MonoBehaviour
{
    [Header("Build Inventory")]
    [Tooltip("The player's build inventory (block types + counts).")]
    public BuildInventory buildInventory;

    [Header("General Item Inventory")]
    [Tooltip("Simple list of other items (iron plates, gears, etc.).")]
    public List<ItemStack> items = new List<ItemStack>();

    public void AddItem(string id, int amount)
    {
        if (amount <= 0) return;

        foreach (var stack in items)
        {
            if (stack.id == id)
            {
                stack.amount += amount;
                return;
            }
        }

        items.Add(new ItemStack { id = id, amount = amount });
    }

    public bool TryConsumeItem(string id, int amount)
    {
        if (amount <= 0) return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].id == id)
            {
                if (items[i].amount < amount)
                    return false;

                items[i].amount -= amount;
                if (items[i].amount <= 0)
                    items.RemoveAt(i);

                return true;
            }
        }

        return false;
    }
}
