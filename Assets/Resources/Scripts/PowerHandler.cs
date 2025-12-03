using UnityEngine;
using TMPro;

public class PowerHandler : MonoBehaviour
{
    public ResizableGridCamera gridCamera;

    public float startingPower = 1000f;
    public float powerUsePerCellPerSecond = 1f;
    public UnityEngine.UI.Slider powerSlider;

    public UnityEngine.UI.Image powerFillImage;

    public Color normalColor = Color.blue;
    public Color lowColor = Color.yellow;
    public Color criticalColor = Color.red;
    public float colorFadeSpeed = 5f;
    private float currentPower;

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

        // Power drain based on visible cells
        float drainAmount = gridCamera.VisibleCellsTotal * powerUsePerCellPerSecond * Time.deltaTime;
        currentPower -= drainAmount;
        currentPower = Mathf.Max(currentPower, 0f);

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

            powerFillImage.color = Color.Lerp(powerFillImage.color, targetColor, Time.deltaTime * colorFadeSpeed);
        }

    }
}