using UnityEngine;
using UnityEngine.Serialization;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Defines a single item type for inventory usage.
    /// </summary>
    [CreateAssetMenu(menuName = "ThiccTapeman/Inventory/Item", fileName = "Item")]
    public class ItemSO : ScriptableObject
    {
        /// <summary>
        /// Display name for the item.
        /// </summary>
        [FormerlySerializedAs("name")]
        public string displayName;
        /// <summary>
        /// True if this item can stack.
        /// </summary>
        public bool isStackable;
        /// <summary>
        /// Max stack size when stackable.
        /// </summary>
        [Min(1)] public int maxStack = 1;
        /// <summary>
        /// Item icon sprite.
        /// </summary>
        public Sprite icon;

        /// <summary>
        /// Attempts to use the item and returns whether it succeeded.
        /// </summary>
        /// <param name="user">The object using the item.</param>
        /// <returns>True if the item was used.</returns>
        public virtual bool TryUseItem(GameObject user)
        {
            return false;
        }

        /// <summary>
        /// Returns a readable name for the item.
        /// </summary>
        /// <returns>Item name.</returns>
        public override string ToString()
        {
            return $"{displayName}";
        }
    }
}
