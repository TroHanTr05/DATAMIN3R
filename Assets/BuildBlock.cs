using UnityEngine;

[System.Serializable]
public class BuildBlock
{
    [Header("Identity")]
    [Tooltip("Unique ID for this block type (used by code / save system).")]
    public string id;

    [Tooltip("Human readable name for UI / tooltips.")]
    public string displayName = "Block";

    [Header("Prefabs")]
    [Tooltip("Prefab that will be spawned into the world (must have PlaceableObject on it).")]
    public PlaceableObject placeablePrefab;

    [Tooltip("Optional preview prefab. If null, the GridPlacementSystem's previewPrefab is used.")]
    public GameObject customPreviewPrefab;

    [Header("Visuals")]
    [Tooltip("Icon for hotbar / build menu.")]
    public Sprite icon;

    [Header("Inventory")]
    [Tooltip("Starting amount. -1 means infinite / creative mode.")]
    public int startingAmount = -1;
}
