using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ResizableGridCamera : MonoBehaviour
{
    // -------------------------------------------------------------
    //  WINDOW SIZE SETTINGS (min + max + smoothing)
    // -------------------------------------------------------------
    [Header("Window Size Settings")]
    public int initialLaunchWidth = 480;
    public int initialLaunchHeight = 480;

    [Tooltip("Smallest window size allowed")]
    public int minAllowedWidth = 480;
    public int minAllowedHeight = 480;

    [Header("Grid-based Max Window Limits")]
    [Tooltip("Extra cells of buffer around grid on each side")]
    public int bufferCells = 1;   // 1 cell margin left/right/top/bottom

    [Tooltip("Hard pixel max per axis (0 = ignore)")]
    public int hardMaxWidth = 2048;
    public int hardMaxHeight = 2048;

    [Header("Resize Clamp Timing")]
    [Tooltip("How long after resizing stops before we correct size")]
    public float resizeFinishDelay = 0.15f;

    [Tooltip("How long the correction lerp lasts")]
    public float resizeLerpDuration = 0.2f;

    private int lastWinW;
    private int lastWinH;

    private bool userResizing = false;
    private float resizeIdleTimer = 0f;

    private bool correctionInProgress = false;
    private int corrStartW, corrStartH;
    private int corrTargetW, corrTargetH;
    private float corrElapsed = 0f;

    // -------------------------------------------------------------
    //  CAMERA + GRID SETTINGS
    // -------------------------------------------------------------
    [Header("Reference Resolution (Max Camera Size)")]
    public int referenceWidth = 1920;
    public int referenceHeight = 1080;

    // How many world units tall the camera should see at referenceHeight
    public float verticalWorldSizeAtReference = 10f;

    [Header("Grid Settings")]
    public int gridWidth = 20;     // number of cells horizontally
    public int gridHeight = 20;    // number of cells vertically
    public float cellSize = 1f;    // world units per cell

    public Color gridColor = Color.gray;
    public Material gridMaterial;  // simple Unlit/Color material

    [Header("Zoom Settings")]
    [Tooltip("1 = default scale (no zoom). 0.25 = 4x zoom in max.")]
    public float minZoomFactor = 0.25f;   // how close you can zoom in
    public float zoomSpeed = 0.1f;        // how fast mouse wheel zooms

    private Camera cam;
    private float unitsPerPixel;          // world units per screen pixel (height)
    private float zoomFactor = 1f;        // 1 = default, <1 = zoom in

    // starting center of the camera (used for panning clamp)
    private Vector2 initialCenter;

    // panning
    private bool isPanning = false;
    private Vector3 panStartCamPos;
    private Vector2 panStartMouseScreen;

    // used by window limit logic
    public float PixelsPerCell => cellSize / unitsPerPixel;

    // -------------------------------------------------------------
    //  UNITY EVENTS
    // -------------------------------------------------------------
    private void OnValidate()
    {
        SetupCamera();
    }

    private void Awake()
    {
        SetupCamera();
        initialCenter = transform.position;

        if (Application.isPlaying)
        {
            // Set initial size at launch
            Screen.SetResolution(initialLaunchWidth, initialLaunchHeight, FullScreenMode.Windowed);

            lastWinW = Screen.width;
            lastWinH = Screen.height;
        }
    }

    private void SetupCamera()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        cam.orthographic = true;

        if (referenceHeight <= 0)
            referenceHeight = 1080;

        // world units tall at referenceHeight / pixels tall at referenceHeight
        unitsPerPixel = verticalWorldSizeAtReference / referenceHeight;
    }

    private void Update()
    {
        HandleZoomInput();
        HandlePanInput();
        HandleWindowSizeEnforcement(); // min + max with smoothing
    }

    // -------------------------------------------------------------
    //  INPUT: ZOOM
    // -------------------------------------------------------------
    private void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // scroll up -> zoom in (smaller zoomFactor)
            zoomFactor -= scroll * zoomSpeed;
            zoomFactor = Mathf.Clamp(zoomFactor, minZoomFactor, 1f);
        }
    }

    // -------------------------------------------------------------
    //  INPUT: PANNING
    // -------------------------------------------------------------
    private void HandlePanInput()
    {
        if (Input.GetMouseButtonDown(2)) // middle mouse pressed
        {
            isPanning = true;
            panStartCamPos = transform.position;
            panStartMouseScreen = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        if (isPanning)
        {
            Vector2 currentMouseScreen = Input.mousePosition;
            Vector2 screenDelta = currentMouseScreen - panStartMouseScreen;

            // How many world units per screen pixel (vertically)
            float worldPerPixelY = (cam.orthographicSize * 2f) / Screen.height;
            float worldPerPixelX = worldPerPixelY * cam.aspect;

            // Move opposite the mouse drag
            Vector3 deltaWorld = new Vector3(
                -screenDelta.x * worldPerPixelX,
                -screenDelta.y * worldPerPixelY,
                0f
            );

            Vector3 targetPos = panStartCamPos + deltaWorld;
            targetPos.z = panStartCamPos.z; // never change Z

            transform.position = targetPos;
        }
    }

    // -------------------------------------------------------------
    //  WINDOW SIZE ENFORCEMENT (min + max + smoothing)
    // -------------------------------------------------------------
    private void HandleWindowSizeEnforcement()
    {
        if (!Application.isPlaying)
            return;

        // If we're currently animating a correction, lerp the window size
        if (correctionInProgress)
        {
            corrElapsed += Time.deltaTime;
            float t = resizeLerpDuration > 0f
                ? Mathf.Clamp01(corrElapsed / resizeLerpDuration)
                : 1f;

            int newW = Mathf.RoundToInt(Mathf.Lerp(corrStartW, corrTargetW, t));
            int newH = Mathf.RoundToInt(Mathf.Lerp(corrStartH, corrTargetH, t));

            if (newW != Screen.width || newH != Screen.height)
            {
                Screen.SetResolution(newW, newH, FullScreenMode.Windowed);
            }

            if (t >= 1f)
            {
                correctionInProgress = false;
                lastWinW = Screen.width;
                lastWinH = Screen.height;
            }

            return; // don't treat this as user resize
        }

        // Detect user resizing
        if (Screen.width != lastWinW || Screen.height != lastWinH)
        {
            lastWinW = Screen.width;
            lastWinH = Screen.height;

            userResizing = true;
            resizeIdleTimer = 0f;
        }

        if (userResizing)
        {
            resizeIdleTimer += Time.deltaTime;

            // new: only correct when they've let go of the window (no mouse buttons held)
            bool mouseReleased =
                !Input.GetMouseButton(0) &&
                !Input.GetMouseButton(1) &&
                !Input.GetMouseButton(2);

            if (resizeIdleTimer >= resizeFinishDelay && mouseReleased)
            {
                userResizing = false;
                StartSizeCorrectionIfNeeded();
            }
        }
    }

    private void StartSizeCorrectionIfNeeded()
    {
        int currentW = Screen.width;
        int currentH = Screen.height;

        // --- MIN LIMITS ---
        int targetW = Mathf.Max(currentW, minAllowedWidth);
        int targetH = Mathf.Max(currentH, minAllowedHeight);

        // --- MAX LIMITS (grid + buffer + hard cap) ---
        float ppc = PixelsPerCell;
        if (ppc > 0f)
        {
            int bufferedGridWidthCells = gridWidth + bufferCells * 2;
            int bufferedGridHeightCells = gridHeight + bufferCells * 2;

            int maxWidthByGridPixels = Mathf.FloorToInt(bufferedGridWidthCells * ppc);
            int maxHeightByGridPixels = Mathf.FloorToInt(bufferedGridHeightCells * ppc);

            int finalMaxW = maxWidthByGridPixels;
            int finalMaxH = maxHeightByGridPixels;

            if (hardMaxWidth > 0)
                finalMaxW = Mathf.Min(finalMaxW, hardMaxWidth);
            if (hardMaxHeight > 0)
                finalMaxH = Mathf.Min(finalMaxH, hardMaxHeight);

            targetW = Mathf.Min(targetW, finalMaxW);
            targetH = Mathf.Min(targetH, finalMaxH);
        }
        else
        {
            // fallback to hard max only, if PixelsPerCell isn't ready
            if (hardMaxWidth > 0) targetW = Mathf.Min(targetW, hardMaxWidth);
            if (hardMaxHeight > 0) targetH = Mathf.Min(targetH, hardMaxHeight);
        }

        // If inside all limits, nothing to do
        if (targetW == currentW && targetH == currentH)
            return;

        // Start smooth correction
        correctionInProgress = true;
        corrStartW = currentW;
        corrStartH = currentH;
        corrTargetW = targetW;
        corrTargetH = targetH;
        corrElapsed = 0f;
    }

    // -------------------------------------------------------------
    // CAMERA FRAMING + GRID RENDERING
    // -------------------------------------------------------------
    private void LateUpdate()
    {
        if (cam == null) return;

        // --- 1. Compute base ortho size from window height (no zoom) ---
        int effectiveHeight = Mathf.Min(Screen.height, referenceHeight);
        float baseOrthoSize = (effectiveHeight * unitsPerPixel) * 0.5f;

        // --- 2. Apply zoom factor ---
        float finalOrthoSize = baseOrthoSize * zoomFactor;
        cam.orthographicSize = finalOrthoSize;

        // --- 3. Constrain camera position so we never leave the
        //         area that would be visible at zoomFactor = 1 ---

        float baseHalfHeight = baseOrthoSize;
        float baseHalfWidth = baseHalfHeight * cam.aspect;

        float finalHalfHeight = finalOrthoSize;
        float finalHalfWidth = finalHalfHeight * cam.aspect;

        // how far we’re allowed to move from the initial center
        float maxOffsetX = Mathf.Max(0f, baseHalfWidth - finalHalfWidth);
        float maxOffsetY = Mathf.Max(0f, baseHalfHeight - finalHalfHeight);

        Vector3 pos = transform.position;

        float minX = initialCenter.x - maxOffsetX;
        float maxX = initialCenter.x + maxOffsetX;
        float minY = initialCenter.y - maxOffsetY;
        float maxY = initialCenter.y + maxOffsetY;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }

    private void OnPostRender()
    {
        if (gridMaterial == null) return;
        if (gridWidth <= 0 || gridHeight <= 0 || cellSize <= 0) return;

        gridMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.LINES);
        GL.Color(gridColor);

        float totalWidth = gridWidth * cellSize;
        float totalHeight = gridHeight * cellSize;

        // Center the grid around (0,0)
        float left = -totalWidth * 0.5f;
        float bottom = -totalHeight * 0.5f;

        // Vertical lines
        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = left + x * cellSize;
            GL.Vertex3(xPos, bottom, 0f);
            GL.Vertex3(xPos, bottom + totalHeight, 0f);
        }

        // Horizontal lines
        for (int y = 0; y <= gridHeight; y++)
        {
            float yPos = bottom + y * cellSize;
            GL.Vertex3(left, yPos, 0f);
            GL.Vertex3(left + totalWidth, yPos, 0f);
        }

        GL.End();
        GL.PopMatrix();
    }
}
