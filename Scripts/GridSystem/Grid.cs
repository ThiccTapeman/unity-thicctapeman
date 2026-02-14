using ThiccTapeman.Utils;
using UnityEngine;
using System;
using TMPro;
using JetBrains.Annotations;


namespace ThiccTapeman.Grid
{
    // ------------------------------------------------------------- //
    // Grid class                                                    //
    // ------------------------------------------------------------- //
    #region Grid Class

    /// <summary>
    /// Grid class that can be used in any direction.
    /// <list>
    ///     <item>See also</item>
    ///     <item><seealso cref="GridObjectAbstract"/></item>
    /// </list>
    /// <example>
    /// This example shows how to create a grid with default settings.
    /// <code>
    /// public class GridObject : GridObjectAbstract 
    /// {
    ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
    ///     
    ///     public override void Update() {}
    ///     public override void RelocatePositions() {};
    /// }
    /// 
    /// Grid grid = new Grid(20, 20, 1f, (grid, x, y) => new GridObject(grid, x, y));
    /// </code>
    /// </example>
    /// </summary>
    public class Grid
    {
        // ------------------------------------------------------------- //
        // Grid Variables                                                //
        // ------------------------------------------------------------- //
        #region Variables

        // Privates

        private Vector3 gridPosition;
        private Vector3 gridRotation;

        private int gridWidth;
        private int gridHeight;

        private float gridTileSizeX;
        private float gridTileSizeY;

        private Func<Grid, int, int, GridObjectAbstract> createDefaultType;

        private GridObjectAbstract[,] grid;

        private bool debug = false;
        
        private TextMeshPro[,] debugTextMeshes;

        #endregion
        // ------------------------------------------------------------- //
        // Setting Grid Variables                                        //
        // ------------------------------------------------------------- //
        #region Setting Variables

        /// <summary>
        /// Will set the grid position and update all the grid objects
        /// </summary>
        /// <param name="position">The new position of the grid</param>
        public void SetGridPosition(Vector3 position)
        {
            gridPosition = position;

            RelocatePositions();
        }

        /// <summary>
        /// Will set the grid rotation and update all the grid objects
        /// </summary>
        /// <param name="rotation">The new rotation of the grid</param>
        public void SetGridRotation(Vector3 rotation)
        {
            gridRotation = rotation;

            RelocatePositions();
        }

        /// <summary>
        /// Will set the tileSize for both X and Y
        /// </summary>
        /// <param name="tileSize">The tilesize that will be used both in X and Y</param>
        public void SetGridTileSize(float tileSize)
        {
            SetGridTileSize(tileSize, tileSize);
        }

        /// <summary>
        /// Will set the tileSize for X and Y
        /// </summary>
        /// <param name="tileSizeX">The tilesize that will be used in X</param>
        /// <param name="tileSizeY">The tilesize that will be used in Y</param>
        public void SetGridTileSize(float tileSizeX, float tileSizeY)
        {
            gridTileSizeX = tileSizeX;
            gridTileSizeY = tileSizeY;

            RelocatePositions();
        }

        #endregion
        // ------------------------------------------------------------- //
        // Getting Grid Variables                                        //
        // ------------------------------------------------------------- //
        #region Getting variables

        /// <summary>
        /// Will out the grid dimensions
        /// </summary>
        /// <param name="width">The width of the grid</param>
        /// <param name="height">The height of the grid</param>
        public void GetGridSize(out int width, out int height)
        {
            width = gridWidth;
            height = gridHeight;
        }

        /// <summary>
        /// Will out the grid width
        /// </summary>
        /// <param name="width">The grid width</param>
        public void GetGridWidth(out int width)
        {
            width = gridWidth;
        }

        /// <summary>
        /// Will out the grid width
        /// </summary>
        public int GetGridWidth()
        {
            return gridWidth;
        }

        /// <summary>
        /// Will out the grid height
        /// </summary>
        /// <param name="height">The grid height</param>
        public void GetGridHeight(out int height)
        {
            height = gridHeight;
        }

        /// <summary>
        /// Will out the grid height
        /// </summary>
        public int GetGridHeight()
        {
            return gridHeight;
        }

        /// <summary>
        /// Will get the tileSize in both X and Y
        /// </summary>
        /// <param name="tileSizeX">The tileSize in the X dimension</param>
        /// <param name="tileSizeY">The tileSize in the Y dimension</param>
        public void GetTileSize(out float tileSizeX, out float tileSizeY)
        {
            tileSizeX = gridTileSizeX;
            tileSizeY = gridTileSizeY;
        }

