using UnityEngine;

[RequireComponent(typeof(ResizableGridCamera))]
public class WindowSnapper : MonoBehaviour
{
    [Header("Time after last resize before clamping (seconds)")]
    public float resizeFinishDelay = 0.15f;

    [Header("Buffer around grid in cells (on each side)")]
    public int bufferCells = 1;   // 1 cell margin left/right/top/bottom

    [Header("Hard pixel max per axis (0 = ignore)")]
    public int hardMaxWidth = 0;    // e.g. 2046
    public int hardMaxHeight = 0;   // e.g. 2046

    private ResizableGridCamera gridCam;

    private int lastWidth;
    private int lastHeight;

    private float timeSinceLastSizeChange = 0f;

    void Awake()
    {
        gridCam = GetComponent<ResizableGridCamera>();
        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }

    void Update()
    {
        // Detect any change in window size
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            // reset timer whenever size changes
            timeSinceLastSizeChange = 0f;
        }
        else
        {
            // only accumulate idle time if size is NOT changing
            timeSinceLastSizeChange += Time.deltaTime;
        }

        // While any mouse button is down, we consider the user to be "still resizing".
        bool anyMouseDown =
            Input.GetMouseButton(0) ||
            Input.GetMouseButton(1) ||
            Input.GetMouseButton(2);

        // Only clamp when:
        //  - window hasn't changed size for a while
        //  - AND the user has let go of the mouse (no drag in progress)
        if (!anyMouseDown && timeSinceLastSizeChange >= resizeFinishDelay)
        {
            ClampToGridWithBuffer();
            // reset timer so we don't keep clamping every frame
            timeSinceLastSizeChange = 0f;
        }
    }

    private void ClampToGridWithBuffer()
    {
        float ppc = gridCam.PixelsPerCell;
        if (ppc <= 0f) return;

        // --- 1. Grid-based limits with buffer cells on ALL sides ---
        int bufferedGridWidthCells = gridCam.gridWidth + bufferCells * 2;
        int bufferedGridHeightCells = gridCam.gridHeight + bufferCells * 2;

        int maxWidthByGridPixels = Mathf.FloorToInt(bufferedGridWidthCells * ppc);
        int maxHeightByGridPixels = Mathf.FloorToInt(bufferedGridHeightCells * ppc);

        // --- 2. Apply hard pixel caps (like 2046) if set ---
        int finalMaxW = maxWidthByGridPixels;
        int finalMaxH = maxHeightByGridPixels;

        if (hardMaxWidth > 0)
            finalMaxW = Mathf.Min(finalMaxW, hardMaxWidth);

        if (hardMaxHeight > 0)
            finalMaxH = Mathf.Min(finalMaxH, hardMaxHeight);

        // --- 3. Clamp the current window size to those limits ---
        int w = Screen.width;
        int h = Screen.height;

        int clampedW = Mathf.Min(w, finalMaxW);
        int clampedH = Mathf.Min(h, finalMaxH);

        if (clampedW != w || clampedH != h)
        {
            Screen.SetResolution(clampedW, clampedH, FullScreenMode.Windowed);
            lastWidth = clampedW;
            lastHeight = clampedH;
        }
    }
}
