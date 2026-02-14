using System;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Holds item data for a single inventory slot.
    /// </summary>
    public class InventorySlot
    {
        /// <summary>
        /// Item stored in this slot.
        /// </summary>
        public ItemSO item;
        /// <summary>
        /// Amount of the stored item.
        /// </summary>
        public int amount;

        /// <summary>
        /// True if the slot is empty.
        /// </summary>
        public bool IsEmpty => item == null || amount <= 0;

        /// <summary>
        /// Clears the slot.
        /// </summary>
        public void Clear()
        {
            item = null;
            amount = 0;
        }

        /// <summary>
        /// Checks if another item can stack with this slot.
        /// </summary>
        /// <param name="other">Item to test.</param>
        /// <returns>True if stacking is possible.</returns>
        public bool CanStack(ItemSO other)
        {
            if (other == null || item == null) return false;
            if (!item.isStackable || !other.isStackable) return false;
            if (!ReferenceEquals(item, other)) return false;
            return amount < GetMaxStack(item);
        }

        /// <summary>
        /// Adds items into this slot and returns how many were added.
        /// </summary>
        /// <param name="newItem">Item to add.</param>
        /// <param name="addAmount">Amount to add.</param>
        /// <returns>Number of items added.</returns>
        public int Add(ItemSO newItem, int addAmount)
        {
            if (newItem == null || addAmount <= 0) return 0;

            if (IsEmpty)
            {
                item = newItem;
                int max = GetMaxStack(newItem);
                int added = newItem.isStackable ? Math.Min(addAmount, max) : 1;
                amount = added;
                return added;
            }

            if (!CanStack(newItem)) return 0;

            int space = GetMaxStack(item) - amount;
            int toAdd = Math.Min(space, addAmount);
            amount += toAdd;
            return toAdd;
        }

        /// <summary>
        /// Removes items from this slot and returns how many were removed.
        /// </summary>
        /// <param name="removeAmount">Amount to remove.</param>
        /// <returns>Number of items removed.</returns>
        public int Remove(int removeAmount)
        {
            if (IsEmpty || removeAmount <= 0) return 0;
            int removed = Math.Min(amount, removeAmount);
            amount -= removed;
            if (amount <= 0) Clear();
            return removed;
        }

        /// <summary>
        /// Returns the max stack size for an item.
        /// </summary>
        /// <param name="item">Item to check.</param>
        /// <returns>Max stack size.</returns>
        public static int GetMaxStack(ItemSO item)
        {
            if (item == null) return 1;
            if (!item.isStackable) return 1;
            return Math.Max(1, item.maxStack);
        }

        /// <summary>
        /// Debug string for slot content.
        /// </summary>
        /// <returns>Readable slot content.</returns>
        public override string ToString()
        {
            return "Slot: " + (item ? item.ToString() + ", " + amount : "null");
        }
    }
}
