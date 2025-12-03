using UnityEngine;

[CreateAssetMenu(menuName = "Build/Build Block", fileName = "NewBuildBlock")]
public class BuildBlock : ScriptableObject
{
    // ***Added for Power Manager***
    [Header("Power Settings")]
    public float powerDrain = 0f;      // negative = consumes power
    public float powerGeneration = 0f; // positive = creates power
    
    [Header("Identity")]
    [Tooltip("Unique ID string used for save/load & lookups.")]
    public string id = "block_id";

    [Tooltip("Name shown in UI / tooltips.")]
    public string displayName = "New Block";

    [TextArea]
    [Tooltip("Optional description for UI.")]
    public string description;

    [Tooltip("Icon used in hotbar / inventory UI.")]
    public Sprite icon;

    [Header("Prefabs")]
    [Tooltip("Prefab actually placed into the world/grid.")]
    public GameObject placeablePrefab;

    [Tooltip("Optional ghost / preview prefab. If null, GridPlacementSystem can fall back to placeablePrefab.")]
    public GameObject previewPrefab;

    [Header("Category / Type (for behavior)")]
    public PlaceableObject.PlaceableType category = PlaceableObject.PlaceableType.Generic;

    [Header("Behavior Settings")]
    [Tooltip("How often the block processes its logic, in seconds.")]
    public float tickRate = 1f;

    [Tooltip("Optional: What item/resource this block produces per cycle (if applicable).")]
    public string outputItemId;

    [Tooltip("Optional: If this block consumes an item to operate, define it here.")]
    public string inputItemId;

    [Tooltip("Optional: Number of input items required per tick.")]
    public int inputAmount = 1;

    [Tooltip("Optional: Number of output items produced per tick.")]
    public int outputAmount = 1;
    public virtual void ProcessTick(PlaceableObject instance)
    {
        switch (category)
        {
            case PlaceableObject.PlaceableType.Miner:
                // Example: Miner produces resources
                // Debug.Log($"{displayName} mined {outputAmount} of {outputItemId}"); (moving to functionality)
                var po = instance;
                
                if (po.outputBuffer == null)
                {
                    // create 1 item
                    Item minedItem = ItemDatabase.GetItem(outputItemId);
                    po.outputBuffer = new ItemStack(minedItem, 1);
                }
                po.TryOutputToConveyor();
                break;

            case PlaceableObject.PlaceableType.Crafter:
                // Example: Crafter consumes input and produces output
                Debug.Log($"{displayName} crafted {outputAmount} {outputItemId} from {inputAmount} {inputItemId}");
                break;

            case PlaceableObject.PlaceableType.Conveyor:
                // Conveyors would transfer items to adjacent blocks (simplified placeholder)
                Debug.Log($"{displayName} moved items forward.");
                break;

            default:
                Debug.Log($"{displayName} ticked with no special behavior.");
                break;
        }
    }
}