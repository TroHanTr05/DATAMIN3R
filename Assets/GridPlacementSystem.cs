using System.Collections.Generic;
using UnityEngine;

public class GridPlacementSystem : MonoBehaviour
{
    [Header("References")]
    public ResizableGridCamera gridCamera;   // assign your camera script here
    public Camera worldCamera;               // usually Camera.main

    [Header("Placement")]
    public GameObject placeablePrefab;       // what to spawn on the grid
    public GameObject previewPrefab;         // optional ghost / preview object

    [Tooltip("Optional parent for all placed objects (should NOT be the camera).")]
    public Transform placementParent;

    [Tooltip("Z position for placed objects (grid is at z = 0)")]
    public float placementZ = 0f;

    [Header("Preview Settings")]
    [Tooltip("Sorting order so previews render above everything else.")]
    public int previewSortingOrder = 1000;

    private GameObject previewInstance;
    private SpriteRenderer previewRenderer;

    // now store actual objects, not just bool
    private GameObject[,] placedObjects;     // [x, y] world objects

    private float totalWidth;
    private float totalHeight;
    private float left;
    private float bottom;

    // drag-placement / erasing state
    private bool isPlacingDrag = false;
    private bool isErasingDrag = false;
    private bool ctrlPlacementMode = false;        // are we in ctrl-line mode for this drag?
    private Vector2Int dragStartCell;
    private HashSet<Vector2Int> cellsModifiedThisDrag = new HashSet<Vector2Int>();

    // line preview pool (for ctrl drag)
    private readonly List<GameObject> linePreviewInstances = new List<GameObject>();
    private readonly List<SpriteRenderer> linePreviewRenderers = new List<SpriteRenderer>();

    // preview colors
    private Color previewCanPlaceColor = new Color(0f, 0.6f, 1f, 0.5f); // bluish, semi-transparent
    private Color previewBlockedColor = new Color(1f, 0f, 0f, 0.5f);   // red, semi-transparent

    void Start()
    {
        if (gridCamera == null)
        {
            Debug.LogError("GridPlacementSystem: gridCamera reference is missing.");
            enabled = false;
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        // precompute grid bounds based on the same logic as ResizableGridCamera's OnPostRender
        totalWidth = gridCamera.gridWidth * gridCamera.cellSize;
        totalHeight = gridCamera.gridHeight * gridCamera.cellSize;

        left = -totalWidth * 0.5f;
        bottom = -totalHeight * 0.5f;

        // object map
        placedObjects = new GameObject[gridCamera.gridWidth, gridCamera.gridHeight];

        // create main preview instance if we have a prefab
        if (previewPrefab != null)
        {
            previewInstance = Instantiate(previewPrefab);
            previewRenderer = previewInstance.GetComponentInChildren<SpriteRenderer>();
            if (previewRenderer != null)
            {
                previewRenderer.sortingOrder = previewSortingOrder;
            }
            SetPreviewVisible(false);
        }
    }

    void Update()
    {
        HandlePreview();
        HandlePlacementAndErasing();
    }

    // ---------------------------------------------------------
    // PREVIEW / HOVER LOGIC
    // ---------------------------------------------------------
    private void HandlePreview()
    {
        if (previewPrefab == null && previewInstance == null)
            return;

        // Ctrl-line drag: show full line preview
        if (isPlacingDrag && ctrlPlacementMode)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
            {
                SetPreviewVisible(false);
                HideLinePreview();
                return;
            }

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
            {
                SetPreviewVisible(false);
                HideLinePreview();
                return;
            }

            Vector2Int currentCell = new Vector2Int(cellX, cellY);
            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, currentCell);

            // collect all cells along the snapped line
            List<Vector2Int> lineCells = new List<Vector2Int>();
            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                lineCells.Add(c);

            // ensure we have enough preview instances
            EnsureLinePreviewPoolSize(lineCells.Count);

            // determine if any cell in the line is blocked
            bool lineFullyFree = true;
            for (int i = 0; i < lineCells.Count; i++)
            {
                Vector2Int cell = lineCells[i];
                if (IsCellOccupied(cell.x, cell.y))
                {
                    lineFullyFree = false;
                    break;
                }
            }

            Color lineColor = lineFullyFree ? previewCanPlaceColor : previewBlockedColor;

            // position and show line previews
            for (int i = 0; i < linePreviewInstances.Count; i++)
            {
                if (i < lineCells.Count)
                {
                    Vector2Int cell = lineCells[i];
                    Vector3 center = CellToWorldCenter(cell.x, cell.y);
                    linePreviewInstances[i].transform.position = new Vector3(center.x, center.y, placementZ);
                    linePreviewInstances[i].SetActive(true);

                    if (linePreviewRenderers[i] != null)
                    {
                        linePreviewRenderers[i].color = lineColor;
                    }
                }
                else
                {
                    linePreviewInstances[i].SetActive(false);
                }
            }

            // hide single-cell preview while showing line
            SetPreviewVisible(false);
            return;
        }

        // Not in ctrl-line drag: show single-cell preview
        HideLinePreview();

        if (previewInstance == null)
            return;

