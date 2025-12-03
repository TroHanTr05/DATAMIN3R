using System.Collections.Generic;
using UnityEngine;

public class GridPlacementSystem : MonoBehaviour
{
    public static GridPlacementSystem Instance; // For conveyors
    
    
    // ===================== REFERENCES =====================
    [Header("References")]
    [Tooltip("Grid camera that defines the grid size, cell size, etc.")]
    public ResizableGridCamera gridCamera;

    [Tooltip("Camera used to convert mouse position to world space. Defaults to Camera.main if null.")]
    public Camera worldCamera;

    [Header("Inventory / Catalog")]
    [Tooltip("Runtime player inventory with counts per block.")]
    public PlayerInventory playerInventory;


    [Header("Fallback Prefabs (used if no block from inventory)")]
    [Tooltip("Fallback prefab when no active block is available. Only used if you want dev infinite placement.")]
    public GameObject fallbackPlaceablePrefab;

    [Tooltip("Fallback ghost/preview prefab when no active block is available.")]
    public GameObject fallbackPreviewPrefab;

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
    public bool continuePastBlockedCells = true;

    // ===================== INPUT / KEYBINDS =====================
    [Header("Keybinds - Mouse Buttons")]
    [Range(0, 2)] public int placeMouseButton = 0;
    [Range(0, 2)] public int eraseMouseButton = 1;

    [Header("Keybinds - Modifiers")]
    public KeyCode lineModifierKeyPrimary = KeyCode.LeftControl;
    public KeyCode lineModifierKeyAlt = KeyCode.RightControl;
    public KeyCode rectModifierKeyPrimary = KeyCode.LeftShift;
    public KeyCode rectModifierKeyAlt = KeyCode.RightShift;

    // ===================== INTERNAL STATE =====================
    private GameObject previewInstance;
    private SpriteRenderer previewRenderer;

    private GameObject[,] placedObjects;

    private float totalWidth;
    private float totalHeight;
    private float left;
    private float bottom;

    private bool isPlacingDrag = false;
    private bool isErasingDrag = false;

    private bool ctrlPlacementMode = false;
    private bool ctrlEraseMode = false;

    private bool rectPlacementMode = false;
    private bool rectEraseMode = false;

    private Vector2Int dragStartCell;
    private HashSet<Vector2Int> cellsModifiedThisDrag = new HashSet<Vector2Int>();

    private readonly List<GameObject> areaPreviewPool = new List<GameObject>();

    private BuildBlock lastSelectedBlock;

    // ===================== UNITY =====================

    void Start()
    {
        Instance = this; // For conveyors
        
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

        // Auto-find player inventory if not assigned
        if (playerInventory == null)
        {
            playerInventory = FindObjectOfType<PlayerInventory>();
            if (playerInventory == null)
            {
                Debug.LogWarning("GridPlacementSystem: No PlayerInventory found. Placement will be infinite / dev-mode.");
            }
        }

        totalWidth = gridCamera.gridWidth * gridCamera.cellSize;
        totalHeight = gridCamera.gridHeight * gridCamera.cellSize;

        left = -totalWidth * 0.5f;
        bottom = -totalHeight * 0.5f;

        placedObjects = new GameObject[gridCamera.gridWidth, gridCamera.gridHeight];

        RebuildPreviewForSelectedBlock();
    }

    void Update()
    {
        BuildBlock current = GetActiveBlock();
        if (current != lastSelectedBlock)
        {
            RebuildPreviewForSelectedBlock();
        }

        HandlePreview();
        HandlePlacementAndErasing();
    }

    // ===================== ACTIVE BLOCK / PREVIEW =====================

    private BuildBlock GetActiveBlock()
    {
        if (playerInventory != null && playerInventory.SelectedBlock != null)
            return playerInventory.SelectedBlock;

        return null;
    }

