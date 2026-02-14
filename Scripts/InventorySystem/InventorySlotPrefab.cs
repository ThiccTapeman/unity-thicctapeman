using TMPro;
using ThiccTapeman.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThiccTapeman.Inventory
{
    /// <summary>
    /// UI component that represents a single inventory slot and handles input.
    /// </summary>
    public class InventorySlotPrefab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        /// <summary>
        /// Item icon image.
        /// </summary>
        [SerializeField] private Image iconImage;
        /// <summary>
        /// Item name text.
        /// </summary>
        [SerializeField] private TMP_Text nameText;
        /// <summary>
        /// Item amount text.
        /// </summary>
        [SerializeField] private TMP_Text amountText;
        /// <summary>
        /// Auto-assign references from children.
        /// </summary>
        [SerializeField] private bool autoFindReferences = true;
        /// <summary>
        /// Optional hover image to toggle.
        /// </summary>
        [SerializeField] private Image hoverImage;

        private Inventory inventory;
        private int slotIndex = -1;

        private static Inventory dragInventory;
        private static int dragIndex = -1;
        private static bool isDragging;
        private static InputItem leftShiftAction;
        private static InputItem rightShiftAction;
        private static InputItem leftMouseAction;
        private static bool shiftActionsInitialized;
        private static Image dragIcon;
        private static RectTransform dragRect;
        private static Canvas dragCanvas;
        private static TextMeshProUGUI dragAmountText;
        private static int dragAmount;
        private static bool splitDrag;

        /// <summary>
        /// Initializes references on startup.
        /// </summary>
        private void Awake()
        {
            if (autoFindReferences)
            {
                AutoFindReferences();
            }

            if (hoverImage != null)
            {
                hoverImage.enabled = false;
            }
        }

        /// <summary>
        /// Keeps references synced in the editor.
        /// </summary>
        private void OnValidate()
        {
            if (autoFindReferences)
            {
                AutoFindReferences();
            }
        }

        /// <summary>
        /// Binds this UI slot to an inventory slot index.
        /// </summary>
        /// <param name="targetInventory">Inventory to bind.</param>
        /// <param name="index">Slot index.</param>
        public void Bind(Inventory targetInventory, int index)
        {
            inventory = targetInventory;
            slotIndex = index;
            Refresh();
        }

        /// <summary>
        /// Refreshes the visuals from the bound inventory slot.
        /// </summary>
        public void Refresh()
        {
            if (inventory == null || slotIndex < 0) return;
            var slot = inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameText != null) nameText.text = string.Empty;
                if (amountText != null) amountText.text = string.Empty;
                return;
            }

            if (isDragging && ReferenceEquals(dragInventory, inventory) && dragIndex == slotIndex)
            {
                if (!splitDrag)
                {
                    if (iconImage != null) iconImage.enabled = false;
                    if (nameText != null) nameText.text = string.Empty;
                    if (amountText != null) amountText.text = string.Empty;
                    return;
                }
            }

            if (iconImage != null) iconImage.enabled = true;
            if (iconImage != null) iconImage.sprite = slot.item != null ? slot.item.icon : null;
            if (nameText != null) nameText.text = slot.item != null ? slot.item.displayName : string.Empty;

            if (amountText != null)
            {
                int displayAmount = slot.amount;
                if (isDragging && splitDrag && ReferenceEquals(dragInventory, inventory) && dragIndex == slotIndex)
                {
                    displayAmount = Mathf.Max(0, slot.amount - dragAmount);
                }

                amountText.text = displayAmount > 1 ? displayAmount.ToString() : string.Empty;
            }
        }

        /// <summary>
        /// Handles hover enter.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverImage != null) hoverImage.enabled = true;

            if (IsShiftHeld() && IsLeftMouseHeld())
            {
                // Shift-drag quick-move across inventories.
                var other = InventoryUI.GetOtherInventory(inventory);
                if (other != null)
                {
                    inventory.QuickMoveToInventory(other, slotIndex);
                }
            }
        }

        /// <summary>
        /// Handles hover exit.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverImage != null) hoverImage.enabled = false;
        }

        /// <summary>
        /// Handles click interactions for this slot.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (inventory == null || slotIndex < 0) return;
            if (eventData == null) return;

            if (eventData.button == PointerEventData.InputButton.Left && IsShiftHeld())
            {
                var other = InventoryUI.GetOtherInventory(inventory);
                if (other != null)
                {
                    inventory.QuickMoveToInventory(other, slotIndex);
                }
            }
        }

        /// <summary>
        /// Starts a drag operation.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (inventory == null || slotIndex < 0) return;
            var slot = inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                // Right-drag moves half the stack.
                int splitAmount = slot.amount / 2;
                if (splitAmount <= 0) return;
                splitDrag = true;
                dragAmount = splitAmount;
            }
            else
            {
                // Left-drag moves the full stack.
                splitDrag = false;
                dragAmount = slot.amount;
            }
            dragInventory = inventory;
            dragIndex = slotIndex;
            isDragging = true;
            Refresh();

            EnsureDragIcon();
            if (dragIcon != null)
            {
                dragIcon.sprite = slot.item != null ? slot.item.icon : null;
                dragIcon.enabled = dragIcon.sprite != null;
                if (dragAmountText != null)
                {
                    dragAmountText.text = dragAmount > 1 ? dragAmount.ToString() : string.Empty;
                    dragAmountText.enabled = dragIcon.enabled && dragAmount > 1;
                }
                UpdateDragIconPosition(eventData);
            }
        }

        /// <summary>
        /// Updates the drag icon while dragging.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnDrag(PointerEventData eventData)
        {
            if (dragIcon == null || !dragIcon.enabled) return;
            UpdateDragIconPosition(eventData);
        }

        /// <summary>
        /// Ends a drag operation.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnEndDrag(PointerEventData eventData)
        {
            dragInventory = null;
            dragIndex = -1;
            isDragging = false;
            if (dragIcon != null) dragIcon.enabled = false;
            if (dragAmountText != null)
            {
                dragAmountText.text = string.Empty;
                dragAmountText.enabled = false;
            }
            dragAmount = 0;
            splitDrag = false;
            Refresh();
        }

        /// <summary>
        /// Handles a drop onto this slot.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        public void OnDrop(PointerEventData eventData)
        {
            if (inventory == null || slotIndex < 0) return;
            if (dragInventory == null || dragIndex < 0) return;

            if (splitDrag && dragAmount > 0)
            {
                // Drop a split amount into the target slot.
                dragInventory.TransferAmountToInventory(inventory, dragIndex, slotIndex, dragAmount);
            }
            else
            {
                // Drop the full stack.
                dragInventory.TransferToInventory(inventory, dragIndex, slotIndex);
            }
            dragInventory = null;
            dragIndex = -1;
            splitDrag = false;
            dragAmount = 0;
        }

        /// <summary>
        /// Attempts to auto-assign UI references from children.
        /// </summary>
        private void AutoFindReferences()
        {
            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>(true);
            }

            if (hoverImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        if (images[i] == null) continue;
                        if (images[i] == iconImage) continue;
                        string name = images[i].name.ToLowerInvariant();
                        if (name.Contains("hover"))
                        {
                            hoverImage = images[i];
                            break;
                        }
                    }
                }
            }

            if (nameText == null || amountText == null)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                if (texts != null && texts.Length > 0)
                {
                    if (nameText == null)
                    {
                        nameText = FindByName(texts, "name") ?? texts[0];
                    }

                    if (amountText == null)
                    {
                        amountText = FindByName(texts, "amount") ?? FindByName(texts, "count");
                        if (amountText == null && texts.Length > 1) amountText = texts[1];
                    }
                }
            }
        }

        /// <summary>
        /// Finds a TMP text by name token.
        /// </summary>
        /// <param name="texts">Candidate texts.</param>
        /// <param name="token">Name token.</param>
        /// <returns>Matching text or null.</returns>
        private static TMP_Text FindByName(TMP_Text[] texts, string token)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                string name = texts[i].name.ToLowerInvariant();
                if (name.Contains(token)) return texts[i];
            }

            return null;
        }

        /// <summary>
        /// Finds the first empty slot index.
        /// </summary>
        /// <param name="target">Target inventory.</param>
        /// <returns>Slot index or -1.</returns>
        private static int FindFirstEmptySlot(Inventory target)
        {
            if (target == null) return -1;
            for (int i = 0; i < target.SlotCount; i++)
            {
                var slot = target.GetSlot(i);
                if (slot != null && slot.IsEmpty) return i;
            }

            return -1;
        }

        /// <summary>
        /// Creates the drag icon overlay if needed.
        /// </summary>
        private void EnsureDragIcon()
        {
            if (dragIcon != null) return;
            dragCanvas = GetComponentInParent<Canvas>();
            if (dragCanvas == null) return;

            // Runtime drag icon under the same canvas.
            var go = new GameObject("InventoryDragIcon");
            go.transform.SetParent(dragCanvas.transform, false);
            dragRect = go.AddComponent<RectTransform>();
            dragIcon = go.AddComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.enabled = false;

            var textGo = new GameObject("Amount");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            dragAmountText = textGo.AddComponent<TextMeshProUGUI>();
            dragAmountText.raycastTarget = false;
            dragAmountText.text = string.Empty;

            CopyAmountTextStyle(textRect);
        }

        /// <summary>
        /// Copies the amount text style onto the drag amount text.
        /// </summary>
        /// <param name="targetRect">Target rect to configure.</param>
        private void CopyAmountTextStyle(RectTransform targetRect)
        {
            if (amountText == null)
            {
                // Fallback placement if no amount text exists.
                targetRect.anchorMin = new Vector2(1f, 0f);
                targetRect.anchorMax = new Vector2(1f, 0f);
                targetRect.pivot = new Vector2(1f, 0f);
                targetRect.anchoredPosition = Vector2.zero;
                dragAmountText.fontSize = 18;
                dragAmountText.alignment = TextAlignmentOptions.BottomRight;
                return;
            }

            var sourceRect = amountText.rectTransform;
            // Match the prefab's amount text placement and styling.
            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.pivot = sourceRect.pivot;
            targetRect.anchoredPosition = sourceRect.anchoredPosition;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.localRotation = sourceRect.localRotation;
            targetRect.localScale = sourceRect.localScale;

            dragAmountText.font = amountText.font;
            dragAmountText.fontSize = amountText.fontSize;
            dragAmountText.fontStyle = amountText.fontStyle;
            dragAmountText.alignment = amountText.alignment;
            dragAmountText.color = amountText.color;
            dragAmountText.enableAutoSizing = amountText.enableAutoSizing;
            dragAmountText.fontSizeMin = amountText.fontSizeMin;
            dragAmountText.fontSizeMax = amountText.fontSizeMax;
            dragAmountText.material = amountText.fontSharedMaterial;
        }

        /// <summary>
        /// Updates the drag icon position to match the pointer.
        /// </summary>
        /// <param name="eventData">Pointer event.</param>
        private void UpdateDragIconPosition(PointerEventData eventData)
        {
            if (dragRect == null || dragCanvas == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                dragRect.anchoredPosition = localPoint;
            }
        }

        /// <summary>
        /// Checks whether either shift key is held.
        /// </summary>
        /// <returns>True if shift is held.</returns>
        private static bool IsShiftHeld()
        {
            EnsureShiftActions();
            bool leftHeld = leftShiftAction != null && leftShiftAction.ReadValue<float>() > 0.5f;
            bool rightHeld = rightShiftAction != null && rightShiftAction.ReadValue<float>() > 0.5f;
            return leftHeld || rightHeld;
        }

        /// <summary>
        /// Checks whether the left mouse button is held.
        /// </summary>
        /// <returns>True if left mouse is held.</returns>
        private static bool IsLeftMouseHeld()
        {
            EnsureMouseActions();
            return leftMouseAction != null && leftMouseAction.ReadValue<float>() > 0.5f;
        }

        /// <summary>
        /// Ensures mouse input actions are created.
        /// </summary>
        private static void EnsureMouseActions()
        {
            if (leftMouseAction != null) return;
            var inputManager = InputManager.GetInstance();
            if (inputManager == null) return;
            leftMouseAction = inputManager.GetTempAction("Inventory.LeftMouse", "<Mouse>/leftButton");
        }

        /// <summary>
        /// Ensures shift input actions are created.
        /// </summary>
        private static void EnsureShiftActions()
        {
            if (shiftActionsInitialized) return;
            shiftActionsInitialized = true;

            var inputManager = InputManager.GetInstance();
            if (inputManager == null) return;

            if (inputManager.tempActions != null)
            {
                inputManager.tempActions.Disable();
                leftShiftAction = inputManager.GetTempAction("Inventory.LeftShift", "<Keyboard>/leftShift");
                rightShiftAction = inputManager.GetTempAction("Inventory.RightShift", "<Keyboard>/rightShift");
                inputManager.tempActions.Enable();
            }
        }
    }
}