        Vector3 mouseWorld2;
        if (!TryGetMouseWorldOnGridPlane(out mouseWorld2))
        {
            SetPreviewVisible(false);
            return;
        }

        int cellX2, cellY2;
        if (!WorldToCell(mouseWorld2, out cellX2, out cellY2))
        {
            SetPreviewVisible(false);
            return; // outside grid
        }

        Vector3 cellCenter2 = CellToWorldCenter(cellX2, cellY2);
        previewInstance.transform.position = new Vector3(
            cellCenter2.x,
            cellCenter2.y,
            placementZ
        );

        bool canPlace = !IsCellOccupied(cellX2, cellY2);

        SetPreviewVisible(true);
        SetPreviewColor(canPlace ? previewCanPlaceColor : previewBlockedColor);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewInstance == null) return;

        if (previewInstance.activeSelf != visible)
            previewInstance.SetActive(visible);
    }

    private void SetPreviewColor(Color c)
    {
        if (previewRenderer != null)
        {
            previewRenderer.color = c;
        }
    }

    private void HideLinePreview()
    {
        for (int i = 0; i < linePreviewInstances.Count; i++)
        {
            if (linePreviewInstances[i] != null && linePreviewInstances[i].activeSelf)
                linePreviewInstances[i].SetActive(false);
        }
    }

    private void EnsureLinePreviewPoolSize(int count)
    {
        if (previewPrefab == null) return;

        while (linePreviewInstances.Count < count)
        {
            GameObject inst = Instantiate(previewPrefab);
            var rend = inst.GetComponentInChildren<SpriteRenderer>();
            if (rend != null)
            {
                rend.sortingOrder = previewSortingOrder;
            }
            inst.SetActive(false);
            linePreviewInstances.Add(inst);
            linePreviewRenderers.Add(rend);
        }
    }

    // ---------------------------------------------------------
    // PLACEMENT + ERASING LOGIC (click + drag, Ctrl for line)
    // ---------------------------------------------------------
    private void HandlePlacementAndErasing()
    {
        if (placeablePrefab == null)
            return;

        // --- LEFT MOUSE: place / paint / ctrl-line ---
        if (Input.GetMouseButtonDown(0))
        {
            // begin placement drag
            isPlacingDrag = true;
            isErasingDrag = false;
            cellsModifiedThisDrag.Clear();

            // snapshot whether we're in ctrl-mode for this entire drag
            ctrlPlacementMode = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            Vector3 mouseWorld;
            if (TryGetMouseWorldOnGridPlane(out mouseWorld))
            {
                int cx, cy;
                if (WorldToCell(mouseWorld, out cx, out cy))
                {
                    dragStartCell = new Vector2Int(cx, cy);
                }
            }
        }

        if (Input.GetMouseButton(0) && isPlacingDrag)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
                return;

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
                return; // outside grid

            if (ctrlPlacementMode)
            {
                // Ctrl-line mode: NO placement while dragging.
                // We only place on MouseButtonUp, using dragStartCell → current cell.
            }
            else
            {
                // normal free painting (per-cell under cursor)
                Vector2Int cell = new Vector2Int(cellX, cellY);
                TryPlaceAtCellOncePerDrag(cell);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isPlacingDrag && ctrlPlacementMode)
            {
                // On release, in ctrl-mode: place a snapped line from dragStartCell to current cell.
                Vector3 mouseWorld;
                if (TryGetMouseWorldOnGridPlane(out mouseWorld))
                {
                    int cellX, cellY;
                    if (WorldToCell(mouseWorld, out cellX, out cellY))
                    {
                        Vector2Int endCell = new Vector2Int(cellX, cellY);
                        Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, endCell);

                        foreach (var cell in GetLineCells(dragStartCell, snappedEnd))
                        {
                            TryPlaceAtCellOncePerDrag(cell);
                        }
                    }
                }
            }

            isPlacingDrag = false;
            ctrlPlacementMode = false;
            cellsModifiedThisDrag.Clear();
        }

        // --- RIGHT MOUSE: erase / erase-drag ---
        if (Input.GetMouseButtonDown(1))
        {
            isErasingDrag = true;
            isPlacingDrag = false;
            ctrlPlacementMode = false;
            cellsModifiedThisDrag.Clear();
        }

        if (Input.GetMouseButton(1) && isErasingDrag)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
                return;

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
                return;

            Vector2Int cell = new Vector2Int(cellX, cellY);
            TryEraseCellOncePerDrag(cell);
        }

        if (Input.GetMouseButtonUp(1))
        {
            isErasingDrag = false;
            cellsModifiedThisDrag.Clear();
        }
    }

    private void TryPlaceAtCellOncePerDrag(Vector2Int cell)
    {
        if (cellsModifiedThisDrag.Contains(cell))
            return;

        cellsModifiedThisDrag.Add(cell);

        int x = cell.x;
        int y = cell.y;

        if (IsCellOccupied(x, y))
            return;

        PlaceAtCell(x, y);
    }

    private void TryEraseCellOncePerDrag(Vector2Int cell)
    {
        if (cellsModifiedThisDrag.Contains(cell))
            return;

        cellsModifiedThisDrag.Add(cell);

        int x = cell.x;
        int y = cell.y;

        if (!IsCellOccupied(x, y))
            return;

        EraseAtCell(x, y);
    }

    private bool IsCellOccupied(int x, int y)
    {
        return placedObjects[x, y] != null;
    }

    private void PlaceAtCell(int cellX, int cellY)
    {
        Vector3 cellCenter = CellToWorldCenter(cellX, cellY);

        GameObject placed = Instantiate(
            placeablePrefab,
            new Vector3(cellCenter.x, cellCenter.y, placementZ),
            Quaternion.identity
        );

        if (placementParent != null)
        {
            placed.transform.SetParent(placementParent, true);
        }

        placedObjects[cellX, cellY] = placed;
    }

    private void EraseAtCell(int cellX, int cellY)
    {
        GameObject obj = placedObjects[cellX, cellY];
        if (obj != null)
        {
            Destroy(obj);
            placedObjects[cellX, cellY] = null;
        }
    }

    // ---------------------------------------------------------
    //  45° LINE HELPERS
    // ---------------------------------------------------------

    // Constrain end cell to nearest 0/45/90-degree direction from start
    private Vector2Int ConstrainTo45DegreeLine(Vector2Int start, Vector2Int end)
    {
        Vector2Int delta = end - start;

        if (delta == Vector2Int.zero)
            return end;

        float dx = delta.x;
        float dy = delta.y;

        float absDx = Mathf.Abs(dx);
        float absDy = Mathf.Abs(dy);

        Vector2Int dir;

        if (absDx > absDy * 2f)
        {
            // Mostly horizontal
            dir = new Vector2Int(dx > 0 ? 1 : -1, 0);
        }
        else if (absDy > absDx * 2f)
        {
            // Mostly vertical
            dir = new Vector2Int(0, dy > 0 ? 1 : -1);
        }
        else
        {
            // Diagonal (≈45°)
            dir = new Vector2Int(dx > 0 ? 1 : -1, dy > 0 ? 1 : -1);
        }

        int length = Mathf.RoundToInt(Mathf.Max(absDx, absDy));

        Vector2Int snappedEnd = start + dir * length;

        // Clamp inside grid bounds just in case
        snappedEnd.x = Mathf.Clamp(snappedEnd.x, 0, gridCamera.gridWidth - 1);
        snappedEnd.y = Mathf.Clamp(snappedEnd.y, 0, gridCamera.gridHeight - 1);

        return snappedEnd;
    }

    // Bresenham-style line between two cells
    private IEnumerable<Vector2Int> GetLineCells(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x;
        int y0 = from.y;
        int x1 = to.x;
        int y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            yield return new Vector2Int(x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    // ---------------------------------------------------------
    // HELPERS: mouse → world, world ↔ cell
    // ---------------------------------------------------------
    private bool TryGetMouseWorldOnGridPlane(out Vector3 mouseWorld)
    {
        if (worldCamera == null)
        {
            mouseWorld = Vector3.zero;
            return false;
        }

        // Our grid lives at z = 0; camera is at some negative Z
        float camToPlane = -worldCamera.transform.position.z;

        if (camToPlane <= 0f)
        {
            // If camera ever ends up in front of the grid, this would be bad.
            camToPlane = 10f;
        }

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = camToPlane;

        mouseWorld = worldCamera.ScreenToWorldPoint(screenPos);
        return true;
    }

    private bool WorldToCell(Vector3 worldPos, out int cellX, out int cellY)
    {
        // local coordinates relative to bottom-left of grid
        float localX = worldPos.x - left;
        float localY = worldPos.y - bottom;

        cellX = Mathf.FloorToInt(localX / gridCamera.cellSize);
        cellY = Mathf.FloorToInt(localY / gridCamera.cellSize);

        // inside grid?
        if (cellX < 0 || cellX >= gridCamera.gridWidth ||
            cellY < 0 || cellY >= gridCamera.gridHeight)
        {
            return false;
        }

        return true;
    }

    private Vector3 CellToWorldCenter(int cellX, int cellY)
    {
        float x = left + (cellX + 0.5f) * gridCamera.cellSize;
        float y = bottom + (cellY + 0.5f) * gridCamera.cellSize;
        return new Vector3(x, y, 0f);
    }

    // for visualizing cell centers in editor (optional)
    private void OnDrawGizmosSelected()
    {
        if (gridCamera == null) return;

        float w = gridCamera.gridWidth * gridCamera.cellSize;
        float h = gridCamera.gridHeight * gridCamera.cellSize;
        float l = -w * 0.5f;
        float b = -h * 0.5f;

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(w, h, 0f));

        Gizmos.color = new Color(0f, 1f, 0f, 0.05f);
        for (int x = 0; x < gridCamera.gridWidth; x++)
        {
            for (int y = 0; y < gridCamera.gridHeight; y++)
            {
                Vector3 c = new Vector3(
                    l + (x + 0.5f) * gridCamera.cellSize,
                    b + (y + 0.5f) * gridCamera.cellSize,
                    0f
                );
                Gizmos.DrawSphere(c, gridCamera.cellSize * 0.1f);
            }
        }
    }
}
