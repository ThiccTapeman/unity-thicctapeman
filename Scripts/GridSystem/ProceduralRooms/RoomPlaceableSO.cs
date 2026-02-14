using UnityEngine;

namespace ThiccTapeman.GridProceduralRooms
{
    /// <summary>
    /// Base ScriptableObject for placeable room items.
    /// </summary>
    public abstract class RoomPlaceableSO : ScriptableObject
    {
        public string displayName;
        public GameObject prefab;
        public Vector2Int footprint = Vector2Int.one;
        public Vector3 positionOffset;
    }
}
