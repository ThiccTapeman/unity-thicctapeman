using System.Collections.Generic;
using UnityEngine;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// Singleton manager for creating inventories and registering UIs.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager instance;
        private Inventory activeInventory;
        private readonly Dictionary<string, InventoryUI> inventoryUIs = new();

        /// <summary>
        /// Gets or creates the singleton instance.
        /// </summary>
        /// <returns>InventoryManager instance.</returns>
        public static InventoryManager GetInstance()
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<InventoryManager>();
            if (instance != null) return instance;

            var go = new GameObject("InventoryManager");
            instance = go.AddComponent<InventoryManager>();
            return instance;
        }

        /// <summary>
        /// Enforces the singleton instance on awake.
        /// </summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        /// <summary>
        /// Creates a new active inventory.
        /// </summary>
        /// <param name="slotCount">Number of slots.</param>
        /// <returns>The created inventory.</returns>
        public Inventory CreateInventory(int slotCount = 20)
        {
            activeInventory = new Inventory(slotCount);
            return activeInventory;
        }

        /// <summary>
        /// Registers a UI instance under a key.
        /// </summary>
        /// <param name="key">UI key.</param>
        /// <param name="ui">UI instance.</param>
        public void RegisterUI(string key, InventoryUI ui)
        {
            if (string.IsNullOrWhiteSpace(key) || ui == null) return;
            inventoryUIs[key] = ui;
        }

        /// <summary>
        /// Gets a registered UI by key.
        /// </summary>
        /// <param name="key">UI key.</param>
        /// <returns>Registered UI or null.</returns>
        public InventoryUI GetUI(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            inventoryUIs.TryGetValue(key, out var ui);
            return ui;
        }

    }
}
