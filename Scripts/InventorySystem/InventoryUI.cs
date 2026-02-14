using System.Collections.Generic;
using UnityEngine;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Renders an inventory into a UI grid of slot prefabs.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        private static readonly List<InventoryUI> Instances = new();

        /// <summary>
        /// Prefab used to render a slot.
        /// </summary>
        [SerializeField] private GameObject slotPrefab;
        /// <summary>
        /// Parent transform that holds slot instances.
        /// </summary>
        [SerializeField] private Transform slotsParent;

        private Inventory inventory;
        private readonly List<InventorySlotPrefab> slotUIs = new();

        /// <summary>
        /// Currently bound inventory.
        /// </summary>
        public Inventory BoundInventory => inventory;

        /// <summary>
        /// Registers this UI instance.
        /// </summary>
        private void OnEnable()
        {
            if (!Instances.Contains(this))
            {
                Instances.Add(this);
            }
        }

        /// <summary>
        /// Unregisters this UI instance.
        /// </summary>
        private void OnDisable()
        {
            Instances.Remove(this);
        }

        /// <summary>
        /// Finds another open inventory that is not the provided one.
        /// </summary>
        /// <param name="current">Current inventory.</param>
        /// <returns>Other inventory if available.</returns>
        public static Inventory GetOtherInventory(Inventory current)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                var ui = Instances[i];
                if (ui == null) continue;
                if (ui.inventory == null) continue;
                if (!ReferenceEquals(ui.inventory, current))
                {
                    return ui.inventory;
                }
            }

            return null;
        }

        /// <summary>
        /// Binds this UI to an inventory and builds its slots.
        /// </summary>
        /// <param name="target">Inventory to bind.</param>
        public void Bind(Inventory target)
        {
            if (ReferenceEquals(inventory, target)) return;
            Unbind();
            inventory = target;
            if (inventory == null) return;

            ResizeSlots(inventory.SlotCount);
            inventory.SlotChanged += HandleSlotChanged;
            inventory.InventoryChanged += HandleInventoryChanged;

            RefreshAll();
        }

        /// <summary>
        /// Unbinds from the current inventory.
        /// </summary>
        public void Unbind()
        {
            if (inventory == null) return;
            inventory.SlotChanged -= HandleSlotChanged;
            inventory.InventoryChanged -= HandleInventoryChanged;
            inventory = null;
        }

        /// <summary>
        /// Creates or removes slot instances to match the inventory size.
        /// </summary>
        /// <param name="count">Slot count.</param>
        private void ResizeSlots(int count)
        {
            if (slotPrefab == null) return;
            if (slotsParent == null) slotsParent = transform;

            // Trim extra slot instances.
            for (int i = slotUIs.Count - 1; i >= count; i--)
            {
                if (slotUIs[i] != null)
                {
                    Destroy(slotUIs[i].gameObject);
                }
                slotUIs.RemoveAt(i);
            }

            // Add missing slot instances.
            for (int i = slotUIs.Count; i < count; i++)
            {
                var instance = Instantiate(slotPrefab, slotsParent);
                var slotUI = instance.GetComponentInChildren<InventorySlotPrefab>();
                if (slotUI == null)
                {
                    slotUI = instance.AddComponent<InventorySlotPrefab>();
                }
                slotUI.Bind(inventory, i);
                slotUIs.Add(slotUI);
            }

            for (int i = 0; i < slotUIs.Count; i++)
            {
                slotUIs[i]?.Bind(inventory, i);
            }
        }

        /// <summary>
        /// Refreshes all slot visuals.
        /// </summary>
        private void RefreshAll()
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                slotUIs[i]?.Refresh();
            }
        }

        /// <summary>
        /// Refreshes a specific slot when it changes.
        /// </summary>
        /// <param name="index">Slot index.</param>
        /// <param name="slot">Slot data.</param>
        private void HandleSlotChanged(int index, InventorySlot slot)
        {
            if (index < 0 || index >= slotUIs.Count) return;
            slotUIs[index]?.Refresh();
        }

        /// <summary>
        /// Refreshes all slots when the inventory changes.
        /// </summary>
        private void HandleInventoryChanged()
        {
            RefreshAll();
        }
    }
}
