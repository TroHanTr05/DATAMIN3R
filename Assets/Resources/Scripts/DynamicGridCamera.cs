using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ResizableGridCamera : MonoBehaviour
{
    [Header("Window Size Settings")]
    public int initialLaunchWidth = 480;
    public int initialLaunchHeight = 480;

    [Tooltip("Smallest window size allowed")]
    public int minAllowedWidth = 480;
    public int minAllowedHeight = 480;

    [Header("Grid-based Max Window Limits")]
    [Tooltip("Extra cells of buffer around grid on each side")]
    public int bufferCells = 1;

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

    [Header("Reference Resolution (Max Camera Size)")]
    public int referenceWidth = 1920;
    public int referenceHeight = 1080;

    public float verticalWorldSizeAtReference = 10f;

    [Header("Grid Settings")]
    public int gridWidth = 20;
    public int gridHeight = 20;
    public float cellSize = 1f;

    public Color gridColor = Color.gray;
    public Material gridMaterial;

    [Header("Zoom Settings")]
    [Tooltip("1 = default scale (no zoom). 0.25 = 4x zoom in max.")]
    public float minZoomFactor = 0.25f;   
    public float zoomSpeed = 0.1f;       

    private Camera cam;
    private float unitsPerPixel;
    private float zoomFactor = 1f;

    private Vector2 initialCenter;

    private bool isPanning = false;
    private Vector3 panStartCamPos;
    private Vector2 panStartMouseScreen;

    public float PixelsPerCell => cellSize / unitsPerPixel;

    public int VisibleCellsX { get; private set; }
    public int VisibleCellsY { get; private set; }
    public int VisibleCellsTotal => VisibleCellsX * VisibleCellsY;

    public bool TryGetMouseCell(out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (cam == null)
            cam = GetComponent<Camera>();

        if (gridWidth <= 0 || gridHeight <= 0 || cellSize <= 0f)
            return false;

        Vector3 mouseScreen = Input.mousePosition;

        // Assume the grid lies on z = 0, matching the grid drawing in OnPostRender
        float planeZ = 0f;
        float dist = planeZ - cam.transform.position.z;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, dist));

        float totalWidth = gridWidth * cellSize;
        float totalHeight = gridHeight * cellSize;

        float left = -totalWidth * 0.5f;
        float bottom = -totalHeight * 0.5f;

        float localX = mouseWorld.x - left;
        float localY = mouseWorld.y - bottom;

        if (localX < 0f || localY < 0f)
            return false;

        int cx = Mathf.FloorToInt(localX / cellSize);
        int cy = Mathf.FloorToInt(localY / cellSize);

        if (cx < 0 || cx >= gridWidth || cy < 0 || cy >= gridHeight)
            return false;

        cellX = cx;
        cellY = cy;
        return true;
    }

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

        unitsPerPixel = verticalWorldSizeAtReference / referenceHeight;
    }

    private void Update()
    {
        HandleZoomInput();
        HandlePanInput();
        HandleWindowSizeEnforcement();
    }

    private void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            zoomFactor -= scroll * zoomSpeed;
            zoomFactor = Mathf.Clamp(zoomFactor, minZoomFactor, 1f);
        }
    }

    private void HandlePanInput()
    {
        if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))))
        {
            isPanning = true;
            panStartCamPos = transform.position;
            panStartMouseScreen = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(0))
        {
            isPanning = false;
        }

        if (isPanning)
        {
            Vector2 currentMouseScreen = Input.mousePosition;
            Vector2 screenDelta = currentMouseScreen - panStartMouseScreen;

            float worldPerPixelY = (cam.orthographicSize * 2f) / Screen.height;
            float worldPerPixelX = worldPerPixelY * cam.aspect;

            Vector3 deltaWorld = new Vector3(
                -screenDelta.x * worldPerPixelX,
                -screenDelta.y * worldPerPixelY,
                0f
            );

            Vector3 targetPos = panStartCamPos + deltaWorld;
            targetPos.z = panStartCamPos.z;

            transform.position = targetPos;
        }
    }

    private void HandleWindowSizeEnforcement()
    {
        if (!Application.isPlaying)
            return;

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

            return;
        }

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

        int targetW = Mathf.Max(currentW, minAllowedWidth);
        int targetH = Mathf.Max(currentH, minAllowedHeight);

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
            if (hardMaxWidth > 0) targetW = Mathf.Min(targetW, hardMaxWidth);
            if (hardMaxHeight > 0) targetH = Mathf.Min(targetH, hardMaxHeight);
        }

        if (targetW == currentW && targetH == currentH)
            return;

        correctionInProgress = true;
        corrStartW = currentW;
        corrStartH = currentH;
        corrTargetW = targetW;
        corrTargetH = targetH;
        corrElapsed = 0f;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        int effectiveHeight = Mathf.Min(Screen.height, referenceHeight);
        float baseOrthoSize = (effectiveHeight * unitsPerPixel) * 0.5f;

        float finalOrthoSize = baseOrthoSize * zoomFactor;
        cam.orthographicSize = finalOrthoSize;

        float baseHalfHeight = baseOrthoSize;
        float baseHalfWidth = baseHalfHeight * cam.aspect;

        float finalHalfHeight = finalOrthoSize;
        float finalHalfWidth = finalHalfHeight * cam.aspect;

        // Compute how many grid cells are currently visible
        float visibleWorldHeight = finalOrthoSize * 2f;
        float visibleWorldWidth = visibleWorldHeight * cam.aspect;

        if (cellSize > 0f)
        {
            VisibleCellsX = Mathf.CeilToInt(visibleWorldWidth / cellSize);
            VisibleCellsY = Mathf.CeilToInt(visibleWorldHeight / cellSize);
        }
        else
        {
            VisibleCellsX = 0;
            VisibleCellsY = 0;
        }

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

        float left = -totalWidth * 0.5f;
        float bottom = -totalHeight * 0.5f;

        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = left + x * cellSize;
            GL.Vertex3(xPos, bottom, 0f);
            GL.Vertex3(xPos, bottom + totalHeight, 0f);
        }

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
