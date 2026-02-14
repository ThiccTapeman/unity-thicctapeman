using UnityEngine;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Loot table definition for inventory population.
    /// </summary>
    [CreateAssetMenu(menuName = "ThiccTapeman/Inventory/Loot Table", fileName = "InventoryLoot")]
    public class InventoryLootSO : ScriptableObject
    {
        /// <summary>
        /// Single loot entry with draw probability and amount range.
        /// </summary>
        [System.Serializable]
        public class InventoryLootEntry
        {
            /// <summary>
            /// Item to draw.
            /// </summary>
            public ItemSO item;
            /// <summary>
            /// Probability for this entry to be selected.
            /// </summary>
            [Range(0f, 1f)] public float probability = 1f;
            /// <summary>
            /// Minimum stack amount when drawn.
            /// </summary>
            [Min(1)] public int minAmount = 1;
            /// <summary>
            /// Maximum stack amount when drawn.
            /// </summary>
            [Min(1)] public int maxAmount = 1;
        }

        /// <summary>
        /// Loot entries for this table.
        /// </summary>
        public InventoryLootEntry[] entries;
    }
}
