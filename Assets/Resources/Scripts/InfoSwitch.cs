using UnityEngine;
using TMPro;

public class InfoSwitch : MonoBehaviour
{
    [Header("References")]
    public ResizableGridCamera gridCamera;
    public PowerHandler powerHandler;
    public GridPlacementSystem gridPlacementSystem;
    public TextMeshProUGUI infoText;
    private RectTransform infoRect;

    [Header("Power Display")]
    [Tooltip("Base power draw per cell (positive = consumes power)")]
    public float basePowerDrawPerCell = 1f;

    [Tooltip("Temporary debug value: flat power generated per cell (used only if PowerHandler missing)")]
    public float debugGeneratedPowerPerCell = 0f;

    // 0 = show cell info, 1 = blank (can add more modes later)
    private int currentModeIndex = 0;

    private void Start()
    {
        infoRect = infoText != null ? infoText.GetComponent<RectTransform>() : null;

        if (powerHandler == null)
        {
            powerHandler = FindObjectOfType<PowerHandler>();
            if (powerHandler == null)
            {
                Debug.LogWarning("InfoSwitch: No PowerHandler found. Falling back to debug power values.");
            }
        }

        if (gridPlacementSystem == null)
        {
            gridPlacementSystem = FindObjectOfType<GridPlacementSystem>();
            if (gridPlacementSystem == null)
            {
                Debug.LogWarning("InfoSwitch: No GridPlacementSystem found. Per-cell tower power will be 0.");
            }
        }

        UpdateInfoDisplay(force: true);
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

        // Make the info follow the mouse
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
            int total = gridCamera.VisibleCellsTotal;
            string cellCountLine = $"Cell Count: {total}";

            // --- Global net power ---

            // Use the same value PowerHandler actually uses.
            float netPower = 0f;
            if (powerHandler != null)
            {
                netPower = powerHandler.LastNetPerSecond;
            }
            else
            {
                // Fallback approximation if PowerHandler is missing
                float fallbackNetPerCell = debugGeneratedPowerPerCell - basePowerDrawPerCell;
                netPower = total * fallbackNetPerCell;
            }

            string netPowerLine = $"Net Power: {netPower:0.##} p/s";

            // --- Individual hovered cell power ---

            int hoveredX, hoveredY;
            string hoveredLine;
            if (TryGetHoveredCell(out hoveredX, out hoveredY))
            {
                // Base drain per cell (positive = consumes)
                float cellBaseDraw = basePowerDrawPerCell;
                if (powerHandler != null)
                {
                    cellBaseDraw = powerHandler.powerUsePerCellPerSecond;
                }

                // Sum of block powerUsage on THIS cell
                float towerPowerOnCell = 0f;
                if (gridPlacementSystem != null)
                {
                    var po = gridPlacementSystem.GetPlaceableAtCell(hoveredX, hoveredY);
                    if (po != null)
                    {
                        towerPowerOnCell += po.powerUsage;
                    }
                }

                // Net cell power = generators - base drain
                float hoveredNetPower = towerPowerOnCell - cellBaseDraw;

                // Example:
                // base = 0.025, no block => 0 - 0.025 = -0.025
                // base = 0.025, block = 100 => 100 - 0.025 = 99.975
                hoveredLine = $"Cell [{hoveredX}, {hoveredY}]: {hoveredNetPower:0.###} p/s";
            }
            else
            {
                hoveredLine = "Cell: (no cell under cursor)";
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