    private GameObject GetActivePlaceablePrefab(BuildBlock block)
    {
        if (block != null && block.placeablePrefab != null)
            return block.placeablePrefab;

        return fallbackPlaceablePrefab;
    }

    private GameObject GetActivePreviewPrefab(BuildBlock block)
    {
        if (block != null)
        {
            if (block.previewPrefab != null)
                return block.previewPrefab;
            if (block.placeablePrefab != null)
                return block.placeablePrefab;
        }

        return fallbackPreviewPrefab != null ? fallbackPreviewPrefab : fallbackPlaceablePrefab;
    }

    private void RebuildPreviewForSelectedBlock()
    {
        lastSelectedBlock = GetActiveBlock();

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
            previewRenderer = null;
        }

        GameObject prefab = GetActivePreviewPrefab(lastSelectedBlock);
        if (prefab == null)
            return;

        previewInstance = Instantiate(prefab);
        previewRenderer = previewInstance.GetComponentInChildren<SpriteRenderer>();
        if (previewRenderer != null)
        {
            previewRenderer.sortingOrder = previewSortingOrder;
            previewRenderer.color = previewCanPlaceColor;
        }
        SetPreviewVisible(false);

        foreach (var go in areaPreviewPool)
        {
            if (go != null) Destroy(go);
        }
        areaPreviewPool.Clear();
    }

    private bool IsKeyHeld(KeyCode primary, KeyCode alt)
    {
        return (primary != KeyCode.None && Input.GetKey(primary)) ||
               (alt != KeyCode.None && Input.GetKey(alt));
    }

    private GameObject GetAreaPreviewInstance(int index)
    {
        GameObject basePrefab = GetActivePreviewPrefab(GetActiveBlock());
        if (basePrefab == null) basePrefab = fallbackPlaceablePrefab;
        if (basePrefab == null) return null;

        while (areaPreviewPool.Count <= index)
        {
            GameObject inst = Instantiate(basePrefab);
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

    // ===================== PREVIEW / HOVER =====================

    private void HandlePreview()
    {
        BuildBlock activeBlock = GetActiveBlock();

        bool showRectPreview = (isPlacingDrag && rectPlacementMode) ||
                               (isErasingDrag && rectEraseMode);

        bool showLinePreview = (isPlacingDrag && ctrlPlacementMode) ||
                               (isErasingDrag && ctrlEraseMode);

        // --- Rectangle preview ---
        if (showRectPreview)
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
            List<Vector2Int> rectCells = GetRectCells(dragStartCell, currentCell);

            Color areaColor = previewBlockedColor;

            if (isPlacingDrag && rectPlacementMode)
            {
                bool allFree = true;
                foreach (var c in rectCells)
                {
                    if (IsCellOccupied(c.x, c.y))
                    {
                        allFree = false;
                        break;
                    }
                }

                areaColor = allFree ? previewCanPlaceColor : previewBlockedColor;
            }

            DeactivateAllAreaPreviews();
            for (int i = 0; i < rectCells.Count; i++)
            {
                Vector2Int c = rectCells[i];
                GameObject inst = GetAreaPreviewInstance(i);
                if (inst == null) continue;
                if (!inst.activeSelf) inst.SetActive(true);

                var rend = inst.GetComponentInChildren<SpriteRenderer>();
                if (rend != null) rend.color = areaColor;

                Vector3 center = CellToWorldCenter(c.x, c.y);
                inst.transform.position = new Vector3(center.x, center.y, placementZ);
            }

            SetPreviewVisible(false);
            return;
        }

        // --- Line preview ---
        if (showLinePreview)
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

            List<Vector2Int> lineCells = new List<Vector2Int>();
            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                lineCells.Add(c);

            DeactivateAllAreaPreviews();

            int poolIndex = 0;

            if (isPlacingDrag && ctrlPlacementMode)
            {
                bool blockingFound = false;

                foreach (var cell in lineCells)
                {
                    if (!continuePastBlockedCells && blockingFound)
                        break;

                    bool occupied = IsCellOccupied(cell.x, cell.y);
                    Color color;

                    if (occupied)
                    {
                        color = previewBlockedColor;
                        blockingFound = true;
                    }
                    else
                    {
                        color = previewCanPlaceColor;
                    }

                    GameObject inst = GetAreaPreviewInstance(poolIndex++);
                    if (inst == null) continue;
                    if (!inst.activeSelf) inst.SetActive(true);

                    var rend = inst.GetComponentInChildren<SpriteRenderer>();
                    if (rend != null) rend.color = color;

                    Vector3 center = CellToWorldCenter(cell.x, cell.y);
                    inst.transform.position = new Vector3(center.x, center.y, placementZ);
                }
            }
            else
            {
                foreach (var cell in lineCells)
                {
                    GameObject inst = GetAreaPreviewInstance(poolIndex++);
                    if (inst == null) continue;
                    if (!inst.activeSelf) inst.SetActive(true);

                    var rend = inst.GetComponentInChildren<SpriteRenderer>();
                    if (rend != null) rend.color = previewBlockedColor;

                    Vector3 center = CellToWorldCenter(cell.x, cell.y);
                    inst.transform.position = new Vector3(center.x, center.y, placementZ);
                }
            }

            SetPreviewVisible(false);
            return;
        }

        // --- Single-cell hover preview ---
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
            return;
        }

        Vector3 cellCenter2 = CellToWorldCenter(cellX2, cellY2);
        previewInstance.transform.position = new Vector3(cellCenter2.x, cellCenter2.y, placementZ);

        bool hasRequired = true;
        BuildBlock block = GetActiveBlock();
        if (playerInventory != null && block != null)
        {
            hasRequired = playerInventory.HasBlock(block, 1);
        }

        bool canPlace = !IsCellOccupied(cellX2, cellY2) &&
                        (GetActivePlaceablePrefab(block) != null) &&
                        hasRequired;

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

    // ===================== PLACEMENT + ERASING =====================

    private void HandlePlacementAndErasing()
    {
        BuildBlock activeBlock = GetActiveBlock();
        GameObject activePlaceablePrefab = GetActivePlaceablePrefab(activeBlock);

        // No active block and no fallback: nothing to place.
        if (activeBlock == null && activePlaceablePrefab == null)
            return;

        bool lineModHeld = IsKeyHeld(lineModifierKeyPrimary, lineModifierKeyAlt);
        bool rectModHeld = IsKeyHeld(rectModifierKeyPrimary, rectModifierKeyAlt);
        bool rectModeHeld = lineModHeld && rectModHeld;

        // ---- PLACE ----
        if (Input.GetMouseButtonDown(placeMouseButton))
        {
            isPlacingDrag = true;
            isErasingDrag = false;
            cellsModifiedThisDrag.Clear();

            rectPlacementMode = rectModeHeld;
            ctrlPlacementMode = !rectPlacementMode && lineModHeld;

            Vector3 mouseWorld;
            if (TryGetMouseWorldOnGridPlane(out mouseWorld))
            {
                int cx, cy;
                if (WorldToCell(mouseWorld, out cx, out cy))
                    dragStartCell = new Vector2Int(cx, cy);
            }
        }

        if (Input.GetMouseButton(placeMouseButton) && isPlacingDrag)
        {
            Vector3 mouseWorld;
            if (!TryGetMouseWorldOnGridPlane(out mouseWorld))
                return;

            int cellX, cellY;
            if (!WorldToCell(mouseWorld, out cellX, out cellY))
                return;

            if (!rectPlacementMode && !ctrlPlacementMode)
            {
                Vector2Int cell = new Vector2Int(cellX, cellY);
                TryPlaceAtCellOncePerDrag(cell, activeBlock, activePlaceablePrefab);
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
                            List<Vector2Int> rectCells = GetRectCells(dragStartCell, endCell);
                            foreach (var c in rectCells)
                                TryPlaceAtCellOncePerDrag(c, activeBlock, activePlaceablePrefab);
                        }
                        else if (ctrlPlacementMode)
                        {
                            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, endCell);
                            bool blockingFound = false;

                            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                            {
                                if (!continuePastBlockedCells && blockingFound)
                                    break;

                                if (IsCellOccupied(c.x, c.y))
                                {
                                    blockingFound = true;
                                    continue;
                                }

                                TryPlaceAtCellOncePerDrag(c, activeBlock, activePlaceablePrefab);
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

        // ---- ERASE ----
        if (Input.GetMouseButtonDown(eraseMouseButton))
        {
            isErasingDrag = true;
            isPlacingDrag = false;
            ctrlPlacementMode = false;
            rectPlacementMode = false;
            cellsModifiedThisDrag.Clear();

            rectEraseMode = rectModeHeld;
            ctrlEraseMode = !rectEraseMode && lineModHeld;

            if (ctrlEraseMode || rectEraseMode)
            {
                Vector3 mouseWorld;
                if (TryGetMouseWorldOnGridPlane(out mouseWorld))
                {
                    int cx, cy;
                    if (WorldToCell(mouseWorld, out cx, out cy))
                        dragStartCell = new Vector2Int(cx, cy);
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

            if (!rectEraseMode && !ctrlEraseMode)
            {
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
                            List<Vector2Int> rectCells = GetRectCells(dragStartCell, endCell);
                            foreach (var c in rectCells)
                                TryEraseCellOncePerDrag(c);
                        }
                        else if (ctrlEraseMode)
                        {
                            Vector2Int snappedEnd = ConstrainTo45DegreeLine(dragStartCell, endCell);
                            foreach (var c in GetLineCells(dragStartCell, snappedEnd))
                                TryEraseCellOncePerDrag(c);
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

    private void TryPlaceAtCellOncePerDrag(Vector2Int cell, BuildBlock block, GameObject prefab)
    {
        if (cellsModifiedThisDrag.Contains(cell))
            return;

        cellsModifiedThisDrag.Add(cell);

        int x = cell.x;
        int y = cell.y;

        if (IsCellOccupied(x, y))
            return;

        if (playerInventory != null && block != null)
        {
            if (!playerInventory.TryConsume(block, 1))
                return; // not enough or failed
        }

        PlaceAtCell(x, y, prefab, block);
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

    private void PlaceAtCell(int cellX, int cellY, GameObject prefab, BuildBlock blockDef)
    {
        if (prefab == null) return;

        Vector3 cellCenter = CellToWorldCenter(cellX, cellY);

        GameObject placed = Instantiate(
            prefab,
            new Vector3(cellCenter.x, cellCenter.y, placementZ),
            Quaternion.identity
        );

        if (placementParent != null)
        {
            placed.transform.SetParent(placementParent, true);
        }

        var po = placed.GetComponent<PlaceableObject>();
        if (po == null)
        {
            po = placed.AddComponent<PlaceableObject>();
        }
        po.blockDefinition = blockDef;
        
        PowerManager.Instance?.RegisterPlaceable(po); // ***Added for Power Manager***

        placedObjects[cellX, cellY] = placed;
    }

    private void EraseAtCell(int cellX, int cellY)
    {
        GameObject obj = placedObjects[cellX, cellY];
        if (obj != null)
        {
            var po = obj.GetComponent<PlaceableObject>();
            if (po != null && po.blockDefinition != null && playerInventory != null)
            {
                playerInventory.TryAddBlock(po.blockDefinition, 1);
            }
            
            PowerManager.Instance?.UnregisterPlaceable(po); // ***Added for Power Manager***

            Destroy(obj);
            placedObjects[cellX, cellY] = null;
        }
    }

    // ===================== RECT & LINE HELPERS =====================

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
                if (x < 0 || x >= gridCamera.gridWidth ||
                    y < 0 || y >= gridCamera.gridHeight)
                    continue;

                result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

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
            dir = new Vector2Int(dx > 0 ? 1 : -1, 0);
        else if (absDy > absDx * 2f)
            dir = new Vector2Int(0, dy > 0 ? 1 : -1);
        else
            dir = new Vector2Int(dx > 0 ? 1 : -1, dy > 0 ? 1 : -1);

        int length = Mathf.RoundToInt(Mathf.Max(absDx, absDy));

        Vector2Int snappedEnd = start + dir * length;

        snappedEnd.x = Mathf.Clamp(snappedEnd.x, 0, gridCamera.gridWidth - 1);
        snappedEnd.y = Mathf.Clamp(snappedEnd.y, 0, gridCamera.gridHeight - 1);

        return snappedEnd;
    }

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

    // ===================== MOUSE ↔ GRID =====================

    private bool TryGetMouseWorldOnGridPlane(out Vector3 mouseWorld)
    {
        if (worldCamera == null)
        {
            mouseWorld = Vector3.zero;
            return false;
        }

        float camToPlane = -worldCamera.transform.position.z;
        if (camToPlane <= 0f) camToPlane = 10f;

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = camToPlane;

        mouseWorld = worldCamera.ScreenToWorldPoint(screenPos);
        return true;
    }

    private bool WorldToCell(Vector3 worldPos, out int cellX, out int cellY)
    {
        float localX = worldPos.x - left;
        float localY = worldPos.y - bottom;

        cellX = Mathf.FloorToInt(localX / gridCamera.cellSize);
        cellY = Mathf.FloorToInt(localY / gridCamera.cellSize);

        if (cellX < 0 || cellX >= gridCamera.gridWidth ||
            cellY < 0 || cellY >= gridCamera.gridHeight)
            return false;

        return true;
    }

    private Vector3 CellToWorldCenter(int cellX, int cellY)
    {
        float x = left + (cellX + 0.5f) * gridCamera.cellSize;
        float y = bottom + (cellY + 0.5f) * gridCamera.cellSize;
        return new Vector3(x, y, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (gridCamera == null) return;

        float w = gridCamera.gridWidth *
                 gridCamera.cellSize;
        float h = gridCamera.gridHeight *
                 gridCamera.cellSize;
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
    
    // ===================== GRID PATCH FOR CONVEYORS =====================
    
    // Returns true if the given grid coordinate is inside the valid grid range
    public bool IsCellInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.x < gridCamera.gridWidth &&
               cell.y >= 0 &&
               cell.y < gridCamera.gridHeight;
    }
    
    // Exposes the placedObjects array so other systems (Conveyors, Machines)
    // can check what is occupying a cell.
    public GameObject GetPlacedObject(Vector2Int cell)
    {
        if (!IsCellInsideGrid(cell))
            return null;
    
        return placedObjects[cell.x, cell.y];
    }
    
    // Converts a world position into the grid cell index.
    // Wrapper version of WorldToCell for convenience.
    public Vector2Int WorldToCellPosition(Vector3 worldPosition)
    {
        int cx, cy;
        if (WorldToCell(worldPosition, out cx, out cy))
            return new Vector2Int(cx, cy);
    
        return new Vector2Int(-9999, -9999); // invalid cell indicator
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.x < gridCamera.gridWidth &&
               cell.y >= 0 &&
               cell.y < gridCamera.gridHeight;
    }

    public GameObject GetObjectAtCell(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return null;

        return placedObjects[cell.x, cell.y];
    }

    public Vector2Int CellFromWorld(Vector3 worldPosition)
    {
        int cx, cy;
        if (WorldToCell(worldPosition, out cx, out cy))
            return new Vector2Int(cx, cy);

        return new Vector2Int(-1, -1);  // invalid
    }
    
}