        /// <summary>
        /// Method to get the tileSize in the X dimension
        /// </summary>
        /// <param name="tileSizeX">The tilesize in the X dimension</param>
        public void GetTileSizeX(out float tileSizeX)
        {
            tileSizeX = gridTileSizeX;
        }

        /// <summary>
        /// Method to get the tileSize in the Y dimension
        /// </summary>
        /// <param name="tileSizeY">The tileSize in the Y dimension</param>
        public void GetTileSizeY(out float tileSizeY)
        {
            tileSizeY = gridTileSizeY;
        }

        /// <summary>
        /// Method to get the position
        /// </summary>
        /// <param name="position">The grid position</param>
        public void GetGridPosition(out Vector3 position)
        {
            position = gridPosition;
        }

        /// <summary>
        /// Method to get the rotation
        /// </summary>
        /// <param name="rotation"></param>
        public void GetGridRotation(out Vector3 rotation)
        {
            rotation = gridRotation;
        }

        #endregion
        // ------------------------------------------------------------- //
        // Grid constructors                                             //
        // ------------------------------------------------------------- //
        #region Constructors

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSize">The tileSize of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <example>
        /// This example shows how to create a grid with default settings.
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract 
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, (grid, x, y) => new GridObject(grid, x, y));
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSize, Func<Grid, int, int, GridObjectAbstract> createDefaultType)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSize;
            this.gridTileSizeY = gridTileSize;
            this.gridPosition = Vector3.zero;
            this.gridRotation = Vector3.zero;
            this.createDefaultType = createDefaultType;

            InitializeGrid();
        }

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSize">The tileSize of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <param name="debug">If debug things should be showed or not</param>
        /// <example>
        /// This example shows how to create a grid with default settings that will show debug.
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract 
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, (grid, x, y) => new GridObject(grid, x, y), true);
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSize, Func<Grid, int, int, GridObjectAbstract> createDefaultType, bool debug)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSize;
            this.gridTileSizeY = gridTileSize;
            this.gridPosition = Vector3.zero;
            this.gridRotation = Vector3.zero;
            this.createDefaultType = createDefaultType;
            this.debug = debug;

            InitializeGrid();
        }

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSize">The tileSize of the grid</param>
        /// <param name="gridPosition">The position of the grid</param>
        /// <param name="gridRotation">The rotation of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <example>
        /// This example shows how to create a grid where rotation and position is specified.
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract 
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, new Vector3(0, 10f, 0), new Vector3(0, 45, 0), (grid, x, y) => new GridObject(grid, x, y));
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSize, Vector3 gridPosition, Vector3 gridRotation, Func<Grid, int, int, GridObjectAbstract> createDefaultType)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSize;
            this.gridTileSizeY = gridTileSize;
            this.gridPosition = gridPosition;
            this.gridRotation = gridRotation;
            this.createDefaultType = createDefaultType;

            InitializeGrid();
        }

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSize">The tileSize of the grid</param>
        /// <param name="gridPosition">The position of the grid</param>
        /// <param name="gridRotation">The rotation of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <param name="debug">If debug things should be showed or not</param>
        /// <example>
        /// This example shows how to create a grid where rotation and position is specified and the debug is shown
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract 
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, new Vector3(0, 10f, 0), new Vector3(0, 45, 0), (grid, x, y) => new GridObject(grid, x, y), true);
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSize, Vector3 gridPosition, Vector3 gridRotation, Func<Grid, int, int, GridObjectAbstract> createDefaultType, bool debug)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSize;
            this.gridTileSizeY = gridTileSize;
            this.gridPosition = gridPosition;
            this.gridRotation = gridRotation;
            this.createDefaultType = createDefaultType;
            this.debug = debug;

            InitializeGrid();
        }

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSizeX">The tileSize in the X direction</param>
        /// <param name="gridTileSizeY">The tileSize in the Y direction</param>
        /// <param name="gridPosition">The position of the grid</param>
        /// <param name="gridRotation">The rotation of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <example>
        /// This example shows how to create a grid where rotation and position is specified, and the gridTileSize is different for the different axies.
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, 1.5f, new Vector3(0, 10f, 0), new Vector3(0, 45, 0), (grid, x, y) => new GridObject(grid, x, y));
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSizeX, float gridTileSizeY, Vector3 gridPosition, Vector3 gridRotation, Func<Grid, int, int, GridObjectAbstract> createDefaultType)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSizeX;
            this.gridTileSizeY = gridTileSizeY;
            this.gridPosition = gridPosition;
            this.gridRotation = gridRotation;
            this.createDefaultType = createDefaultType;

            InitializeGrid();
        }

        /// <summary>
        /// Creates a grid
        /// </summary>
        /// <param name="gridWidth">The width of the grid</param>
        /// <param name="gridHeight">The height of the grid</param>
        /// <param name="gridTileSizeX">The tileSize in the X direction</param>
        /// <param name="gridTileSizeY">The tileSize in the Y direction</param>
        /// <param name="gridPosition">The position of the grid</param>
        /// <param name="gridRotation">The rotation of the grid</param>
        /// <param name="createDefaultType">The default function that will be used to create all the objects inside the grid</param>
        /// <param name="debug">If debug things should be showed or not</param>
        /// <example>
        /// This example shows how to create a grid where rotation and position is specified, the gridTileSize is different for the different axies, and the debug is shown.
        /// 
        /// <code>
        /// public class GridObject : GridObjectAbstract
        /// {
        ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
        ///     
        ///     public override void UpdateGrid() {}
        /// }
        /// 
        /// Grid grid = new Grid(20, 20, 1f, 1.5f, new Vector3(0, 10f, 0), new Vector3(0, 45, 0), (grid, x, y) => new GridObject(grid, x, y));
        /// </code>
        /// </example>
        public Grid(int gridWidth, int gridHeight, float gridTileSizeX, float gridTileSizeY, Vector3 gridPosition, Vector3 gridRotation, Func<Grid, int, int, GridObjectAbstract> createDefaultType, bool debug)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.gridTileSizeX = gridTileSizeX;
            this.gridTileSizeY = gridTileSizeY;
            this.gridPosition = gridPosition;
            this.gridRotation = gridRotation;
            this.createDefaultType = createDefaultType;
            this.debug = debug;

            InitializeGrid();
        }

        #endregion
        // ------------------------------------------------------------- //
        // Initialize                                                    //
        // ------------------------------------------------------------- //
        #region Initialize

        /// <summary>
        /// Initializes the grid
        /// </summary>
        private void InitializeGrid()
        {
            grid = new GridObjectAbstract[gridWidth, gridHeight];

            for(int x = 0; x < gridWidth; x++)
            {
                for(int y = 0; y < gridHeight; y++)
                {
                    grid[x, y] = createDefaultType(this, x, y);
                }
            }

            Debug();
        }

        #endregion
        // ------------------------------------------------------------- //
        // Grid position                                                 //
        // ------------------------------------------------------------- //
        #region Grid position

        /// <summary>
        /// This will turn the grid position into a world position
        /// 
        /// <example>
        /// This example shows how you can get the worldPosition
        /// 
        /// <code>
        /// Grid grid = // previously created grid
        /// 
        /// Vector3 worldPosition = grid.GridToWorldPosition(2, 2);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="x">the X cordinate in the grid</param>
        /// <param name="y">The Y cordinate in the grid</param>
        /// <returns>Returns the world position</returns>
        public Vector3 GridToWorldPosition(int x, int y)
        {
            return GridToWorldPosition(x, y, out Vector3 _);
        }

        /// <summary>
        /// This will turn the grid position into a world position
        /// 
        /// <example>
        /// This example shows how you can get the worldPosition and the rotation
        /// 
        /// <code>
        /// Grid grid = // previously created grid
        /// 
        /// Vector3 worldPosition = grid.GridToWorldPosition(2, 2, out Vector3 rotation);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="x">the X cordinate in the grid</param>
        /// <param name="y">The Y cordinate in the grid</param>
        /// <param name="rotation">This is an that will return the rotation</param>
        /// <returns>Returns the world position</returns>
        public Vector3 GridToWorldPosition(int x, int y, out Vector3 rotation)
        {
            float xPos = x * gridTileSizeX;
            float yPos = y * gridTileSizeY;

            Vector3 position = new Vector3(xPos, yPos, 0);

            position = Vector3Utils.RotatePositionAroundOrigin(position, gridRotation);

            rotation = gridRotation;

            return position + gridPosition;
        }

        public Vector3 SnapWorldPositionToGrid(Vector3 position)
        {
            WorldToGridPosition(position, out int x, out int y);
            return GridToWorldPosition(x, y);
        }

        /// <summary>
        /// This will turn the world position into a grid position
        /// </summary>
        /// <param name="worldPosition">The world position</param>
        /// <param name="x">The X cordinate</param>
        /// <param name="y">The Y cordinate</param>
        /// <example>
        /// This example shows how you can get the grid position using the world position
        /// 
        /// <code>
        /// Grid grid = // previously created grid
        /// 
        /// Vector3 worldPosition = new Vector3(1f, 0, 2f);
        /// grid.WorldToGridPosition(worldPosition, out int x, out int y)
        /// </code>
        /// </example>
        public void WorldToGridPosition(Vector3 worldPosition, out int x, out int y)
        {
            // Project onto the plane defined by the grid rotation
            Vector3 planeNormals = Vector3Utils.CalculateNormalizedNormalFromEulerAngles(gridRotation);

            Vector2 projectedPosition = Vector3Utils.ProjectVectorOntoPlaneRelative(gridPosition, planeNormals, worldPosition);


            x = Mathf.FloorToInt((projectedPosition.x - gridPosition.x + gridTileSizeX / 2) / gridTileSizeX);
            y = Mathf.FloorToInt((projectedPosition.y - gridPosition.y + gridTileSizeY / 2) / gridTileSizeY);
        }

        #endregion
        // ------------------------------------------------------------- //
        // GridObjects                                                   //
        // ------------------------------------------------------------- //
        #region GridObjects

        /// <summary>
        /// Used for getting the gridObject at specified position
        /// </summary>
        /// <param name="position">The position</param>
        /// <returns>The GridObjectAbstract that is on the specified world position, returns null if outside the grid</returns>
        public GridObjectAbstract GetGridObject(Vector3 position)
        {
            WorldToGridPosition(position, out int x, out int y);

            return GetGridObject(x, y);
        }

        /// <summary>
        /// Used for getting the gridObject at specified position
        /// </summary>
        /// <param name="pos">The grid position</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract GetGridObject(Vector2Int pos)
        {
            if (!PositionIsInsideGrid(pos.x, pos.y)) return null;

            return grid[pos.x, pos.y];
        }

        /// <summary>
        /// Used for getting the gridObject at specified position
        /// </summary>
        /// <param name="x">The X position</param>
        /// <param name="y">The Y position</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract GetGridObject(int x, int y)
        {
            if(!PositionIsInsideGrid(x, y)) return null;

            return grid[x, y];
        }

        /// <summary>
        /// Used for setting the gridObject at specified position to the default GridObject
        /// </summary>
        /// <param name="position">The position</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract SetGridObject(Vector3 position)
        {
            WorldToGridPosition(position, out int x, out int y);

            return SetGridObject(x, y, null);
        }

        /// <summary>
        /// Used for setting the gridObject at specified position
        /// </summary>
        /// <param name="position">The position</param>
        /// <param name="gridObject">The new GridObject</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract SetGridObject(Vector3 position, GridObjectAbstract gridObject)
        {
            WorldToGridPosition(position, out int x, out int y);

            return SetGridObject(x, y, gridObject);
        }

        /// <summary>
        /// Used for setting the gridObject at specified position to the default GridObject
        /// </summary>
        /// <param name="x">The X position</param>
        /// <param name="y">The Y position</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract SetGridObject(int x, int y)
        {
            return SetGridObject(x, y, null);
        }

        /// <summary>
        /// Used for setting the gridObject at specified position
        /// </summary>
        /// <param name="x">The X position</param>
        /// <param name="y">The Y position</param>
        /// <param name="gridObject">The new GridObject</param>
        /// <returns>The GridObjectAbstract that is on the specified grid position, returns null if outside the grid</returns>
        public GridObjectAbstract SetGridObject(int x, int y, GridObjectAbstract gridObject)
        {
            if (!PositionIsInsideGrid(x, y)) return null;

            if(gridObject == null) gridObject = createDefaultType(this, x, y);

            return grid[x, y] = gridObject;
        }

        #endregion
        // ------------------------------------------------------------- //
        // GridObject updates                                            //
        // ------------------------------------------------------------- //
        #region Updates

        /// <summary>
        /// This will update all the gridObjects inside
        /// </summary>

        public void RelocatePositions()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    grid[x, y].RelocatePositions();
                }
            }
            Debug();
        }

        public void UpdateAllGridObjects()
        {
            for(int x = 0; x < gridWidth; x++)
            {
                for(int y = 0; y < gridHeight; y++)
                {
                    grid[x, y].Update();
                }
            }
        }

        public void UpdateGridObjectsArea(int x, int y, int size)
        {
            UpdateGridObjectsArea(x, y, size, size);
        }

        public void UpdateGridObjectsArea(int x, int y, int width, int height)
        {
            if (width % 2 == 0) width += 1;
            if (height % 2 == 0) height += 1;

            x -= Mathf.Max(Mathf.FloorToInt(width / 2), 0);
            y -= Mathf.Max(Mathf.FloorToInt(height / 2), 0);

            for(int i = x; i < Mathf.FloorToInt(width / 2) && i < gridWidth; i++)
            {
                for (int j = y; j < Mathf.FloorToInt(height / 2) && j < gridHeight; j++)
                {
                    grid[i, j].Update();
                }
            }
        }

        #endregion
        // ------------------------------------------------------------- //
        // Debug                                                         //
        // ------------------------------------------------------------- //
        #region Debug

        /// <summary>
        /// This will show the debug for the grid
        /// </summary>
        private void Debug()
        {
            if(!debug) return;

            if (debugTextMeshes == null) debugTextMeshes = new TextMeshPro[gridWidth, gridHeight];

            for(int x = 0; x < gridWidth; x++)
            {
                for(int y = 0; y < gridHeight; y++)
                {
                    DebugPerObject(x, y);
                }
            }
        }

        private void DebugPerObject(int x, int y)
        {
            Vector3 position = GridToWorldPosition(x, y);

            string text = grid[x, y].ToString();

            if (debugTextMeshes[x, y] != null)
            {
                debugTextMeshes[x, y].gameObject.transform.position = position;
                debugTextMeshes[x, y].text = text;

                return;
            }

            debugTextMeshes[x, y] = Text.CreateWorldText(text, position);
        }

        public void DebugLogGrid(int startX, int startY, int endX, int endY)
        {
            for(int i = startY; i < gridHeight && i < endY; i++)
            {
                string s = "| ";
                for (int j = startX; j < gridWidth && j < endX; j++)
                {
                    s += grid[j, i].ToString() + " | ";
                }

                UnityEngine.Debug.Log(s);
            }
        }

        public void DebugLogGrid(int x, int y)
        {
            
        }

        #endregion
        // ------------------------------------------------------------- //
        // Extras                                                        //
        // ------------------------------------------------------------- //
        #region Extras

        /// <summary>
        /// Will check if the specified position is inside the grid bounderies
        /// </summary>
        /// <param name="x">The grid X cordinate</param>
        /// <param name="y">The grid Z cordinate</param>
        /// <returns>Will return true if the position is inside the grid</returns>
        public bool PositionIsInsideGrid(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        /// <summary>
        /// Will check if the specified position is inside the grid bounderies
        /// </summary>
        /// <param name="position">The world position</param>
        /// <returns>Will return true if the position is inside the grid</returns>
        public bool PositionIsInsideGrid(Vector3 position)
        {
            WorldToGridPosition(position, out int x, out int y);
            return PositionIsInsideGrid(x, y);
        }

        #endregion
    }

    #endregion
    // ------------------------------------------------------------- //
    // GridObjectAbstract class                                      //
    // ------------------------------------------------------------- //
    #region GridObjectAbstract Class
    /// <summary>
    /// This is the class that the default Grid will work with.
    /// <list>
    ///     <item>See also</item>
    ///     <item><seealso cref="Grid"/></item>
    /// </list>
    /// <example>
    /// This is one example of how the class can be implimented where it will have an Transform attached and move it as the grid changes
    /// 
    /// <code>
    /// public class GridObject : GridObjectAbstract 
    /// {
    ///     private Transform transform
    /// 
    ///     public GridObject(Grid grid, int x, int y) : Base(grid, x, y) {}
    ///     
    ///     public override void Update() {};
    /// 
    ///     public override void RelocatePositions()
    ///     {
    ///         if(transform == null) return;
    ///         
    ///         transform.position = grid.GridToWorldPosition(x, y, out Vector3 rotation);
    ///         transform.rotation = Quaternion.Euler(rotation);
    ///     }
    ///     
    ///     public void SetTransform(Transform t) => transform = t;
    ///     public Transform GetTransform() => return transform;
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public abstract class GridObjectAbstract
    {
        protected Grid grid;

        public int x;
        public int y;

        protected GridObjectAbstract(Grid grid, int x, int y) 
        {
            this.grid = grid;
            this.x = x;
            this.y = y;
        }

        public virtual void Update() { }

        /// <summary>
        /// This will be called when the grid is moved, rotated, or the tileSize changes
        /// </summary>
        public virtual void RelocatePositions() { }
    }

    #endregion
}