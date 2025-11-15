using System.Collections.Generic;
using UnityEngine;

public class GridPlacementSystem : MonoBehaviour
{
    // ===================== REFERENCES =====================
    [Header("References")]
    [Tooltip("Grid camera that defines the grid size, cell size, etc.")]
    public ResizableGridCamera gridCamera;

    [Tooltip("Camera used to convert mouse position to world space. Defaults to Camera.main if null.")]
    public Camera worldCamera;

    [Tooltip("Inventory that defines which blocks exist and which one is selected.")]
    public BuildInventory buildInventory;

    // ===================== PREFABS & VISUALS =====================
    [Header("Placement Prefabs (Fallbacks)")]
    [Tooltip("Fallback prefab to place if no BuildInventory is assigned. Should have PlaceableObject on it.")]
    public GameObject placeablePrefab;   // for quick testing without inventory

    [Tooltip("Ghost/preview prefab (usually a 1x1 highlight sprite).")]
    public GameObject previewPrefab;

    [Tooltip("Optional parent transform for all placed objects. Leave null to keep them at root.")]
    public Transform placementParent;

    [Tooltip("Z position in world space where placed objects should live. The grid is drawn at z = 0.")]
    public float placementZ = 0f;

    [Header("Preview Settings")]
    [Tooltip("Sorting order for preview sprites so they always render on top.")]
    public int previewSortingOrder = 1000;

    [Tooltip("Color for preview tiles when placement is valid.")]
    public Color previewCanPlaceColor = new Color(0f, 0.6f, 1f, 0.5f); // bluish

    [Tooltip("Color for preview tiles when placement is blocked (occupied / erase).")]
    public Color previewBlockedColor = new Color(1f, 0f, 0f, 0.5f);   // red

    // ===================== BEHAVIOR OPTIONS =====================
    [Header("Line Placement Options")]
    [Tooltip("If true, ctrl-line placement will continue past blocked cells (skipping the blocked ones). If false, the line stops at the first blocked cell.")]
    public bool continuePastBlockedCells = true;

    // ===================== INPUT / KEYBINDS =====================
    [Header("Keybinds - Mouse Buttons")]
    [Tooltip("Mouse button index used to PLACE tiles. 0 = left, 1 = right, 2 = middle.")]
    [Range(0, 2)] public int placeMouseButton = 0;

    [Tooltip("Mouse button index used to ERASE tiles. 0 = left, 1 = right, 2 = middle.")]
    [Range(0, 2)] public int eraseMouseButton = 1;

    [Header("Keybinds - Modifiers")]
    [Tooltip("Primary modifier for LINE and RECTANGLE modes (e.g. LeftControl).")]
    public KeyCode lineModifierKeyPrimary = KeyCode.LeftControl;

    [Tooltip("Alternate modifier for LINE and RECTANGLE modes (e.g. RightControl).")]
    public KeyCode lineModifierKeyAlt = KeyCode.RightControl;

    [Tooltip("Primary extra modifier for RECTANGLE mode (used together with line modifier, e.g. LeftShift).")]
    public KeyCode rectModifierKeyPrimary = KeyCode.LeftShift;

    [Tooltip("Alternate extra modifier for RECTANGLE mode (used together with line modifier, e.g. RightShift).")]
    public KeyCode rectModifierKeyAlt = KeyCode.RightShift;

    // ===================== INTERNAL STATE =====================
    private GameObject previewInstance;
    private SpriteRenderer previewRenderer;

    // store actual objects per cell
    private GameObject[,] placedObjects;     // [x, y] world objects

    private float totalWidth;
    private float totalHeight;
    private float left;
    private float bottom;

    // drag-placement / erasing state
    private bool isPlacingDrag = false;
    private bool isErasingDrag = false;

    // line modes
    private bool ctrlPlacementMode = false;        // line place for this drag?
    private bool ctrlEraseMode = false;            // line erase for this drag?

    // rectangle modes
    private bool rectPlacementMode = false;        // rect place for this drag?
    private bool rectEraseMode = false;            // rect erase for this drag?

