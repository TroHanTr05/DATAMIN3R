using UnityEngine;
using TMPro;

public class PowerUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI powerChangeText;

    [Header("Color Settings")]
    public Color positiveColor = Color.green;
    public Color negativeColor = Color.red;
    public Color neutralColor = Color.yellow;

    private void Update()
    {
        if (PowerManager.Instance == null)
            return;

        float power = PowerManager.Instance.currentPower;
        float delta = PowerManager.Instance.netPowerChange;

        // --- Display current power ---
        powerText.text = $"Power: {power:F1}";

        // --- Display net change ---
        if (Mathf.Abs(delta) < 0.001f)
        {
            powerChangeText.text = "+0.0 / sec";
            powerChangeText.color = neutralColor;
        }
        else
        {
            string sign = delta > 0 ? "+" : "";
            powerChangeText.text = $"{sign}{delta:F1} / sec";
            powerChangeText.color = delta > 0 ? positiveColor : negativeColor;
        }
    }
}