# Inventory System

## Scope
This document covers the runtime inventory data model, manager, and UI integration.

## Core Types
- `Inventory` is the data container and API for item operations.
- `InventorySlot` stores one item type and its amount.
- `ItemSO` defines a single item type (stacking, icon, display name).
- `InventoryLootSO` defines randomized loot tables.
- `InventoryManager` creates inventories and registers UIs by key.
- `InventoryUI` renders an `Inventory` into UI slots.
- `InventorySlotPrefab` drives per-slot UI and drag interactions.

## Inventory Data Model
`Inventory` is a plain C# class with a fixed-size `InventorySlot[]` array.

### Events
- `SlotChanged(index, slot)` fires after a specific slot changed.
- `InventoryChanged()` fires after any inventory change.
- `ItemDropped(item, amount)` fires when items are dropped via `DropFromSlot`.

### Common Operations
- `AddItem(item, amount)` and `AddItemReturnRemaining(item, amount)`.
- `RemoveItem(item, amount)` and `RemoveAt(slotIndex, amount)`.
- `MoveSlot(fromIndex, toIndex)` swaps or stacks when possible.
- `SplitStack(fromIndex, toIndex, splitAmount)`.
- `TransferToInventory(target, fromIndex, toIndex)`.
- `TransferAmountToInventory(target, fromIndex, toIndex, amount)`.
- `QuickMoveToInventory(target, fromIndex)`.
- `SplitStackToInventory(target, fromIndex)`.
- `PopulateInventory(amount, lootTable)`.
- `Clear()`.

### Slot Behavior
`InventorySlot` supports:
- Stack checks via `CanStack(item)`.
- Adds via `Add(item, amount)`.
- Removes via `Remove(amount)`.
- `IsEmpty` when no item or amount is zero.

## InventoryManager
`InventoryManager` is a scene singleton for creating inventories and binding UI.

- `CreateInventory(slotCount)` returns a new `Inventory` and stores it as active.
- `RegisterUI(key, ui)` registers a UI instance for lookup.
- `GetUI(key)` returns a registered UI.

## InventoryUI
`InventoryUI` renders an inventory into slot prefab instances.

Key behavior:
- `Bind(inventory)` attaches the UI and subscribes to inventory events.
- `Unbind()` detaches and removes event subscriptions.
- `ResizeSlots(count)` creates or removes slot instances.
- `GetOtherInventory(current)` returns another visible inventory for quick moves.

## InventorySlotPrefab (UI + Input)
`InventorySlotPrefab` handles interaction and drag/drop. It uses `InputManager` temp actions for shift and mouse state.

Interaction rules:
- Left click with Shift quick-moves the slot to another open inventory.
- Hover with Shift + left mouse also quick-moves.
- Left-drag moves the entire stack.
- Right-drag moves half the stack.
- Drop onto another slot moves or merges the stack.

Drag icon details:
- A runtime icon is created under the parent `Canvas`.
- The icon copies the amount text styling from the slot prefab when possible.

## ItemSO
`ItemSO` defines the data for a single item type:
- `displayName`, `icon`, `isStackable`, `maxStack`.
- `TryUseItem(user)` can be overridden to implement item use.

## InventoryLootSO
`InventoryLootSO` defines randomized loot draws:
- Each entry has `item`, `probability`, `minAmount`, `maxAmount`.
- `Inventory.PopulateInventory` draws items until the target count or attempts are exhausted.

## Example Usage
```csharp
var manager = InventoryManager.GetInstance();
var inventory = manager.CreateInventory(20);

inventory.AddItem(healthPotion, 3);

var ui = manager.GetUI("PlayerInventory");
inventory.AttachUI(ui);
```

## Common Pitfalls
- `InventoryUI` needs a valid `slotPrefab` and `slotsParent` reference.
- `InventorySlotPrefab` expects `Image` and `TMP_Text` components for icon/name/amount.
- `GetOtherInventory` returns the first other bound UI, so keep only the inventories you want open.

## File References
- `Assets/ThiccTapeman/Scripts/InventorySystem/Inventory.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/InventorySlot.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/ItemSO.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/InventoryLootSO.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/InventoryManager.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/InventoryUI.cs`
- `Assets/ThiccTapeman/Scripts/InventorySystem/InventorySlotPrefab.cs`