    private Vector2Int dragStartCell;
    private HashSet<Vector2Int> cellsModifiedThisDrag = new HashSet<Vector2Int>();

    // area preview pool (used for both line & rectangle)
    private readonly List<GameObject> areaPreviewPool = new List<GameObject>();

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

        // main single-cell preview instance
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

    // ===================== HELPERS: CURRENT BLOCK =====================

    private PlaceableObject GetCurrentPlaceableDefinition()
    {
        // Prefer inventory-selected block
        if (buildInventory != null && buildInventory.SelectedBlock != null &&
            buildInventory.SelectedBlock.placeablePrefab != null)
        {
            return buildInventory.SelectedBlock.placeablePrefab;
        }

        // Fallback to single prefab field
        if (placeablePrefab != null)
        {
            return placeablePrefab.GetComponent<PlaceableObject>();
        }

        return null;
    }

    private GameObject GetCurrentPlaceablePrefabGO()
    {
        var def = GetCurrentPlaceableDefinition();
        return def != null ? def.gameObject : null;
    }

    // ===================== HELPERS: KEYBINDS =====================
    private bool IsKeyHeld(KeyCode primary, KeyCode alt)
    {
        return (primary != KeyCode.None && Input.GetKey(primary)) ||
               (alt != KeyCode.None && Input.GetKey(alt));
    }

    // ===================== AREA PREVIEW POOL =====================
    private GameObject GetAreaPreviewInstance(int index)
    {
        // Grow pool if needed
        while (areaPreviewPool.Count <= index)
        {
            GameObject inst = Instantiate(previewPrefab);
            var rend = inst.GetComponentInChildren<SpriteRenderer>();
            if (rend != null)
            {
                rend.sortingOrder = previewSortingOrder;
            }
            inst.SetActive(false);
            areaPreviewPool.Add(inst);
        }
        return areaPreviewPool[index];
    }

    private void DeactivateAllAreaPreviews()
    {
        for (int i = 0; i < areaPreviewPool.Count; i++)
        {
            if (areaPreviewPool[i] != null && areaPreviewPool[i].activeSelf)
                areaPreviewPool[i].SetActive(false);
        }
    }

    // ===================== PREVIEW / HOVER LOGIC =====================
    private void HandlePreview()
    {
        PlaceableObject currentDef = GetCurrentPlaceableDefinition();

        bool showRectPreview = (isPlacingDrag && rectPlacementMode) ||
                               (isErasingDrag && rectEraseMode);

        bool showLinePreview = (isPlacingDrag && ctrlPlacementMode) ||
                               (isErasingDrag && ctrlEraseMode);

        // --- Rectangle preview (modifier + extra) ---
        if (showRectPreview && previewPrefab != null)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
            {
                SetPreviewVisible(false);
                DeactivateAllAreaPreviews();
                return;
            }

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
            {
                SetPreviewVisible(false);
                DeactivateAllAreaPreviews();
                return;
            }

            Vector2Int currentCell = new Vector2Int(cellX, cellY);

            // build axis-aligned rectangle between dragStartCell and currentCell
            List<Vector2Int> rectCells = GetRectCells(dragStartCell, currentCell);

            // For placement: blue if all free, red if any overlap
            // For erase: always red
            Color areaColor = previewBlockedColor;

            if (isPlacingDrag && rectPlacementMode)
            {
                bool allFree = true;
                if (currentDef != null)
                {
                    foreach (var anchor in rectCells)
                    {
                        if (!CanPlaceAtAnchorCell(anchor, currentDef))
                        {
                            allFree = false;
                            break;
                        }
                    }
                }

                areaColor = allFree ? previewCanPlaceColor : previewBlockedColor;
            }
            else
            {
                // Rect erase -> always red
                areaColor = previewBlockedColor;
            }

            // Use pool instead of destroy/instantiate
            DeactivateAllAreaPreviews();
            for (int i = 0; i < rectCells.Count; i++)
            {
                Vector2Int c = rectCells[i];
                GameObject inst = GetAreaPreviewInstance(i);
                if (!inst.activeSelf) inst.SetActive(true);

                var rend = inst.GetComponentInChildren<SpriteRenderer>();
                if (rend != null)
                {
                    rend.color = areaColor;
                }

                Vector3 center = CellToWorldCenter(c.x, c.y);
                inst.transform.position = new Vector3(center.x, center.y, placementZ);
            }

            SetPreviewVisible(false);
            return;
        }

