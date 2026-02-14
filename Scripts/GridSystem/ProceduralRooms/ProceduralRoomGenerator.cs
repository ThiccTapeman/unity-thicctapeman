using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using RoomGrid = ThiccTapeman.Grid.Grid;
using ThiccTapeman.Grid;

namespace ThiccTapeman.GridProceduralRooms
{
    /// <summary>
    /// Generates rectangular rooms on a tilemap and places items inside them.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralRoomGenerator : MonoBehaviour
    {
        [Header("Tilemap")]
        [SerializeField] private Tilemap roomTilemap;
        [SerializeField] private TileBase roomTile;

        [Header("Room Layout")]
        [Min(1)] public int roomCount = 1;
        public Vector2Int roomSizeMin = new Vector2Int(6, 6);
        public Vector2Int roomSizeMax = new Vector2Int(12, 12);
        public Vector2Int roomAreaSize = new Vector2Int(50, 50);
        public Vector2Int areaOrigin = Vector2Int.zero;
        public Vector2Int roomPadding = new Vector2Int(2, 2);
        public bool clusterRooms = true;
        [Min(1)] public int clusterAttempts = 8;
        public bool connectRooms = true;
        [Min(1)] public int corridorWidth = 2;

        [Header("Room Shape")]
        public RoomShapeType roomShape = RoomShapeType.Random;
        [Min(1)] public int shapeArmThickness = 3;

        [Header("Item Placement")]
        public bool useGridPlacement = true;
        [Min(0)] public int itemPadding = 1;
        [Min(0)] public int wallClearance = 1;
        [Min(1)] public int maxPlacementAttempts = 50;
        [Min(0f)] public float minFloatingSpacing = 0f;
        public Transform itemParent;

        public RoomTableSO[] roomTables;

        [Header("Runtime")]
        public bool generateOnStart = true;

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateRooms();
            }
        }

        [ContextMenu("Generate Rooms")]
        public void GenerateRooms()
        {
            if (roomTilemap == null || roomTile == null)
            {
                Debug.LogWarning("Room tilemap or tile is not assigned.", this);
                return;
            }

            roomTilemap.ClearAllTiles();
            ClearSpawnedItems();

            List<RoomDefinition> rooms = GenerateRoomDefinitions();

            foreach (RoomDefinition room in rooms)
            {
                PaintRoom(room);
                RoomPlacementContext context = CreatePlacementContext(room);
                PlaceTables(roomTables, context);
            }

            if (connectRooms && rooms.Count > 1)
            {
                ConnectRoomsWithCorridors(rooms);
            }
        }

        private List<RoomDefinition> GenerateRoomDefinitions()
        {
            List<RoomDefinition> rooms = new List<RoomDefinition>();
            HashSet<Vector2Int> occupiedFloorTiles = new HashSet<Vector2Int>();
            int attemptsPerRoom = maxPlacementAttempts * 2;

            for (int i = 0; i < roomCount; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < attemptsPerRoom && !placed; attempt++)
                {
                    Vector2Int size = new Vector2Int(
                        UnityEngine.Random.Range(roomSizeMin.x, roomSizeMax.x + 1),
                        UnityEngine.Random.Range(roomSizeMin.y, roomSizeMax.y + 1)
                    );

                    BoundsInt bounds;
                    if (!TryFindRoomBounds(size, rooms, out bounds))
                    {
                        continue;
                    }

                    if (OverlapsExistingRoom(bounds, rooms))
                    {
                        continue;
                    }

                    RoomDefinition definition = CreateRoomDefinition(bounds);
                    if (FloorOverlapsExisting(definition, occupiedFloorTiles))
                    {
                        continue;
                    }

                    AddOccupiedTiles(definition, occupiedFloorTiles);
                    rooms.Add(definition);
                    placed = true;
                }
            }

            return rooms;
        }

        private bool TryFindRoomBounds(Vector2Int size, List<RoomDefinition> rooms, out BoundsInt bounds)
        {
            bounds = default;
            int maxX = areaOrigin.x + roomAreaSize.x - size.x;
            int maxY = areaOrigin.y + roomAreaSize.y - size.y;
            if (maxX < areaOrigin.x || maxY < areaOrigin.y)
            {
                return false;
            }

            if (clusterRooms && rooms.Count > 0)
            {
                for (int attempt = 0; attempt < clusterAttempts; attempt++)
                {
                    RoomDefinition anchor = rooms[UnityEngine.Random.Range(0, rooms.Count)];
                    if (TryGetAdjacentBounds(anchor.Bounds, size, out bounds) &&
                        bounds.xMin >= areaOrigin.x &&
                        bounds.yMin >= areaOrigin.y &&
                        bounds.xMax <= areaOrigin.x + roomAreaSize.x &&
                        bounds.yMax <= areaOrigin.y + roomAreaSize.y)
                    {
                        return true;
                    }
                }
            }

            int x = UnityEngine.Random.Range(areaOrigin.x, maxX + 1);
            int y = UnityEngine.Random.Range(areaOrigin.y, maxY + 1);
            bounds = new BoundsInt(x, y, 0, size.x, size.y, 1);
            return true;
        }

        private bool TryGetAdjacentBounds(BoundsInt anchor, Vector2Int size, out BoundsInt bounds)
        {
            int side = UnityEngine.Random.Range(0, 4);
            int paddingX = Mathf.Max(0, roomPadding.x);
            int paddingY = Mathf.Max(0, roomPadding.y);
            int x;
            int y;

            switch (side)
            {
                case 0: // left
                    x = anchor.xMin - size.x - paddingX;
                    y = UnityEngine.Random.Range(anchor.yMin - size.y + 1, anchor.yMax - 1);
                    break;
                case 1: // right
                    x = anchor.xMax + paddingX;
                    y = UnityEngine.Random.Range(anchor.yMin - size.y + 1, anchor.yMax - 1);
                    break;
                case 2: // down
                    x = UnityEngine.Random.Range(anchor.xMin - size.x + 1, anchor.xMax - 1);
                    y = anchor.yMin - size.y - paddingY;
                    break;
                default: // up
                    x = UnityEngine.Random.Range(anchor.xMin - size.x + 1, anchor.xMax - 1);
                    y = anchor.yMax + paddingY;
                    break;
            }

            bounds = new BoundsInt(x, y, 0, size.x, size.y, 1);
            return true;
        }

        private bool OverlapsExistingRoom(BoundsInt room, List<RoomDefinition> rooms)
        {
            BoundsInt padded = new BoundsInt(
                room.xMin - roomPadding.x,
                room.yMin - roomPadding.y,
                0,
                room.size.x + roomPadding.x * 2,
                room.size.y + roomPadding.y * 2,
                1
            );

            foreach (RoomDefinition existing in rooms)
            {
                if (BoundsOverlap2D(padded, existing.Bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private bool BoundsOverlap2D(BoundsInt a, BoundsInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
        }

        private bool FloorOverlapsExisting(RoomDefinition definition, HashSet<Vector2Int> occupiedFloorTiles)
        {
            BoundsInt bounds = definition.Bounds;
            for (int i = 0; i < definition.FloorCells.Count; i++)
            {
                Vector2Int cell = definition.FloorCells[i];
                Vector2Int worldCell = new Vector2Int(bounds.xMin + cell.x, bounds.yMin + cell.y);
                if (occupiedFloorTiles.Contains(worldCell))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddOccupiedTiles(RoomDefinition definition, HashSet<Vector2Int> occupiedFloorTiles)
        {
            BoundsInt bounds = definition.Bounds;
            for (int i = 0; i < definition.FloorCells.Count; i++)
            {
                Vector2Int cell = definition.FloorCells[i];
                occupiedFloorTiles.Add(new Vector2Int(bounds.xMin + cell.x, bounds.yMin + cell.y));
            }
        }

        private RoomDefinition CreateRoomDefinition(BoundsInt bounds)
        {
            bool[,] mask = BuildRoomMask(bounds.size.x, bounds.size.y);
            List<Vector2Int> floorCells = new List<Vector2Int>();
            for (int x = 0; x < bounds.size.x; x++)
            {
                for (int y = 0; y < bounds.size.y; y++)
                {
                    if (mask[x, y])
                    {
                        floorCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            return new RoomDefinition(bounds, mask, floorCells);
        }

        private bool[,] BuildRoomMask(int width, int height)
        {
            bool[,] mask = new bool[width, height];
            RoomShapeType shape = PickRoomShapeType();

            switch (shape)
            {
                case RoomShapeType.LShape:
                    BuildLShapeMask(mask);
                    break;
                case RoomShapeType.TShape:
                    BuildTShapeMask(mask);
                    break;
                case RoomShapeType.SShape:
                    BuildSShapeMask(mask);
                    break;
                default:
                    FillMask(mask, true);
                    break;
            }

            return mask;
        }

        private RoomShapeType PickRoomShapeType()
        {
            if (roomShape != RoomShapeType.Random)
            {
                return roomShape;
            }

            int roll = UnityEngine.Random.Range(0, 4);
            return (RoomShapeType)roll;
        }

        private void BuildLShapeMask(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            int thickness = Mathf.Clamp(shapeArmThickness, 1, Mathf.Min(width, height));

            int orientation = UnityEngine.Random.Range(0, 4);
            if (orientation == 0)
            {
                FillRect(mask, 0, 0, thickness, height);
                FillRect(mask, 0, 0, width, thickness);
            }
            else if (orientation == 1)
            {
                FillRect(mask, width - thickness, 0, thickness, height);
                FillRect(mask, 0, 0, width, thickness);
            }
            else if (orientation == 2)
            {
                FillRect(mask, 0, 0, thickness, height);
                FillRect(mask, 0, height - thickness, width, thickness);
            }
            else
            {
                FillRect(mask, width - thickness, 0, thickness, height);
                FillRect(mask, 0, height - thickness, width, thickness);
            }
        }

        private void BuildTShapeMask(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            int thickness = Mathf.Clamp(shapeArmThickness, 1, Mathf.Min(width, height));
            int centerX = Mathf.Max(0, (width - thickness) / 2);
            int centerY = Mathf.Max(0, (height - thickness) / 2);

            int orientation = UnityEngine.Random.Range(0, 4);
            if (orientation == 0)
            {
                FillRect(mask, 0, height - thickness, width, thickness);
                FillRect(mask, centerX, 0, thickness, height - thickness);
            }
            else if (orientation == 1)
            {
                FillRect(mask, 0, 0, width, thickness);
                FillRect(mask, centerX, thickness, thickness, height - thickness);
            }
            else if (orientation == 2)
            {
                FillRect(mask, 0, 0, thickness, height);
                FillRect(mask, thickness, centerY, width - thickness, thickness);
            }
            else
            {
                FillRect(mask, width - thickness, 0, thickness, height);
                FillRect(mask, 0, centerY, width - thickness, thickness);
            }
        }

        private void BuildSShapeMask(bool[,] mask)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            int thickness = Mathf.Clamp(shapeArmThickness, 1, Mathf.Min(width, height));

            if (width >= height)
            {
                int barWidth = Mathf.Max(thickness + 1, width - thickness);
                FillRect(mask, 0, height - thickness, barWidth, thickness);
                FillRect(mask, width - barWidth, 0, barWidth, thickness);
                int connectorX = Mathf.Max(0, (width - thickness) / 2);
                int connectorHeight = Mathf.Max(1, height - thickness * 2);
                FillRect(mask, connectorX, thickness, thickness, connectorHeight);
            }
            else
            {
                int barHeight = Mathf.Max(thickness + 1, height - thickness);
                FillRect(mask, 0, height - barHeight, thickness, barHeight);
                FillRect(mask, width - thickness, 0, thickness, barHeight);
                int connectorY = Mathf.Max(0, (height - thickness) / 2);
                int connectorWidth = Mathf.Max(1, width - thickness * 2);
                FillRect(mask, thickness, connectorY, connectorWidth, thickness);
            }
        }

        private void FillMask(bool[,] mask, bool value)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    mask[x, y] = value;
                }
            }
        }

        private void FillRect(bool[,] mask, int startX, int startY, int width, int height)
        {
            int maxX = mask.GetLength(0);
            int maxY = mask.GetLength(1);
            for (int x = startX; x < startX + width; x++)
            {
                if (x < 0 || x >= maxX)
                {
                    continue;
                }

                for (int y = startY; y < startY + height; y++)
                {
                    if (y < 0 || y >= maxY)
                    {
                        continue;
                    }

                    mask[x, y] = true;
                }
            }
        }

        private void PaintRoom(RoomDefinition room)
        {
            BoundsInt bounds = room.Bounds;
            for (int x = 0; x < bounds.size.x; x++)
            {
                for (int y = 0; y < bounds.size.y; y++)
                {
                    if (!room.FloorMask[x, y])
                    {
                        continue;
                    }

                    roomTilemap.SetTile(new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0), roomTile);
                }
            }
        }

        private void ConnectRoomsWithCorridors(List<RoomDefinition> rooms)
        {
            rooms.Sort((a, b) => a.Center.x.CompareTo(b.Center.x));
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                Vector2Int start = rooms[i].Center;
                Vector2Int end = rooms[i + 1].Center;
                PaintCorridor(start, end, corridorWidth);
            }
        }

        private void PaintCorridor(Vector2Int start, Vector2Int end, int width)
        {
            if (width < 1)
            {
                width = 1;
            }

            bool horizontalFirst = UnityEngine.Random.value > 0.5f;
            if (horizontalFirst)
            {
                PaintHorizontalCorridor(start.x, end.x, start.y, width);
                PaintVerticalCorridor(start.y, end.y, end.x, width);
            }
            else
            {
                PaintVerticalCorridor(start.y, end.y, start.x, width);
                PaintHorizontalCorridor(start.x, end.x, end.y, width);
            }
        }

        private void PaintHorizontalCorridor(int xStart, int xEnd, int y, int width)
        {
            int min = Mathf.Min(xStart, xEnd);
            int max = Mathf.Max(xStart, xEnd);
            int half = width / 2;
            for (int x = min; x <= max; x++)
            {
                for (int offset = -half; offset <= half; offset++)
                {
                    roomTilemap.SetTile(new Vector3Int(x, y + offset, 0), roomTile);
                }
            }
        }

        private void PaintVerticalCorridor(int yStart, int yEnd, int x, int width)
        {
            int min = Mathf.Min(yStart, yEnd);
            int max = Mathf.Max(yStart, yEnd);
            int half = width / 2;
            for (int y = min; y <= max; y++)
            {
                for (int offset = -half; offset <= half; offset++)
                {
                    roomTilemap.SetTile(new Vector3Int(x + offset, y, 0), roomTile);
                }
            }
        }

        private RoomPlacementContext CreatePlacementContext(RoomDefinition room)
        {
            BoundsInt bounds = room.Bounds;
            Vector3 cellSize = roomTilemap.layoutGrid.cellSize;
            Vector3 worldOrigin = roomTilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));

            RoomGrid grid = null;
            if (useGridPlacement)
            {
                grid = new RoomGrid(
                    bounds.size.x,
                    bounds.size.y,
                    cellSize.x,
                    cellSize.y,
                    worldOrigin,
                    Vector3.zero,
                    (g, x, y) => new RoomCell(g, x, y)
                );
            }

            return new RoomPlacementContext(bounds, room.FloorMask, room.FloorCells, grid, worldOrigin, new Vector2(cellSize.x, cellSize.y));
        }


        private void PlaceTables(RoomTableSO[] tables, RoomPlacementContext context)
        {
            if (tables == null || tables.Length == 0)
            {
                return;
            }

            foreach (RoomTableSO table in tables)
            {
                if (table == null || table.entries == null || table.entries.Length == 0)
                {
                    continue;
                }

                int rolls = UnityEngine.Random.Range(Mathf.Max(0, table.minRolls), Mathf.Max(0, table.maxRolls) + 1);
                for (int roll = 0; roll < rolls; roll++)
                {
                    RoomTableSO.RoomTableEntry entry = PickWeightedEntry(table.entries);
                    if (entry == null || entry.item == null || entry.item.prefab == null)
                    {
                        continue;
                    }

                    int count = entry.GetRandomCount();
                    for (int i = 0; i < count; i++)
                    {
                        if (!TryPlacePlaceable(entry.item, context, out Vector3 spawnPosition))
                        {
                            break;
                        }

                        SpawnPlaceable(entry.item, spawnPosition);
                    }
                }
            }
        }

        private RoomTableSO.RoomTableEntry PickWeightedEntry(RoomTableSO.RoomTableEntry[] entries)
        {
            float totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                RoomTableSO.RoomTableEntry entry = entries[i];
                if (entry == null || entry.item == null || entry.item.prefab == null)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0f, entry.weight);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.value * totalWeight;
            float accumulated = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                RoomTableSO.RoomTableEntry entry = entries[i];
                if (entry == null || entry.item == null || entry.item.prefab == null)
                {
                    continue;
                }

                accumulated += Mathf.Max(0f, entry.weight);
                if (roll <= accumulated)
                {
                    return entry;
                }
            }

            return null;
        }

        private bool TryPlacePlaceable(RoomPlaceableSO placeable, RoomPlacementContext context, out Vector3 spawnPosition)
        {
            return useGridPlacement
                ? TryPlaceOnGrid(placeable, context, out spawnPosition)
                : TryPlaceFloating(placeable, context, out spawnPosition);
        }

        private bool TryPlaceOnGrid(RoomPlaceableSO placeable, RoomPlacementContext context, out Vector3 spawnPosition)
        {
            Vector2Int footprint = ClampFootprint(placeable.footprint);
            int width = context.RoomBounds.size.x;
            int height = context.RoomBounds.size.y;

            int minX = itemPadding;
            int minY = itemPadding;
            int maxX = width - footprint.x - itemPadding;
            int maxY = height - footprint.y - itemPadding;

            spawnPosition = Vector3.zero;
            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                int x = UnityEngine.Random.Range(minX, maxX + 1);
                int y = UnityEngine.Random.Range(minY, maxY + 1);

                if (!CanPlaceAt(context, x, y, footprint))
                {
                    continue;
                }

                MarkOccupied(context.Grid, x, y, footprint);
                spawnPosition = GetGridWorldCenter(context, x, y, footprint);
                return true;
            }

            return false;
        }

        private bool TryPlaceFloating(RoomPlaceableSO placeable, RoomPlacementContext context, out Vector3 spawnPosition)
        {
            Vector2Int footprint = ClampFootprint(placeable.footprint);
            spawnPosition = Vector3.zero;
            if (context.FloorCells.Count == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                Vector2Int cell = context.FloorCells[UnityEngine.Random.Range(0, context.FloorCells.Count)];
                if (!HasClearance(context, cell.x, cell.y, footprint))
                {
                    continue;
                }

                Vector3 basePos = GetGridWorldCenter(context, cell.x, cell.y, footprint);
                float jitterX = UnityEngine.Random.Range(-context.CellSize.x * 0.25f, context.CellSize.x * 0.25f);
                float jitterY = UnityEngine.Random.Range(-context.CellSize.y * 0.25f, context.CellSize.y * 0.25f);
                Vector3 candidate = basePos + new Vector3(jitterX, jitterY, 0f);

                if (!IsFarEnough(candidate, context))
                {
                    continue;
                }

                context.FloatingPositions.Add(candidate);
                spawnPosition = candidate;
                return true;
            }

            return false;
        }

        private bool IsFarEnough(Vector3 candidate, RoomPlacementContext context)
        {
            if (minFloatingSpacing <= 0f)
            {
                return true;
            }

            float minDistance = minFloatingSpacing;
            foreach (Vector3 existing in context.FloatingPositions)
            {
                if (Vector3.Distance(existing, candidate) < minDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 GetGridWorldCenter(RoomPlacementContext context, int x, int y, Vector2Int footprint)
        {
            Vector3 basePos = context.Grid != null
                ? context.Grid.GridToWorldPosition(x, y)
                : context.WorldOrigin + new Vector3(x * context.CellSize.x, y * context.CellSize.y, 0f);
            Vector3 halfSize = new Vector3(
                context.CellSize.x * footprint.x * 0.5f,
                context.CellSize.y * footprint.y * 0.5f,
                0f
            );

            return basePos + halfSize;
        }

        private bool CanPlaceAt(RoomPlacementContext context, int x, int y, Vector2Int footprint)
        {
            for (int dx = 0; dx < footprint.x; dx++)
            {
                for (int dy = 0; dy < footprint.y; dy++)
                {
                    int cx = x + dx;
                    int cy = y + dy;
                    if (!IsFloorCell(context, cx, cy))
                    {
                        return false;
                    }

                    RoomCell cell = context.Grid.GetGridObject(cx, cy) as RoomCell;
                    if (cell == null || cell.IsOccupied)
                    {
                        return false;
                    }
                }
            }

            return HasClearance(context, x, y, footprint);
        }

        private void MarkOccupied(RoomGrid grid, int x, int y, Vector2Int footprint)
        {
            for (int dx = 0; dx < footprint.x; dx++)
            {
                for (int dy = 0; dy < footprint.y; dy++)
                {
                    RoomCell cell = grid.GetGridObject(x + dx, y + dy) as RoomCell;
                    if (cell != null)
                    {
                        cell.IsOccupied = true;
                    }
                }
            }
        }

        private Vector2Int ClampFootprint(Vector2Int footprint)
        {
            int x = Mathf.Max(1, footprint.x);
            int y = Mathf.Max(1, footprint.y);
            return new Vector2Int(x, y);
        }

        private void SpawnPlaceable(RoomPlaceableSO placeable, Vector3 position)
        {
            Transform parent = itemParent != null ? itemParent : transform;
            GameObject instance = Instantiate(placeable.prefab, position + placeable.positionOffset, Quaternion.identity, parent);
            spawnedObjects.Add(instance);
        }

        private void ClearSpawnedItems()
        {
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = spawnedObjects[i];
                if (obj == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(obj);
                }
                else
                {
                    DestroyImmediate(obj);
                }
            }

            spawnedObjects.Clear();
        }

        private bool IsFloorCell(RoomPlacementContext context, int x, int y)
        {
            if (x < 0 || y < 0 || x >= context.RoomBounds.size.x || y >= context.RoomBounds.size.y)
            {
                return false;
            }

            return context.FloorMask[x, y];
        }

        private bool HasClearance(RoomPlacementContext context, int startX, int startY, Vector2Int footprint)
        {
            int clearance = Mathf.Max(0, wallClearance);
            if (clearance == 0)
            {
                return true;
            }

            for (int dx = 0; dx < footprint.x; dx++)
            {
                for (int dy = 0; dy < footprint.y; dy++)
                {
                    int cx = startX + dx;
                    int cy = startY + dy;
                    for (int ox = -clearance; ox <= clearance; ox++)
                    {
                        for (int oy = -clearance; oy <= clearance; oy++)
                        {
                            if (!IsFloorCell(context, cx + ox, cy + oy))
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void OnValidate()
        {
            roomSizeMin.x = Mathf.Max(1, roomSizeMin.x);
            roomSizeMin.y = Mathf.Max(1, roomSizeMin.y);
            roomSizeMax.x = Mathf.Max(roomSizeMin.x, roomSizeMax.x);
            roomSizeMax.y = Mathf.Max(roomSizeMin.y, roomSizeMax.y);
            roomAreaSize.x = Mathf.Max(1, roomAreaSize.x);
            roomAreaSize.y = Mathf.Max(1, roomAreaSize.y);
            roomPadding.x = Mathf.Max(0, roomPadding.x);
            roomPadding.y = Mathf.Max(0, roomPadding.y);
            clusterAttempts = Mathf.Max(1, clusterAttempts);
            corridorWidth = Mathf.Max(1, corridorWidth);
            shapeArmThickness = Mathf.Max(1, shapeArmThickness);
            wallClearance = Mathf.Max(0, wallClearance);
        }

        private sealed class RoomCell : GridObjectAbstract
        {
            public bool IsOccupied { get; set; }

            public RoomCell(RoomGrid grid, int x, int y) : base(grid, x, y)
            {
            }
        }

        private sealed class RoomPlacementContext
        {
            public RoomPlacementContext(BoundsInt roomBounds, bool[,] floorMask, List<Vector2Int> floorCells, RoomGrid grid, Vector3 worldOrigin, Vector2 cellSize)
            {
                RoomBounds = roomBounds;
                FloorMask = floorMask;
                FloorCells = floorCells;
                Grid = grid;
                WorldOrigin = worldOrigin;
                CellSize = cellSize;
                FloatingPositions = new List<Vector3>();
            }

            public BoundsInt RoomBounds { get; }
            public bool[,] FloorMask { get; }
            public List<Vector2Int> FloorCells { get; }
            public RoomGrid Grid { get; }
            public Vector3 WorldOrigin { get; }
            public Vector2 CellSize { get; }
            public List<Vector3> FloatingPositions { get; }
        }

        private sealed class RoomDefinition
        {
            public RoomDefinition(BoundsInt bounds, bool[,] floorMask, List<Vector2Int> floorCells)
            {
                Bounds = bounds;
                FloorMask = floorMask;
                FloorCells = floorCells;
                Center = new Vector2Int(
                    bounds.xMin + bounds.size.x / 2,
                    bounds.yMin + bounds.size.y / 2
                );
            }

            public BoundsInt Bounds { get; }
            public bool[,] FloorMask { get; }
            public List<Vector2Int> FloorCells { get; }
            public Vector2Int Center { get; }
        }

        public enum RoomShapeType
        {
            Rectangle = 0,
            LShape = 1,
            TShape = 2,
            SShape = 3,
            Random = 4
        }

    }
}
