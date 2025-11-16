using UnityEngine;

[CreateAssetMenu(menuName = "Build/Build Block", fileName = "NewBuildBlock")]
public class BuildBlock : ScriptableObject
{
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
}