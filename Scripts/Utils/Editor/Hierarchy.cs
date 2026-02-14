using System;
using System.Collections.Generic;
using UnityEngine;
using ThiccTapeman.Input;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ThiccTapeman.EditorUI
{
#if UNITY_EDITOR
    public enum ContextMenuMode
    {
        Default,
        None
    }

    public enum HierarchyAction
    {
        Rename,
        Delete
    }

    public sealed class Hierarchy
    {
        private readonly Dictionary<string, bool> foldouts = new();
        private readonly List<HierarchyItem> items = new();
        private readonly List<HierarchyContextSection> contextSections = new();
        private readonly Dictionary<HierarchyAction, InputItem> keybinds = new();
        private readonly Dictionary<HierarchyAction, Action<HierarchyItem>> actionHandlers = new();
        private string searchText = string.Empty;
        private Func<HierarchyItem, string> searchSelector;
        private Func<HierarchyItem, bool> filter;

        private HierarchyItem pendingDragItem;
        private bool showDropLine;
        private float dropLineY;

        public string InternalDragKey { get; set; } = "HierarchyItem";
        public Func<HierarchyItem, bool> CanStartDrag { get; set; }
        public Action<object, HierarchyRow?> OnInternalDrop { get; set; }
        public Func<UnityEngine.Object[], bool> CanAcceptExternal { get; set; }
        public Action<UnityEngine.Object[], HierarchyRow?> OnExternalDrop { get; set; }
        public Action<HierarchyItem> OnSelect { get; set; }
        public Action<HierarchyItem> OnDoubleClick { get; set; }
        public Action<HierarchyItem> OnTripleClick { get; set; }
        public HierarchyItem SelectedItem { get; private set; }

        public static Hierarchy New() => new Hierarchy();

        public static Hierarchy New(Hierarchy existing)
        {
            if (existing == null) return new Hierarchy();
            existing.items.Clear();
            existing.contextSections.Clear();
            return existing;
        }

        public IReadOnlyList<HierarchyItem> Items => items;

        public void SetKeybind(HierarchyAction action, InputItem item)
        {
            keybinds[action] = item;
        }

        public void SetActionHandler(HierarchyAction action, Action<HierarchyItem> handler)
        {
            actionHandlers[action] = handler;
        }

        public void SetSearch(string text, Func<HierarchyItem, string> selector = null)
        {
            searchText = text ?? string.Empty;
            searchSelector = selector;
        }

        public void SetFilter(Func<HierarchyItem, bool> predicate)
        {
            filter = predicate;
        }

        internal bool TryGetActionHandler(HierarchyAction action, out Action<HierarchyItem> handler)
        {
            return actionHandlers.TryGetValue(action, out handler);
        }

        public HierarchyItem Folder(string name)
        {
            return new HierarchyItem(this, name, isFolder: true);
        }

        public HierarchyItem Item(string name, Texture icon, Action<Rect> style = null)
        {
            var item = new HierarchyItem(this, name, isFolder: false)
            {
                Icon = icon
            };
            if (style != null)
            {
                item.Style(style);
            }
            return item;
        }

        public void AddItem(HierarchyItem item)
        {
            if (item == null) return;
            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }

        public void Move(HierarchyItem item, HierarchyLocation location)
        {
            if (item == null) return;
            Move(item, location.index);
        }

        public void Move(HierarchyItem item, int index)
        {
            if (item == null) return;
            int current = items.IndexOf(item);
            if (current < 0) return;
            index = Mathf.Clamp(index, 0, items.Count - 1);
            if (current == index) return;
            items.RemoveAt(current);
            items.Insert(index, item);
        }

        public void ContextMenu(string header, Action<GenericMenu> build)
        {
            if (build == null) return;
            contextSections.Add(new HierarchyContextSection(header, build));
        }

        public void DrawFolderRow(string key, int depth, int childCount, Func<Rect> drawName, out Rect foldoutRect, out Rect nameRect)
        {
            foldoutRect = Rect.zero;
            nameRect = Rect.zero;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14f);

                if (!foldouts.TryGetValue(key, out bool open))
                {
                    open = true;
                    foldouts[key] = true;
                }

                Rect foldRect = GUILayoutUtility.GetRect(12f, EditorGUIUtility.singleLineHeight, GUILayout.Width(12f));
                if (childCount > 0)
                {
                    bool newOpen = EditorGUI.Foldout(foldRect, open, GUIContent.none, true);
                    if (newOpen != open) foldouts[key] = newOpen;
                }
                foldoutRect = foldRect;

                if (drawName != null)
                {
                    GUILayout.Space(2f);
                    nameRect = drawName.Invoke();
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{childCount} items", EditorStyles.miniLabel, GUILayout.Width(70));
            }
        }

        public bool IsVisible(IReadOnlyList<string> ancestorIds)
        {
            if (ancestorIds == null || ancestorIds.Count == 0) return true;

            for (int i = 0; i < ancestorIds.Count; i++)
            {
                string id = ancestorIds[i];
                if (!foldouts.TryGetValue(id, out bool open))
                {
                    open = true;
                    foldouts[id] = true;
                }

                if (!open) return false;
            }

            return true;
        }

        public bool ShouldDisplay(HierarchyItem item)
        {
            if (item == null) return false;
            if (filter != null && !filter(item)) return false;

            if (string.IsNullOrWhiteSpace(searchText)) return true;

            string haystack = searchSelector != null ? searchSelector(item) : item.Name;
            if (string.IsNullOrWhiteSpace(haystack)) return false;
            return haystack.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void ShowContextMenu()
        {
            if (contextSections.Count == 0) return;

            var menu = new GenericMenu();
            for (int i = 0; i < contextSections.Count; i++)
            {
                var section = contextSections[i];
                if (!string.IsNullOrWhiteSpace(section.header))
                {
                    menu.AddDisabledItem(new GUIContent(section.header));
                }
                section.build?.Invoke(menu);
                if (i < contextSections.Count - 1)
                {
                    menu.AddSeparator("");
                }
            }
            menu.ShowAsContext();
        }

        public void ShowContextMenu(HierarchyItem item)
        {
            if (item == null) return;

            var menu = new GenericMenu();
            item.BuildMenu(menu);
            menu.ShowAsContext();
        }

        public void HandleSelection(IReadOnlyList<HierarchyRow> rows)
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0) return;
            if (GUIUtility.hotControl != 0) return;

            var row = FindRowUnderMouse(rows, evt.mousePosition);
            if (!row.HasValue) return;

            SelectedItem = row.Value.item;
            OnSelect?.Invoke(row.Value.item);

            if (evt.clickCount >= 3)
            {
                OnTripleClick?.Invoke(row.Value.item);
            }
            else if (evt.clickCount == 2)
            {
                OnDoubleClick?.Invoke(row.Value.item);
            }

            evt.Use();
        }

        public void HandleKeybinds(HierarchyItem selected)
        {
            if (selected == null) return;
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown) return;

            foreach (var pair in keybinds)
            {
                if (!MatchesInputItem(evt, pair.Value)) continue;
                if (actionHandlers.TryGetValue(pair.Key, out var handler))
                {
                    handler?.Invoke(selected);
                    evt.Use();
                    return;
                }
            }
        }

        public void HandleDragAndDrop(IReadOnlyList<HierarchyRow> rows)
        {
            Event evt = Event.current;
            if (evt == null) return;

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                var row = FindRowUnderMouse(rows, evt.mousePosition);
                if (row.HasValue && (CanStartDrag == null || CanStartDrag(row.Value.item)))
                {
                    pendingDragItem = row.Value.item;
                }
                return;
            }

            if (evt.type == EventType.MouseDrag && pendingDragItem != null)
            {
                var payload = pendingDragItem.BuildDragPayload?.Invoke();
                if (payload != null)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = payload.Value.objectReferences ?? Array.Empty<UnityEngine.Object>();
                    DragAndDrop.SetGenericData(payload.Value.key ?? InternalDragKey, payload.Value.data ?? pendingDragItem);
                    DragAndDrop.StartDrag(payload.Value.title ?? pendingDragItem.Name);
                    evt.Use();
                }
                return;
            }

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                var internalData = DragAndDrop.GetGenericData(InternalDragKey);
                bool isInternal = internalData != null;
                if (isInternal)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    UpdateDropLine(rows, evt.mousePosition);

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        var row = FindRowUnderMouse(rows, evt.mousePosition);
                        OnInternalDrop?.Invoke(internalData, row);
                        ClearDragState();
                    }

                    evt.Use();
                    return;
                }

                var objects = DragAndDrop.objectReferences;
                if (objects == null || objects.Length == 0) return;
                if (CanAcceptExternal != null && !CanAcceptExternal(objects)) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                UpdateDropLine(rows, evt.mousePosition);

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    var row = FindRowUnderMouse(rows, evt.mousePosition);
                    OnExternalDrop?.Invoke(objects, row);
                    ClearDragState();
                }

                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseUp)
            {
                ClearDragState();
            }
        }

        public void DrawDropIndicator()
        {
            if (!showDropLine || Event.current.type != EventType.Repaint) return;

            Rect lineRect = new Rect(0f, dropLineY, EditorGUIUtility.currentViewWidth, 1.5f);
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.7f, 1f, 0.8f));
        }

        private void ClearDragState()
        {
            pendingDragItem = null;
            showDropLine = false;
        }

        private static HierarchyRow? FindRowUnderMouse(IReadOnlyList<HierarchyRow> rows, Vector2 mousePosition)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].rect.Contains(mousePosition))
                {
                    return rows[i];
                }
            }

            return null;
        }

        private void UpdateDropLine(IReadOnlyList<HierarchyRow> rows, Vector2 mousePosition)
        {
            showDropLine = false;
            dropLineY = 0f;

            var row = FindRowUnderMouse(rows, mousePosition);
            if (!row.HasValue) return;

            Rect rect = row.Value.rect;
            float mid = rect.y + rect.height * 0.5f;
            dropLineY = mousePosition.y < mid ? rect.y - 1f : rect.yMax + 1f;
            showDropLine = true;
        }

        private static bool MatchesInputItem(Event evt, InputItem item)
        {
            if (evt == null || item == null) return false;
            var action = item.inputAction;
            if (action == null) return false;

            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;

                string path = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
                if (string.IsNullOrWhiteSpace(path)) continue;

                var keyCode = KeyCodeFromBinding(path);
                if (keyCode == KeyCode.None) continue;

                if (evt.keyCode == keyCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static KeyCode KeyCodeFromBinding(string path)
        {
            int slashIndex = path.LastIndexOf('/');
            string key = slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
            key = key.Trim().ToLowerInvariant();

            if (key.Length == 1)
            {
                char c = key[0];
                if (c >= 'a' && c <= 'z')
                {
                    return (KeyCode)Enum.Parse(typeof(KeyCode), c.ToString().ToUpperInvariant());
                }
                if (c >= '0' && c <= '9')
                {
                    return (KeyCode)Enum.Parse(typeof(KeyCode), $"Alpha{c}");
                }
            }

            return key switch
            {
                "delete" => KeyCode.Delete,
                "backspace" => KeyCode.Backspace,
                "space" => KeyCode.Space,
                "enter" => KeyCode.Return,
                "numpadenter" => KeyCode.KeypadEnter,
                "tab" => KeyCode.Tab,
                "escape" => KeyCode.Escape,
                "f1" => KeyCode.F1,
                "f2" => KeyCode.F2,
                "f3" => KeyCode.F3,
                "f4" => KeyCode.F4,
                "f5" => KeyCode.F5,
                "f6" => KeyCode.F6,
                "f7" => KeyCode.F7,
                "f8" => KeyCode.F8,
                "f9" => KeyCode.F9,
                "f10" => KeyCode.F10,
                "f11" => KeyCode.F11,
                "f12" => KeyCode.F12,
                _ => KeyCode.None
            };
        }

        private readonly struct HierarchyContextSection
        {
            public readonly string header;
            public readonly Action<GenericMenu> build;

            public HierarchyContextSection(string header, Action<GenericMenu> build)
            {
                this.header = header;
                this.build = build;
            }
        }
    }

    public readonly struct HierarchyLocation
    {
        public readonly int index;

        public HierarchyLocation(int index)
        {
            this.index = index;
        }
    }

    public sealed class HierarchyItem
    {
        private readonly Hierarchy owner;
        private readonly List<HierarchyContextSection> contextSections = new();

        public string Name { get; }
        public bool IsFolder { get; }
        public string Id { get; private set; }
        public int Depth { get; private set; }
        public int ChildCount { get; private set; }
        public Texture Icon { get; set; }
        public Action<Rect> StyleDraw { get; private set; }
        public Func<Rect> DrawContent { get; private set; }
        public object Data { get; private set; }
        public Func<HierarchyDragPayload?> BuildDragPayload { get; private set; }
        public ContextMenuMode ContextMenuMode { get; private set; } = ContextMenuMode.Default;

        public HierarchyItem(Hierarchy owner, string name, bool isFolder)
        {
            this.owner = owner;
            Name = name;
            IsFolder = isFolder;
        }

        public HierarchyItem IdOf(string id)
        {
            Id = id;
            return this;
        }

        public HierarchyItem DepthOf(int depth)
        {
            Depth = depth;
            return this;
        }

        public HierarchyItem Children(int count)
        {
            ChildCount = count;
            return this;
        }

        public HierarchyItem Draw(Func<Rect> draw)
        {
            DrawContent = draw;
            return this;
        }

        public HierarchyItem Style(Action<Rect> draw)
        {
            StyleDraw = draw;
            return this;
        }

        public HierarchyItem ContextMenu(string header, Action<GenericMenu> build)
        {
            if (build == null) return this;
            contextSections.Add(new HierarchyContextSection(header, build));
            return this;
        }

        public HierarchyItem WithData(object data)
        {
            Data = data;
            return this;
        }

        public HierarchyItem SetContextMenu(ContextMenuMode mode, List<string> items, List<Action> actions)
        {
            ContextMenuMode = mode;
            contextSections.Clear();

            if (items != null && actions != null)
            {
                int count = Mathf.Min(items.Count, actions.Count);
                for (int i = 0; i < count; i++)
                {
                    string label = items[i];
                    Action action = actions[i];
                    if (string.IsNullOrWhiteSpace(label) || action == null) continue;
                    contextSections.Add(new HierarchyContextSection(label, menu =>
                    {
                        menu.AddItem(new GUIContent(label), false, () => action.Invoke());
                    }));
                }
            }

            return this;
        }

        public HierarchyItem SetDragPayload(Func<HierarchyDragPayload?> builder)
        {
            BuildDragPayload = builder;
            return this;
        }

        public void ShowContextMenu()
        {
            if (contextSections.Count == 0) return;

            var menu = new GenericMenu();
            for (int i = 0; i < contextSections.Count; i++)
            {
                var section = contextSections[i];
                if (!string.IsNullOrWhiteSpace(section.header))
                {
                    menu.AddDisabledItem(new GUIContent(section.header));
                }
                section.build?.Invoke(menu);
                if (i < contextSections.Count - 1)
                {
                    menu.AddSeparator("");
                }
            }
            menu.ShowAsContext();
        }

        internal void BuildMenu(GenericMenu menu)
        {
            for (int i = 0; i < contextSections.Count; i++)
            {
                var section = contextSections[i];
                section.build?.Invoke(menu);
            }

            if (ContextMenuMode == ContextMenuMode.Default)
            {
                if (contextSections.Count > 0)
                {
                    menu.AddSeparator("");
                }

                if (owner.TryGetActionHandler(HierarchyAction.Rename, out var rename))
                {
                    menu.AddItem(new GUIContent("Rename"), false, () => rename?.Invoke(this));
                }

                if (owner.TryGetActionHandler(HierarchyAction.Delete, out var delete))
                {
                    menu.AddItem(new GUIContent("Delete"), false, () => delete?.Invoke(this));
                }
            }
        }

        private readonly struct HierarchyContextSection
        {
            public readonly string header;
            public readonly Action<GenericMenu> build;

            public HierarchyContextSection(string header, Action<GenericMenu> build)
            {
                this.header = header;
                this.build = build;
            }
        }
    }

    public readonly struct HierarchyRow
    {
        public readonly Rect rect;
        public readonly HierarchyItem item;

        public HierarchyRow(Rect rect, HierarchyItem item)
        {
            this.rect = rect;
            this.item = item;
        }
    }

    public readonly struct HierarchyDragPayload
    {
        public readonly string key;
        public readonly object data;
        public readonly UnityEngine.Object[] objectReferences;
        public readonly string title;

        public HierarchyDragPayload(string key, object data, UnityEngine.Object[] objectReferences, string title)
        {
            this.key = key;
            this.data = data;
            this.objectReferences = objectReferences;
            this.title = title;
        }
    }
#endif
}
