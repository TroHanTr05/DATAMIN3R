using UnityEngine;
using TMPro;

public class PowerHandler : MonoBehaviour
{
    public ResizableGridCamera gridCamera;

    public float startingPower = 1000f;

    // Base power draw per *visible* grid cell per second (positive = consumes power)
    public float powerUsePerCellPerSecond = 1f;

    public UnityEngine.UI.Slider powerSlider;
    public UnityEngine.UI.Image powerFillImage;

    public Color normalColor = Color.blue;
    public Color lowColor = Color.yellow;
    public Color criticalColor = Color.red;
    public float colorFadeSpeed = 5f;

    private float currentPower;

    // Latest computed rates (per second) for UI / debug queries
    public float LastTowerNetPerSecond { get; private set; }
    public float LastCellDrainPerSecond { get; private set; }
    public float LastNetPerSecond { get; private set; }

    // Track all placeable objects that affect power
    private readonly System.Collections.Generic.List<PlaceableObject> trackedPlaceables =
        new System.Collections.Generic.List<PlaceableObject>();

    public void RegisterPlaceable(PlaceableObject placeable)
    {
        if (placeable == null) return;
        if (!trackedPlaceables.Contains(placeable))
            trackedPlaceables.Add(placeable);
    }

    public void UnregisterPlaceable(PlaceableObject placeable)
    {
        if (placeable == null) return;
        trackedPlaceables.Remove(placeable);
    }

    void Start()
    {
        currentPower = startingPower;

        if (powerSlider != null)
        {
            powerSlider.maxValue = startingPower;
            powerSlider.value = startingPower;
        }
    }

    void Update()
    {
        if (gridCamera == null) return;

        // 1) Base power drain from visible cells
        float cellDrainPerSecond = gridCamera.VisibleCellsTotal * powerUsePerCellPerSecond;
        LastCellDrainPerSecond = cellDrainPerSecond;

        // 2) Add up all block power usage
        float towerNetPerSecond = 0f;
        for (int i = trackedPlaceables.Count - 1; i >= 0; i--)
        {
            var p = trackedPlaceables[i];
            if (p == null)
            {
                trackedPlaceables.RemoveAt(i);
                continue;
            }

            // Positive = generates power, Negative = consumes power
            towerNetPerSecond += p.powerUsage;
        }
        LastTowerNetPerSecond = towerNetPerSecond;

        // 3) Net power change = blocks - cell cost
        float netPerSecond = towerNetPerSecond - cellDrainPerSecond;
        LastNetPerSecond = netPerSecond;

        // Apply over time
        currentPower += netPerSecond * Time.deltaTime;
        currentPower = Mathf.Clamp(currentPower, 0f, startingPower);

        if (powerSlider != null)
        {
            powerSlider.value = currentPower;
        }

        // Update slider color based on remaining power percentage (smooth fade, linear gradient)
        if (powerFillImage != null && startingPower > 0f)
        {
            float powerPercent = currentPower / startingPower; // 0..1
            Color targetColor;

            // Linear gradient: blue -> yellow -> red as power decreases
            if (powerPercent >= 0.25f)
            {
                // From lowColor at 25% to normalColor at 100%
                float t = (powerPercent - 0.25f) / (1f - 0.25f); // 0 at 25%, 1 at 100%
                targetColor = Color.Lerp(lowColor, normalColor, t);
            }
            else
            {
                // From criticalColor at 0% to lowColor at 25%
                float t = powerPercent / 0.25f; // 0 at 0%, 1 at 25%
                targetColor = Color.Lerp(criticalColor, lowColor, t);
            }

            powerFillImage.color = Color.Lerp(
                powerFillImage.color,
                targetColor,
                Time.deltaTime * colorFadeSpeed
            );
        }
    }
}