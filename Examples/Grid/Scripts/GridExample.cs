using UnityEngine;
using UnityEngine.Serialization;
using ThiccTapeman.Grid;
using ThiccTapeman.Utils;

public class GridExample : MonoBehaviour
{
    ThiccTapeman.Grid.Grid grid;

    // -- Publics -- //

    [FormerlySerializedAs("gameObject")]
    public GameObject tilePrefab;

    // Grid variables
    public int gridSizeX = 20;
    public int gridSizeY = 20;
    public Vector3 position;
    public Vector3 rotation;
    public float tileSizeX;
    public float tileSizeY;

    // Highlighting
    public Transform highlight;
    public Material highlightMaterial;

    // Debug stuff
    public Transform projectionDebugObject;
    public bool projectionDebug = false;

    public bool Debug;

    // -- Privates -- //

    // Highlighting
    private Transform lastObject = null;
    private Material lastMaterial = null;

    // Start is called before the first frame update
    private void Awake()
    {
        // This will create our initial grid and place a cube inside each
        grid = new ThiccTapeman.Grid.Grid(gridSizeX, gridSizeY, tileSizeX, tileSizeY, position, rotation, (grid, x, y) =>
        {
            GameObject obj = Instantiate(tilePrefab);

            // Just naming and moving the object accordingly
            obj.name = $"grid_object ({x},{y})";
            obj.transform.SetParent(transform);
            obj.transform.position = grid.GridToWorldPosition(x, y, out Vector3 rotation);
            obj.transform.rotation = Quaternion.Euler(rotation);

            return new GridObject(grid, x, y, obj.transform);
        }, Debug);
    }

    private void Update()
    {
        HighlightObject();
        DebugProjection();
    }

    private void OnValidate()
    {
        UpdateGrid();
    }

    private void UpdateGrid()
    {

        if (grid == null) return;

        // Will move the grid, set the new tilesize, and rotate it
        grid.SetGridPosition(position);
        grid.SetGridRotation(rotation);
        grid.SetGridTileSize(tileSizeX, tileSizeY);
    }

    private void DebugProjection()
    {
        if (!projectionDebug) return;

        // Gets the rotation and position of the grid then projects the position onto a plane
        grid.GetGridRotation(out Vector3 rot);
        grid.GetGridPosition(out Vector3 pos);
        Vector3 projectedPosition = Vector3Utils.ProjectVectorOntoPlane(pos, Vector3Utils.CalculateNormalizedNormalFromEulerAngles(rot), highlight.position);

        projectionDebugObject.transform.position = projectedPosition;
    }

    // This will highlight the object that the highlightObject is closest too
    private void HighlightObject()
    {
        // Will set the last objects material to the material it had
        if (lastObject != null)
        {
            lastObject.GetComponent<MeshRenderer>().material = lastMaterial;
            lastObject = null;
            lastMaterial = null;
        }

        // Gets the grid object that is at the projected position
        GridObject go = (GridObject)grid.GetGridObject(highlight.position);

        if (go == null) return;

        // Makes it so we can change the material back
        lastObject = go.transform;
        lastMaterial = go.transform.GetComponent<MeshRenderer>().material;

        // Sets the new material
        go.transform.GetComponent<MeshRenderer>().material = highlightMaterial;
    }

    // Creates a new class implementing the gridobject class
    public class GridObject : GridObjectAbstract
    {
        public Transform transform;

        // Creates the base of the grid object, just assigning the transform and the base variables
        public GridObject(ThiccTapeman.Grid.Grid grid, int x, int y, Transform transform) : base(grid, x, y)
        {
            this.transform = transform;
        }

        // Will just move the object, this will need to be implemented by you.
        public override void Update()
        {

        }

        public override void RelocatePositions()
        {
            if (transform == null) return;

            transform.position = grid.GridToWorldPosition(x, y, out Vector3 rotation);
            transform.rotation = Quaternion.Euler(rotation);
        }

        // This is used when debug is turned on, it will be displayed as worldtext's at the grids cordinates
        public override string ToString()
        {
            return $"({x},{y})";
        }

        // Any other function can be added down below and you can access it from inside the gridObject that is returned from the grid
        // . . .
    }
}
