using UnityEngine;
using TMPro;

public class InfoSwitch : MonoBehaviour
{
    [Header("References")]
    public ResizableGridCamera gridCamera;
    public TextMeshProUGUI infoText;
    private RectTransform infoRect;

    [Header("Power Display")]
    [Tooltip("Base power draw per cell (positive = consumes power)")]
    public float basePowerDrawPerCell = 1f;

    [Tooltip("Temporary debug value: flat power generated per cell (will later come from towers)")]
    public float debugGeneratedPowerPerCell = 0f;

    // 0 = show cell info, 1 = blank (can add more modes later)
    private int currentModeIndex = 0;

    private void Start()
    {
        UpdateInfoDisplay(force: true);
        infoRect = infoText.GetComponent<RectTransform>();
    }

    private void Update()
    {
        // Cycle info mode with I key
        if (Input.GetKeyDown(KeyCode.I))
        {
            CycleMode();
        }

        // Continuously update when we're in a mode that shows live data
        UpdateInfoDisplay(force: false);

        if (infoRect != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                infoRect.parent as RectTransform,
                Input.mousePosition,
                null,
                out pos);
            infoRect.anchoredPosition = pos;
        }
    }

    private void CycleMode()
    {
        // Right now we have 2 modes: 0 = info, 1 = blank
        currentModeIndex++;
        if (currentModeIndex > 1)
        {
            currentModeIndex = 0;
        }

        UpdateInfoDisplay(force: true);
    }

    private void UpdateInfoDisplay(bool force)
    {
        if (infoText == null)
            return;

        // Mode 0: show cell + power info
        if (currentModeIndex == 0)
        {
            if (gridCamera == null)
            {
                infoText.text = string.Empty;
                return;
            }

            // Cell count
            int cellsX = gridCamera.VisibleCellsX;
            int cellsY = gridCamera.VisibleCellsY;
            int total = gridCamera.VisibleCellsTotal;

            string cellCountLine = $"Cell Count: {total}";

            // Net power per cell = generated - draw (can be negative if more is drawn than generated)
            float netPerCell = debugGeneratedPowerPerCell - basePowerDrawPerCell;

            // Net power for all currently visible cells
            float netPower = total * netPerCell;
            string netPowerLine = $"{netPower:0.##} p/s";

            // Net individual cell power under mouse cursor
            int hoveredX, hoveredY;
            string hoveredLine;
            if (TryGetHoveredCell(out hoveredX, out hoveredY))
            {
                float hoveredNetPower = netPerCell; // placeholder until we query real per-cell tower data
                hoveredLine = $"[{hoveredX}, {hoveredY}]: {hoveredNetPower:0.##} p/s";
            }
            else
            {
                hoveredLine = "Net Individual Cell Power: (no cell under cursor)";
            }

            infoText.text = cellCountLine + " | " + netPowerLine + " | " + hoveredLine;
        }
        // Mode 1: blank text
        else if (currentModeIndex == 1)
        {
            // Only clear once or when forced
            if (force || !string.IsNullOrEmpty(infoText.text))
            {
                infoText.text = string.Empty;
            }
        }
    }

    private bool TryGetHoveredCell(out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (gridCamera == null)
            return false;

        Camera cam = gridCamera.GetComponent<Camera>();
        if (cam == null)
            return false;

        if (gridCamera.gridWidth <= 0 || gridCamera.gridHeight <= 0 || gridCamera.cellSize <= 0f)
            return false;

        Vector3 mouseScreen = Input.mousePosition;

        // Assume the grid lies on z = 0, same convention as the grid drawing in ResizableGridCamera
        float planeZ = 0f;
        float dist = planeZ - cam.transform.position.z;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, dist));

        float totalWidth = gridCamera.gridWidth * gridCamera.cellSize;
        float totalHeight = gridCamera.gridHeight * gridCamera.cellSize;

        float left = -totalWidth * 0.5f;
        float bottom = -totalHeight * 0.5f;

        float localX = mouseWorld.x - left;
        float localY = mouseWorld.y - bottom;

        if (localX < 0f || localY < 0f)
            return false;

        int cx = Mathf.FloorToInt(localX / gridCamera.cellSize);
        int cy = Mathf.FloorToInt(localY / gridCamera.cellSize);

        if (cx < 0 || cx >= gridCamera.gridWidth || cy < 0 || cy >= gridCamera.gridHeight)
            return false;

        cellX = cx;
        cellY = cy;
        return true;
    }
}
