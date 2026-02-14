using System;
using UnityEngine;

namespace ThiccTapeman.GridProceduralRooms
{
    /// <summary>
    /// Loot table definition for room placeables.
    /// </summary>
    [CreateAssetMenu(menuName = "ThiccTapeman/Rooms/Room Table", fileName = "RoomTable")]
    public class RoomTableSO : ScriptableObject
    {
        [Min(0)] public int minRolls = 1;
        [Min(0)] public int maxRolls = 1;

        [Serializable]
        public class RoomTableEntry
        {
            public RoomPlaceableSO item;
            [Min(0f)] public float weight = 1f;
            [Min(0)] public int minCount = 0;
            [Min(0)] public int maxCount = 1;

            public int GetRandomCount()
            {
                int min = Mathf.Max(0, minCount);
                int max = Mathf.Max(min, maxCount);
                return UnityEngine.Random.Range(min, max + 1);
            }
        }

        public RoomTableEntry[] entries;
    }
}