        // --- Line preview (modifier only) ---
        if (showLinePreview && previewPrefab != null)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
            {
                SetPreviewVisible(false);
                DeactivateAllAreaPreviews();
                return;
            }

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
            {
                SetPreviewVisible(false);
                DeactivateAllAreaPreviews();
                return;
            }

            Vector2Int currentCell = new Vector2Int(cellX, cellY);
            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, currentCell);

            // collect all cells along the snapped line
            List<Vector2Int> lineCells = new List<Vector2Int>();
            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                lineCells.Add(c);

            DeactivateAllAreaPreviews();

            if (isPlacingDrag && ctrlPlacementMode)
            {
                // Per-cell colors: blue for free, red for blocked.
                // Optionally stop preview past first blocked cell.
                bool blockingFound = false;
                int poolIndex = 0;

                foreach (var anchor in lineCells)
                {
                    if (!continuePastBlockedCells && blockingFound)
                        break;

                    bool canPlace = currentDef != null && CanPlaceAtAnchorCell(anchor, currentDef);
                    Color color;

                    if (!canPlace)
                    {
                        color = previewBlockedColor;
                        blockingFound = true;
                    }
                    else
                    {
                        color = previewCanPlaceColor;
                    }

                    GameObject inst = GetAreaPreviewInstance(poolIndex++);
                    if (!inst.activeSelf) inst.SetActive(true);

                    var rend = inst.GetComponentInChildren<SpriteRenderer>();
                    if (rend != null)
                    {
                        rend.color = color;
                    }

                    Vector3 center = CellToWorldCenter(anchor.x, anchor.y);
                    inst.transform.position = new Vector3(center.x, center.y, placementZ);
                }
            }
            else
            {
                // Erase preview: solid red line
                int poolIndex = 0;
                foreach (var cell in lineCells)
                {
                    GameObject inst = GetAreaPreviewInstance(poolIndex++);
                    if (!inst.activeSelf) inst.SetActive(true);

                    var rend = inst.GetComponentInChildren<SpriteRenderer>();
                    if (rend != null)
                    {
                        rend.color = previewBlockedColor;
                    }

                    Vector3 center = CellToWorldCenter(cell.x, cell.y);
                    inst.transform.position = new Vector3(center.x, center.y, placementZ);
                }
            }

            // hide single-cell preview while showing line
            SetPreviewVisible(false);
            return;
        }

        // --- Not in rect/line drag: single-cell hover preview ---
        DeactivateAllAreaPreviews();

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

        Vector2Int anchorCell = new Vector2Int(cellX2, cellY2);
        Vector3 cellCenter2 = CellToWorldCenter(cellX2, cellY2);
        previewInstance.transform.position = new Vector3(
            cellCenter2.x,
            cellCenter2.y,
            placementZ
        );

        bool canSinglePlace = currentDef != null && CanPlaceAtAnchorCell(anchorCell, currentDef);

        SetPreviewVisible(true);
        SetPreviewColor(canSinglePlace ? previewCanPlaceColor : previewBlockedColor);
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

    // ===================== PLACEMENT + ERASING =====================
    private void HandlePlacementAndErasing()
    {
        PlaceableObject currentDef = GetCurrentPlaceableDefinition();
        if (currentDef == null && placeablePrefab == null)
            return;

        bool lineModHeld = IsKeyHeld(lineModifierKeyPrimary, lineModifierKeyAlt);
        bool rectModHeld = IsKeyHeld(rectModifierKeyPrimary, rectModifierKeyAlt);
        bool rectModeHeld = lineModHeld && rectModHeld;

        // --- LEFT MOUSE: place / paint / line / rect-place ---
        if (Input.GetMouseButtonDown(placeMouseButton))
        {
            isPlacingDrag = true;
            isErasingDrag = false;
            cellsModifiedThisDrag.Clear();

            // priority: rectangle (line+rect) > line (line only) > free
            rectPlacementMode = rectModeHeld;
            ctrlPlacementMode = !rectPlacementMode && lineModHeld;

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

        if (Input.GetMouseButton(placeMouseButton) && isPlacingDrag)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
                return;

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
                return; // outside grid

            if (rectPlacementMode)
            {
                // Rect-place: NO placement while dragging, preview only
            }
            else if (ctrlPlacementMode)
            {
                // Line-place: NO placement while dragging, preview only
            }
            else
            {
                // normal free painting (per-cell under cursor)
                Vector2Int cell = new Vector2Int(cellX, cellY);
                TryPlaceAtAnchorOncePerDrag(cell);
            }
        }

        if (Input.GetMouseButtonUp(placeMouseButton))
        {
            if (isPlacingDrag)
            {
                Vector3 mouseWorld;
                if (TryGetMouseWorldOnGridPlane(out mouseWorld))
                {
                    int cellX, cellY;
                    if (WorldToCell(mouseWorld, out cellX, out cellY))
                    {
                        Vector2Int endCell = new Vector2Int(cellX, cellY);

                        if (rectPlacementMode)
                        {
                            // place rectangle
                            List<Vector2Int> rectCells = GetRectCells(dragStartCell, endCell);
                            foreach (var c in rectCells)
                            {
                                TryPlaceAtAnchorOncePerDrag(c);
                            }
                        }
                        else if (ctrlPlacementMode)
                        {
                            // place snapped line, respecting continuePastBlockedCells
                            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, endCell);
                            bool blockingFound = false;

                            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                            {
                                if (!continuePastBlockedCells && blockingFound)
                                    break;

                                if (!TryPlaceAtAnchorOncePerDrag(c))
                                {
                                    // If it failed due to blocking, mark it
                                    PlaceableObject def = GetCurrentPlaceableDefinition();
                                    if (def != null && !CanPlaceAtAnchorCell(c, def))
                                        blockingFound = true;
                                }
                            }
                        }
                    }
                }
            }

            isPlacingDrag = false;
            ctrlPlacementMode = false;
            rectPlacementMode = false;
            cellsModifiedThisDrag.Clear();
            DeactivateAllAreaPreviews();
        }

        // --- RIGHT MOUSE (or custom): erase / line-erase / rect-erase ---
        if (Input.GetMouseButtonDown(eraseMouseButton))
        {
            isErasingDrag = true;
            isPlacingDrag = false;
            ctrlPlacementMode = false;
            rectPlacementMode = false;
            cellsModifiedThisDrag.Clear();

            rectEraseMode = rectModeHeld;
            ctrlEraseMode = !rectEraseMode && lineModHeld;

            // remember start cell for line/rect erase
            if (ctrlEraseMode || rectEraseMode)
            {
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
        }

        if (Input.GetMouseButton(eraseMouseButton) && isErasingDrag)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
                return;

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
                return;

            if (rectEraseMode || ctrlEraseMode)
            {
                // line/rect erase: preview only while dragging
            }
            else
            {
                // normal free erase
                Vector2Int cell = new Vector2Int(cellX, cellY);
                TryEraseCellOncePerDrag(cell);
            }
        }

        if (Input.GetMouseButtonUp(eraseMouseButton))
        {
            if (isErasingDrag)
            {
                Vector3 mouseWorld;
                if (TryGetMouseWorldOnGridPlane(out mouseWorld))
                {
                    int cellX, cellY;
                    if (WorldToCell(mouseWorld, out cellX, out cellY))
                    {
                        Vector2Int endCell = new Vector2Int(cellX, cellY);

                        if (rectEraseMode)
                        {
                            // erase rectangle
                            List<Vector2Int> rectCells = GetRectCells(dragStartCell, endCell);
                            foreach (var c in rectCells)
                            {
                                TryEraseCellOncePerDrag(c);
                            }
                        }
                        else if (ctrlEraseMode)
                        {
                            // erase snapped line (full line)
                            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, endCell);
                            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                            {
                                TryEraseCellOncePerDrag(c);
                            }
                        }
                    }
                }
            }

            isErasingDrag = false;
            ctrlEraseMode = false;
            rectEraseMode = false;
            cellsModifiedThisDrag.Clear();
            DeactivateAllAreaPreviews();
        }
    }

    // ===== PLACEMENT / ERASE HELPERS (multi-cell aware) =====

    private bool TryPlaceAtAnchorOncePerDrag(Vector2Int anchorCell)
    {
        if (cellsModifiedThisDrag.Contains(anchorCell))
            return false;

        cellsModifiedThisDrag.Add(anchorCell);

        PlaceableObject def = GetCurrentPlaceableDefinition();
        if (def == null) return false;

        // inventory check (if we have one)
        if (buildInventory != null && !buildInventory.CanPlaceSelected(1))
            return false;

        if (!CanPlaceAtAnchorCell(anchorCell, def))
            return false;

        PlaceAtAnchorCell(anchorCell, def);
        return true;
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

    private bool CanPlaceAtAnchorCell(Vector2Int anchorCell, PlaceableObject def)
    {
        foreach (Vector2Int cell in def.GetOccupiedCells(anchorCell))
        {
            // inside grid bounds?
            if (cell.x < 0 || cell.x >= gridCamera.gridWidth ||
                cell.y < 0 || cell.y >= gridCamera.gridHeight)
            {
                return false;
            }

            if (def.blocksPlacement && IsCellOccupied(cell.x, cell.y))
            {
                return false;
            }
        }
        return true;
    }

    private void PlaceAtAnchorCell(Vector2Int anchorCell, PlaceableObject def)
    {
        GameObject prefab = GetCurrentPlaceablePrefabGO();
        if (prefab == null) return;

        // world position: assume prefab pivot is aligned to anchor cell
        Vector3 anchorWorld = CellToWorldCenter(anchorCell.x, anchorCell.y);

        GameObject placed = Instantiate(
            prefab,
            new Vector3(anchorWorld.x, anchorWorld.y, placementZ),
            Quaternion.identity
        );

        if (placementParent != null)
        {
            placed.transform.SetParent(placementParent, true);
        }

        // mark all occupied cells
        foreach (Vector2Int cell in def.GetOccupiedCells(anchorCell))
        {
            placedObjects[cell.x, cell.y] = placed;
        }

        // consume inventory
        if (buildInventory != null)
        {
            buildInventory.TryConsumeSelected(1);
        }
    }

    private void EraseAtCell(int cellX, int cellY)
    {
        GameObject obj = placedObjects[cellX, cellY];
        if (obj == null) return;

        // REFUND inventory once per object
        if (buildInventory != null)
        {
            PlaceableObject po = obj.GetComponent<PlaceableObject>();
            if (po != null && !string.IsNullOrEmpty(po.id))
            {
                buildInventory.AddToBlock(po.id, 1);
            }
        }

        // Clear ALL cells that reference this same object
        for (int x = 0; x < gridCamera.gridWidth; x++)
        {
            for (int y = 0; y < gridCamera.gridHeight; y++)
            {
                if (placedObjects[x, y] == obj)
                {
                    placedObjects[x, y] = null;
                }
            }
        }

        Destroy(obj);
    }

    // ===================== RECTANGLE & LINE HELPERS =====================
    private List<Vector2Int> GetRectCells(Vector2Int a, Vector2Int b)
    {
        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        List<Vector2Int> result = new List<Vector2Int>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                // clamp to grid just in case
                if (x < 0 || x >= gridCamera.gridWidth ||
                    y < 0 || y >= gridCamera.gridHeight)
                    continue;

                result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

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

    // ===================== MOUSE ↔ GRID HELPERS =====================
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

    // optional gizmos
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
