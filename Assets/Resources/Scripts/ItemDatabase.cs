using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    [Header("All item definitions in the game")]
    public List<Item> items = new List<Item>();

    private Dictionary<string, Item> lookup = new Dictionary<string, Item>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildLookup();
    }

    private void BuildLookup()
    {
        lookup.Clear();

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                continue;

            if (!lookup.ContainsKey(item.id))
                lookup.Add(item.id, item);
            else
                Debug.LogWarning($"Duplicate item ID detected: {item.id}");
        }
    }

    public static Item GetItem(string id)
    {
        if (Instance == null)
        {
            Debug.LogError("ItemDatabase is missing from the scene!");
            return null;
        }

        if (string.IsNullOrEmpty(id))
            return null;

        if (Instance.lookup.TryGetValue(id, out Item item))
            return item;

        Debug.LogWarning("Item not found: " + id);
        return null;
    }
}