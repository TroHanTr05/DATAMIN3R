using System.Collections.Generic;
using UnityEngine;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance;

    [Header("Starting Power")]
    public float initialPower = 10f;
    public float currentPower;

    [Header("Global Power Rules")]
    public float tileBaseDrain = 0.05f;   // per tile on the grid

    [Tooltip("Reference to the GridPlacementSystem for tile counting")]
    public GridPlacementSystem gridSystem;

    // All active buildings in the world
    private readonly List<PlaceableObject> activePlaceables = new List<PlaceableObject>();

    // For UI / debugging
    public float totalTileDrain { get; private set; }
    public float totalBuildingDrain { get; private set; }
    public float totalGeneration { get; private set; }
    public float netPowerChange => totalGeneration - (totalTileDrain + totalBuildingDrain);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        currentPower = initialPower;
    }

    private void Start()
    {
        if (gridSystem == null)
            gridSystem = FindObjectOfType<GridPlacementSystem>();

        RecalculatePower();
    }

    private void Update()
    {
        float delta = netPowerChange * Time.deltaTime;
        currentPower += delta;

        if (currentPower < 0)
        {
            currentPower = 0;
            // TODO: Trigger “Out of Power” game effects
        }
    }

    // Called by GridPlacementSystem when something is placed
    public void RegisterPlaceable(PlaceableObject obj)
    {
        if (!activePlaceables.Contains(obj))
        {
            activePlaceables.Add(obj);
            RecalculatePower();
        }
    }

    // Called when an object is removed
    public void UnregisterPlaceable(PlaceableObject obj)
    {
        if (activePlaceables.Contains(obj))
        {
            activePlaceables.Remove(obj);
            RecalculatePower();
        }
    }

    public void RecalculatePower()
    {
        totalTileDrain = (gridSystem.gridCamera.gridWidth *
                          gridSystem.gridCamera.gridHeight)
                          * tileBaseDrain;

        totalBuildingDrain = 0;
        totalGeneration = 0;

        foreach (PlaceableObject po in activePlaceables)
        {
            var block = po.blockDefinition;
            if (block == null) continue;

            totalBuildingDrain += Mathf.Max(0, block.powerDrain);
            totalGeneration += Mathf.Max(0, block.powerGeneration);
        }
    }
}
