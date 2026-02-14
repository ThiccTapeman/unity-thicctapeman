using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Main inventory class used for storing the items
    /// </summary>
    public class Inventory
    {
        #region Fields
        private InventorySlot[] slots;
        private InventoryUI inventoryUI;
        #endregion

        #region Events
        /// <summary>
        /// Fired when a specific slot is updated.
        /// </summary>
        public event Action<int, InventorySlot> SlotChanged;
        /// <summary>
        /// Fired when the inventory content changes.
        /// </summary>
        public event Action InventoryChanged;
        /// <summary>
        /// Fired when items are dropped from a slot.
        /// </summary>
        public event Action<ItemSO, int> ItemDropped;
        #endregion

        #region Initialization
        /// <summary>
        /// Creates an inventory with a fixed amount of slots.
        /// </summary>
        /// <param name="amountSlots">Total number of slots.</param>
        public Inventory(int amountSlots)
        {
            slots = new InventorySlot[amountSlots];
            for (int i = 0; i < amountSlots; i++)
            {
                slots[i] = new InventorySlot();
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Total number of slots in this inventory.
        /// </summary>
        public int SlotCount => slots.Length;
        #endregion

        #region Slot Access
        /// <summary>
        /// Gets the slot at the given index.
        /// </summary>
        /// <param name="slotIndex">Slot index.</param>
        /// <returns>The slot if valid; otherwise null.</returns>
        public InventorySlot GetSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < slots.Length)
            {
                return slots[slotIndex];
            }
            return null;
        }
        #endregion

        #region UI Binding
        /// <summary>
        /// Attaches a UI by manager key.
        /// </summary>
        /// <param name="uiKey">Registered UI key.</param>
        public void AttachUI(string uiKey)
        {
            var manager = InventoryManager.GetInstance();
            AttachUI(manager.GetUI(uiKey));
        }

        /// <summary>
        /// Attaches a UI instance to this inventory.
        /// </summary>
        /// <param name="ui">UI to bind.</param>
        public void AttachUI(InventoryUI ui)
        {
            if (ReferenceEquals(inventoryUI, ui)) return;
            DetachUI();
            inventoryUI = ui;
            inventoryUI?.Bind(this);
        }

        /// <summary>
        /// Detaches the currently bound UI, if any.
        /// </summary>
        public void DetachUI()
        {
            if (inventoryUI == null) return;
            inventoryUI.Unbind();
            inventoryUI = null;
        }
        #endregion

        #region Item Operations
        /// <summary>
        /// Adds an item stack to the inventory.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <param name="amount">Amount to add.</param>
        /// <returns>True if fully added; otherwise false.</returns>
        public bool AddItem(ItemSO item, int amount = 1)
        {
            return AddItemReturnRemaining(item, amount) == 0;
        }

        /// <summary>
        /// Removes a specific item from the inventory.
        /// </summary>
        /// <param name="item">Item to remove.</param>
        /// <param name="amount">Amount to remove.</param>
        /// <returns>True if fully removed; otherwise false.</returns>
        public bool RemoveItem(ItemSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            int remaining = amount;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty || !ReferenceEquals(slots[i].item, item)) continue;
                int removed = slots[i].Remove(remaining);
                if (removed > 0)
                {
                    remaining -= removed;
                    RaiseSlotChanged(i);
                }
            }

            if (remaining != amount) RaiseInventoryChanged();
            return remaining == 0;
        }

        /// <summary>
        /// Removes items from a specific slot.
        /// </summary>
        /// <param name="slotIndex">Slot index.</param>
        /// <param name="amount">Amount to remove.</param>
        /// <returns>True if any items were removed.</returns>
        public bool RemoveAt(int slotIndex, int amount = 1)
        {
            if (!IsValidIndex(slotIndex) || amount <= 0) return false;
            int removed = slots[slotIndex].Remove(amount);
            if (removed > 0)
            {
                RaiseSlotChanged(slotIndex);
                RaiseInventoryChanged();
            }
            return removed > 0;
        }

        /// <summary>
        /// Swaps two slot contents.
        /// </summary>
        /// <param name="indexA">First slot index.</param>
        /// <param name="indexB">Second slot index.</param>
        /// <returns>True if swap occurred.</returns>
        public bool SwapSlots(int indexA, int indexB)
        {
            if (!IsValidIndex(indexA) || !IsValidIndex(indexB)) return false;
            if (indexA == indexB) return false;

            SwapSlotContents(slots[indexA], slots[indexB]);
            RaiseSlotChanged(indexA);
            RaiseSlotChanged(indexB);
            RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Moves a slot into another slot, stacking if possible.
        /// </summary>
        /// <param name="fromIndex">Source slot index.</param>
        /// <param name="toIndex">Target slot index.</param>
        /// <returns>True if any change occurred.</returns>
        public bool MoveSlot(int fromIndex, int toIndex)
        {
            if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex)) return false;
            if (fromIndex == toIndex) return false;

            var from = slots[fromIndex];
            var to = slots[toIndex];

            if (from.IsEmpty) return false;

            if (to.IsEmpty)
            {
                CopySlotContents(from, to);
                from.Clear();
                RaiseSlotChanged(fromIndex);
                RaiseSlotChanged(toIndex);
                RaiseInventoryChanged();
                return true;
            }

            if (to.CanStack(from.item))
            {
                int moved = to.Add(from.item, from.amount);
                if (moved > 0)
                {
                    from.Remove(moved);
                    RaiseSlotChanged(fromIndex);
                    RaiseSlotChanged(toIndex);
                    RaiseInventoryChanged();
                    return true;
                }
            }

            return SwapSlots(fromIndex, toIndex);
        }

        /// <summary>
        /// Splits a stack into another slot.
        /// </summary>
        /// <param name="fromIndex">Source slot index.</param>
        /// <param name="toIndex">Target slot index.</param>
        /// <param name="splitAmount">Amount to move.</param>
        /// <returns>True if any items moved.</returns>
        public bool SplitStack(int fromIndex, int toIndex, int splitAmount)
        {
            if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex)) return false;
            if (fromIndex == toIndex || splitAmount <= 0) return false;

            var from = slots[fromIndex];
            var to = slots[toIndex];
            if (from.IsEmpty || !from.item.isStackable) return false;
            if (!to.IsEmpty && !to.CanStack(from.item)) return false;

            int moved = to.Add(from.item, splitAmount);
            if (moved <= 0) return false;

            from.Remove(moved);
            RaiseSlotChanged(fromIndex);
            RaiseSlotChanged(toIndex);
            RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Removes items from a slot and fires a drop event.
        /// </summary>
        /// <param name="slotIndex">Slot index.</param>
        /// <param name="amount">Amount to drop.</param>
        /// <returns>True if any items were dropped.</returns>
        public bool DropFromSlot(int slotIndex, int amount = 1)
        {
            if (!IsValidIndex(slotIndex) || amount <= 0) return false;

            var slot = slots[slotIndex];
            if (slot.IsEmpty) return false;

            ItemSO item = slot.item;
            int removed = slot.Remove(amount);
            if (removed <= 0) return false;

            RaiseSlotChanged(slotIndex);
            RaiseInventoryChanged();
            ItemDropped?.Invoke(item, removed);
            return true;
        }
        #endregion

        #region Inventory Maintenance
        /// <summary>
        /// Clears all slots and emits change events.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    slots[i].Clear();
                    RaiseSlotChanged(i);
                }
            }

            RaiseInventoryChanged();
        }
        #endregion

        #region Add Helpers
        /// <summary>
        /// Adds items and returns how many could not be added.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <param name="amount">Amount to add.</param>
        /// <returns>Remaining amount that did not fit.</returns>
        public int AddItemReturnRemaining(ItemSO item, int amount)
        {
            if (item == null || amount <= 0) return amount;
            int remaining = amount;

            if (item.isStackable)
            {
                // Fill existing stacks first.
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                {
                    if (!slots[i].CanStack(item)) continue;
                    int added = slots[i].Add(item, remaining);
                    if (added > 0)
                    {
                        remaining -= added;
                        RaiseSlotChanged(i);
                    }
                }
            }

            // Then fill empty slots.
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int added = slots[i].Add(item, remaining);
                if (added > 0)
                {
                    remaining -= added;
                    RaiseSlotChanged(i);
                }
            }

            if (remaining != amount) RaiseInventoryChanged();
            return remaining;
        }
        #endregion

        #region Inventory Transfers
        /// <summary>
        /// Transfers a slot to another inventory slot.
        /// </summary>
        /// <param name="target">Target inventory.</param>
        /// <param name="fromIndex">Source slot index.</param>
        /// <param name="toIndex">Target slot index.</param>
        /// <returns>True if any change occurred.</returns>
        public bool TransferToInventory(Inventory target, int fromIndex, int toIndex)
        {
            if (target == null) return false;
            if (!IsValidIndex(fromIndex) || !target.IsValidIndex(toIndex)) return false;

            var from = slots[fromIndex];
            var to = target.slots[toIndex];

            if (from.IsEmpty) return false;

            if (ReferenceEquals(target, this))
            {
                // Same inventory: treat as move.
                return MoveSlot(fromIndex, toIndex);
            }

            if (to.IsEmpty)
            {
                // Empty target slot takes full stack.
                CopySlotContents(from, to);
                from.Clear();
                RaiseSlotChanged(fromIndex);
                target.RaiseSlotChanged(toIndex);
                RaiseInventoryChanged();
                target.RaiseInventoryChanged();
                return true;
            }

            if (to.CanStack(from.item))
            {
                // Merge stacks where possible.
                int moved = to.Add(from.item, from.amount);
                if (moved > 0)
                {
                    from.Remove(moved);
                    RaiseSlotChanged(fromIndex);
                    target.RaiseSlotChanged(toIndex);
                    RaiseInventoryChanged();
                    target.RaiseInventoryChanged();
                    return true;
                }
            }

            // Fallback to swap when stacks cannot merge.
            SwapSlotContents(from, to);
            RaiseSlotChanged(fromIndex);
            target.RaiseSlotChanged(toIndex);
            RaiseInventoryChanged();
            target.RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Transfers a specific amount into another inventory slot.
        /// </summary>
        /// <param name="target">Target inventory.</param>
        /// <param name="fromIndex">Source slot index.</param>
        /// <param name="toIndex">Target slot index.</param>
        /// <param name="amount">Amount to transfer.</param>
        /// <returns>True if any items were transferred.</returns>
        public bool TransferAmountToInventory(Inventory target, int fromIndex, int toIndex, int amount)
        {
            if (target == null) return false;
            if (amount <= 0) return false;
            if (!IsValidIndex(fromIndex) || !target.IsValidIndex(toIndex)) return false;

            var from = slots[fromIndex];
            if (from.IsEmpty) return false;
            amount = Mathf.Min(amount, from.amount);

            if (ReferenceEquals(target, this))
            {
                // Same inventory: split into target slot.
                return SplitStack(fromIndex, toIndex, amount);
            }

            var to = target.slots[toIndex];
            if (to.IsEmpty)
            {
                // Place amount into an empty target slot.
                to.item = from.item;
                to.amount = amount;
                from.Remove(amount);
                RaiseSlotChanged(fromIndex);
                target.RaiseSlotChanged(toIndex);
                RaiseInventoryChanged();
                target.RaiseInventoryChanged();
                return true;
            }

            if (to.CanStack(from.item))
            {
                // Add to existing stack if possible.
                int moved = to.Add(from.item, amount);
                if (moved > 0)
                {
                    from.Remove(moved);
                    RaiseSlotChanged(fromIndex);
                    target.RaiseSlotChanged(toIndex);
                    RaiseInventoryChanged();
                    target.RaiseInventoryChanged();
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Quick Move
        /// <summary>
        /// Quick-moves a slot to another inventory.
        /// </summary>
        /// <param name="target">Target inventory.</param>
        /// <param name="fromIndex">Source slot index.</param>
        /// <returns>True if any items moved.</returns>
        public bool QuickMoveToInventory(Inventory target, int fromIndex)
        {
            if (target == null) return false;
            if (!IsValidIndex(fromIndex)) return false;

            var from = slots[fromIndex];
            if (from.IsEmpty) return false;

            int remaining = target.AddItemReturnRemaining(from.item, from.amount);
            int moved = from.amount - remaining;
            if (moved <= 0) return false;

            from.Remove(moved);
            RaiseSlotChanged(fromIndex);
            RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Splits a stack into another inventory.
        /// </summary>
        /// <param name="target">Target inventory.</param>
        /// <param name="fromIndex">Source slot index.</param>
        /// <returns>True if any items moved.</returns>
        public bool SplitStackToInventory(Inventory target, int fromIndex)
        {
            if (target == null) return false;
            if (!IsValidIndex(fromIndex)) return false;

            var from = slots[fromIndex];
            if (from.IsEmpty || from.amount <= 1 || !from.item.isStackable) return false;

            int splitAmount = from.amount / 2;
            int remaining = target.AddItemReturnRemaining(from.item, splitAmount);
            int moved = splitAmount - remaining;
            if (moved <= 0) return false;

            from.Remove(moved);
            RaiseSlotChanged(fromIndex);
            RaiseInventoryChanged();
            return true;
        }
        #endregion

        #region Randomization
        /// <summary>
        /// Fills the inventory with a random selection from a loot table.
        /// </summary>
        /// <param name="amount">Number of items to draw.</param>
        /// <param name="loot">Loot table to draw from.</param>
        /// <returns>Number of items successfully added.</returns>
        public int PopulateInventory(int amount, InventoryLootSO loot)
        {
            if (loot == null || amount <= 0) return 0;
            if (loot.entries == null || loot.entries.Length == 0) return 0;

            int added = 0;
            int attempts = 0;
            int maxAttempts = amount * 10;

            while (added < amount && attempts < maxAttempts)
            {
                attempts++;
                var entry = loot.entries[Random.Range(0, loot.entries.Length)];
                if (entry == null || entry.item == null) continue;

                float roll = Random.value;
                if (roll > entry.probability) continue;

                int min = Mathf.Max(1, entry.minAmount);
                int max = Mathf.Max(min, entry.maxAmount);
                int stackAmount = entry.item.isStackable ? Random.Range(min, max + 1) : 1;

                if (AddItemRandom(entry.item, stackAmount))
                {
                    added++;
                }
            }

            return added;
        }
        #endregion

        #region Private Helpers
        private void RaiseSlotChanged(int index)
        {
            SlotChanged?.Invoke(index, slots[index]);
        }

        private void RaiseInventoryChanged()
        {
            InventoryChanged?.Invoke();
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < slots.Length;
        }

        private static void SwapSlotContents(InventorySlot a, InventorySlot b)
        {
            var tempItem = a.item;
            var tempAmount = a.amount;
            a.item = b.item;
            a.amount = b.amount;
            b.item = tempItem;
            b.amount = tempAmount;
        }

        private static void CopySlotContents(InventorySlot source, InventorySlot target)
        {
            target.item = source.item;
            target.amount = source.amount;
        }

        private bool AddItemRandom(ItemSO item, int amount)
        {
            if (item == null || amount <= 0) return false;
            int remaining = amount;

            // Shuffle slot indices to place items randomly.
            var indices = new List<int>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                indices.Add(i);
            }

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int i = 0; i < indices.Count && remaining > 0; i++)
            {
                int index = indices[i];
                int added = slots[index].Add(item, remaining);
                if (added > 0)
                {
                    remaining -= added;
                    RaiseSlotChanged(index);
                }
            }

            if (remaining != amount) RaiseInventoryChanged();
            return remaining == 0;
        }
        #endregion
    }
}
