using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ThiccTapeman.EditorUI;
using UnityEngine.InputSystem;
using ThiccTapeman.Input;

public class SoundLibraryMusicWindow : EditorWindow
{
    // ----------------------------------------- //
    //                   Fields                  //
    // ----------------------------------------- //
    #region Fields
    private SoundLibrary library;
    private string searchText = string.Empty;
    private Vector2 scroll;

    private Hierarchy hierarchy = Hierarchy.New();
    private readonly Dictionary<SoundLibraryEntry, HierarchyItem> entryItems = new();

    private MethodInfo audioUtilPlay;
    private MethodInfo audioUtilStop;
    private MethodInfo audioUtilStopAll;
    private AudioClip currentPreviewClip;
    private SoundLibraryEntry pendingDragEntry;
    private SoundLibraryFolder pendingDragParent;
    private SoundLibraryEntry renamingEntry;
    private SoundLibraryFolder renamingParent;
    private SfxVariantGroup renamingParentVariantGroup;
    private NarrationGroup renamingParentNarrationGroup;
    private NarrationVariantGroup renamingParentNarrationVariantGroup;
    private string renameBuffer = string.Empty;
    private bool renameSelectAll;
    private List<AudioClip> pendingDropClips;
    private SoundLibraryFolder pendingDropFolder;
    private bool hierarchyDirty = true;
    private string lastSearchText = string.Empty;
    private SoundLibrary lastLibrary;
    private readonly List<DrawItem> cachedItems = new();
    #endregion

    // ----------------------------------------- //
    //              Window Lifecycle             //
    // ----------------------------------------- //

    #region Window Lifecycle

    [MenuItem("Tools/Audio/Music Library")]
    public static void Open()
    {
        GetWindow<SoundLibraryMusicWindow>("Sound Library");
    }

    private void OnEnable()
    {
        CacheAudioUtil();
        hierarchyDirty = true;
    }

    private void OnDisable()
    {
        StopAllPreview();
    }

    #endregion

    // ----------------------------------------- //
    //             Audio Preview                 //
    // ----------------------------------------- //

    #region Audio Preview

    private void CacheAudioUtil()
    {
        // UnityEditor.AudioUtil is internal, so use reflection
        var audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (audioUtilType == null) return;

        audioUtilPlay = audioUtilType.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null
        );

        audioUtilStopAll = audioUtilType.GetMethod(
            "StopAllPreviewClips",
            BindingFlags.Static | BindingFlags.Public
        );

        audioUtilStop = audioUtilType.GetMethod(
            "StopPreviewClip",
            BindingFlags.Static | BindingFlags.Public
        );
    }

    private void StopAllPreview()
    {
        try { audioUtilStopAll?.Invoke(null, null); }
        catch { /* ignore */ }
        currentPreviewClip = null;
    }

    private void PlayPreview(AudioClip clip, bool loop)
    {
        if (clip == null) return;
        if (currentPreviewClip == clip)
        {
            StopPreview(clip);
            return;
        }

        StopAllPreview();
        try { audioUtilPlay?.Invoke(null, new object[] { clip, 0, loop }); }
        catch { /* ignore */ }
        currentPreviewClip = clip;
    }

    private void StopPreview(AudioClip clip)
    {
        if (clip == null) return;
        if (audioUtilStop == null)
        {
            StopAllPreview();
            return;
        }

        try { audioUtilStop?.Invoke(null, new object[] { clip }); }
        catch { /* ignore */ }
        if (currentPreviewClip == clip) currentPreviewClip = null;
    }

    #endregion

    // ----------------------------------------- //
    //              UI Rendering                 //
    // ----------------------------------------- //

    #region UI Rendering

    private void OnGUI()
    {
        DrawHeader();

        EditorGUILayout.Space(6);

        if (library == null)
        {
            EditorGUILayout.HelpBox("Assign a SoundLibrary asset to browse entries.", MessageType.Info);
            return;
        }

        DrawList();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Library", GUILayout.Width(100));
                library = (SoundLibrary)EditorGUILayout.ObjectField(library, typeof(SoundLibrary), false);
                if (library != lastLibrary)
                {
                    lastLibrary = library;
                    hierarchyDirty = true;
                }

                if (GUILayout.Button("Rebuild Lookup", GUILayout.Width(120)))
                {
                    library.BuildLookup();
                    EditorUtility.SetDirty(library);
                    hierarchyDirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTypeFilterDropdown();

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Search", GUILayout.Width(50));
                searchText = EditorGUILayout.TextField(searchText);

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    searchText = string.Empty;
                }

                if (GUILayout.Button("Stop", GUILayout.Width(60)))
                    StopAllPreview();
            }
        }
    }

    private void DrawList()
    {
        if (library != lastLibrary)
        {
            lastLibrary = library;
            hierarchyDirty = true;
        }
        if (!string.Equals(searchText, lastSearchText, StringComparison.Ordinal))
        {
            lastSearchText = searchText;
        }

        if (hierarchyDirty)
        {
            cachedItems.Clear();
            if (library.entries != null)
            {
                for (int i = 0; i < library.entries.Count; i++)
                    Collect(cachedItems, library.entries[i], depth: 0, parentPath: "", parentFolder: null);
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (cachedItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No entries found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (hierarchyDirty)
        {
            hierarchy = Hierarchy.New(hierarchy);
            ConfigureHierarchy();
            entryItems.Clear();

            for (int i = 0; i < cachedItems.Count; i++)
            {
                var it = cachedItems[i];

                var item = it.entry is SoundLibraryFolder || it.entry is SfxVariantGroup || it.entry is NarrationGroup || it.entry is NarrationVariantGroup
                    ? hierarchy.Folder(it.entry.name)
                    : hierarchy.Item(it.entry.name, null);

                item.WithData(it)
                    .IdOf(it.path)
                    .DepthOf(it.depth)
                    .Children(GetChildCount(it.entry))
                    .Draw(() => DrawRow(it))
                    .SetContextMenu(ContextMenuMode.Default, BuildContextMenuItems(it), BuildContextMenuActions(it))
                    .SetDragPayload(() => BuildDragPayload(it));

                hierarchy.AddItem(item);
                entryItems[it.entry] = item;
            }

            hierarchyDirty = false;
        }

        UpdateHierarchySearchFilter();

        int shown = 0;
        var rows = new List<RowInfo>(hierarchy.Items.Count);
        var hierarchyRows = new List<HierarchyRow>(hierarchy.Items.Count);

        for (int i = 0; i < hierarchy.Items.Count; i++)
        {
            var hItem = hierarchy.Items[i];
            if (hItem.Data is not DrawItem it) continue;
            if (!hierarchy.ShouldDisplay(hItem)) continue;
            if (!IsVisibleThroughFoldouts(it)) continue;

            Rect rowRect = hItem.DrawContent != null ? hItem.DrawContent.Invoke() : GUILayoutUtility.GetLastRect();
            rowRect.x = 0f;
            rowRect.width = EditorGUIUtility.currentViewWidth;
            rows.Add(new RowInfo { item = it, rect = rowRect });
            hierarchyRows.Add(new HierarchyRow(rowRect, hItem));
            DrawSelectionHighlight(it, rowRect);
            shown++;
            EditorGUILayout.Space(1f);
        }

        if (shown == 0)
        {
            EditorGUILayout.HelpBox("No matches for current filters.", MessageType.Info);
        }

        HandleContextMenu(rows);
        hierarchy.HandleDragAndDrop(hierarchyRows);
        hierarchy.HandleSelection(hierarchyRows);
        hierarchy.DrawDropIndicator();
        HandleKeyboardShortcuts(cachedItems);

        EditorGUILayout.EndScrollView();
    }

    private bool IsVisibleThroughFoldouts(DrawItem item)
    {
        return hierarchy.IsVisible(item.ancestorFolderIds);
    }

    private Rect DrawRow(DrawItem it)
    {
        var entry = it.entry;

        if (entry is SoundLibraryFolder folder)
        {
            return DrawFolderRow(it, folder);
        }
        if (entry is SfxVariantGroup group)
        {
            return DrawVariantGroupRow(it, group);
        }
        if (entry is NarrationGroup narrationGroup)
        {
            return DrawNarrationGroupRow(it, narrationGroup);
        }
        if (entry is NarrationVariantGroup narrationVariantGroup)
        {
            return DrawNarrationVariantGroupRow(it, narrationVariantGroup);
        }

        Rect iconRect = Rect.zero;
        Rect nameRect = Rect.zero;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(it.depth * 14f);

            if (entry is SoundDefinition def)
            {
                GUIContent typeIcon = entry is MusicDefinition
                    ? GetIcon("d_AudioMixerView Icon", "AudioMixerView Icon", "d_AudioSource Icon", "AudioSource Icon")
                    : GetIcon("d_AudioSource Icon", "AudioSource Icon");
                GUILayout.Label(typeIcon, GUILayout.Width(16), GUILayout.Height(16));
                iconRect = GUILayoutUtility.GetLastRect();

                EditorGUI.BeginDisabledGroup(def.clip == null);
                bool isPlaying = currentPreviewClip == def.clip;
                GUIContent playIcon = EditorGUIUtility.IconContent(isPlaying ? "d_PauseButton" : "d_PlayButton");
                if (GUILayout.Button(playIcon, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    if (isPlaying) StopPreview(def.clip);
                    else PlayPreview(def.clip, def.loop);
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (entry is SfxVariant variant)
            {
                GUIContent typeIcon = GetIcon("d_AudioSource Icon", "AudioSource Icon");
                GUILayout.Label(typeIcon, GUILayout.Width(16), GUILayout.Height(16));
                iconRect = GUILayoutUtility.GetLastRect();

                EditorGUI.BeginDisabledGroup(variant.clip == null);
                bool isPlaying = currentPreviewClip == variant.clip;
                GUIContent playIcon = EditorGUIUtility.IconContent(isPlaying ? "d_PauseButton" : "d_PlayButton");
                if (GUILayout.Button(playIcon, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    if (isPlaying) StopPreview(variant.clip);
                    else PlayPreview(variant.clip, false);
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (entry is NarrationPlayable narrationPlayable)
            {
                GUIContent typeIcon = GetIcon("d_AudioSource Icon", "AudioSource Icon");
                GUILayout.Label(typeIcon, GUILayout.Width(16), GUILayout.Height(16));
                iconRect = GUILayoutUtility.GetLastRect();

                EditorGUI.BeginDisabledGroup(narrationPlayable.clip == null);
                bool isPlaying = currentPreviewClip == narrationPlayable.clip;
                GUIContent playIcon = EditorGUIUtility.IconContent(isPlaying ? "d_PauseButton" : "d_PlayButton");
                if (GUILayout.Button(playIcon, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    if (isPlaying) StopPreview(narrationPlayable.clip);
                    else PlayPreview(narrationPlayable.clip, false);
                }
                EditorGUI.EndDisabledGroup();
            }

            nameRect = DrawNameField(entry);

            if (entry is SoundDefinition def2)
            {
                DrawVolume(def2);
                EditorGUILayout.ObjectField(def2.clip, typeof(AudioClip), false, GUILayout.Width(100));
            }
            else if (entry is SfxVariant variant2)
            {
                DrawVolume(variant2);
                DrawProbability(variant2);
                EditorGUILayout.ObjectField(variant2.clip, typeof(AudioClip), false, GUILayout.Width(100));
            }
            else if (entry is NarrationPlayable narrationPlayable2)
            {
                DrawVolume(narrationPlayable2);
                DrawWaitBeforeStart(narrationPlayable2);
                EditorGUILayout.ObjectField(narrationPlayable2.clip, typeof(AudioClip), false, GUILayout.Width(100));
                DrawNarrationReorderButtons(it.parentNarrationGroup, it.entry);
            }
        }

        return GUILayoutUtility.GetLastRect();
    }

    private Rect DrawFolderRow(DrawItem it, SoundLibraryFolder folder)
    {
        Rect foldoutRect = Rect.zero;
        Rect nameRect = Rect.zero;

        string folderKey = it.path;
        int count = folder.children != null ? folder.children.Count : 0;
        hierarchy.DrawFolderRow(folderKey, it.depth, count, () => DrawNameField(folder), out foldoutRect, out nameRect);

        Rect[] clickTargets = new[] { foldoutRect, nameRect };
        return GUILayoutUtility.GetLastRect();
    }

    private Rect DrawVariantGroupRow(DrawItem it, SfxVariantGroup group)
    {
        Rect foldoutRect = Rect.zero;
        Rect nameRect = Rect.zero;

        string folderKey = it.path;
        int count = group.variants != null ? group.variants.Count : 0;
        hierarchy.DrawFolderRow(folderKey, it.depth, count, () =>
        {
            Rect rect = DrawNameField(group);
            DrawGroupVolume(group);
            return rect;
        }, out foldoutRect, out nameRect);

        Rect[] clickTargets = new[] { foldoutRect, nameRect };
        return GUILayoutUtility.GetLastRect();
    }

    private Rect DrawNarrationGroupRow(DrawItem it, NarrationGroup group)
    {
        Rect foldoutRect = Rect.zero;
        Rect nameRect = Rect.zero;

        string folderKey = it.path;
        int count = group.entries != null ? group.entries.Count : 0;
        hierarchy.DrawFolderRow(folderKey, it.depth, count, () =>
        {
            Rect rect = DrawNameField(group);
            DrawNarrationGroupVolume(group);
            return rect;
        }, out foldoutRect, out nameRect);

        Rect[] clickTargets = new[] { foldoutRect, nameRect };
        return GUILayoutUtility.GetLastRect();
    }

    private Rect DrawNarrationVariantGroupRow(DrawItem it, NarrationVariantGroup group)
    {
        Rect foldoutRect = Rect.zero;
        Rect nameRect = Rect.zero;

        string folderKey = it.path;
        int count = group.variants != null ? group.variants.Count : 0;
        hierarchy.DrawFolderRow(folderKey, it.depth, count, () =>
        {
            Rect rect = DrawNameField(group);
            DrawNarrationReorderButtons(it.parentNarrationGroup, it.entry);
            return rect;
        }, out foldoutRect, out nameRect);

        Rect[] clickTargets = new[] { foldoutRect, nameRect };
        return GUILayoutUtility.GetLastRect();
    }

    private static string GetNameLabel(SoundLibraryEntry entry)
    {
        string name = string.IsNullOrEmpty(entry.name) ? "(no name)" : entry.name;
        if (entry is SoundLibraryFolder) return name;
        if (entry is MusicDefinition) return $"{name}.music";
        if (entry is SfxDefinition) return $"{name}.sfx";
        if (entry is SfxVariant) return $"{name}.var";
        if (entry is SfxVariantGroup) return $"{name}.sfxvariants";
        if (entry is NarrationGroup) return $"{name}.narration";
        if (entry is NarrationClip) return $"{name}.clip";
        if (entry is NarrationVariant) return $"{name}.variant";
        if (entry is NarrationVariantGroup) return $"{name}.narrationvariants";
        return name;
    }

    private static int GetChildCount(SoundLibraryEntry entry)
    {
        if (entry is SoundLibraryFolder folder && folder.children != null) return folder.children.Count;
        if (entry is SfxVariantGroup variantGroup && variantGroup.variants != null) return variantGroup.variants.Count;
        if (entry is NarrationGroup narrationGroup && narrationGroup.entries != null) return narrationGroup.entries.Count;
        if (entry is NarrationVariantGroup narrationVariantGroup && narrationVariantGroup.variants != null) return narrationVariantGroup.variants.Count;
        return 0;
    }

    private List<string> BuildContextMenuItems(DrawItem it)
    {
        var items = new List<string>();

        items.Add("Add/Folder");
        items.Add("Add/SFX");
        items.Add("Add/Music");
        items.Add("Add/Narration");

        if (it.entry is SfxDefinition)
        {
            items.Add("Add/Variant");
        }
        else if (it.entry is SfxVariantGroup)
        {
            items.Add("Add/Variant");
        }
        else if (it.entry is NarrationGroup)
        {
            items.Add("Add/Clip");
        }
        else if (it.entry is NarrationClip)
        {
            items.Add("Create/Variant");
        }
        else if (it.entry is NarrationVariantGroup)
        {
            items.Add("Add/Variant");
        }

        if (it.entry is SoundDefinition)
        {
            items.Add("Change Type/Music");
            items.Add("Change Type/SFX");
        }

        return items;
    }

    private List<Action> BuildContextMenuActions(DrawItem it)
    {
        var actions = new List<Action>();
        var targetFolder = it.entry as SoundLibraryFolder ?? it.parentFolder;

        actions.Add(() => AddNewEntry(targetFolder, EntryKind.Folder));
        actions.Add(() => AddNewEntry(targetFolder, EntryKind.Sfx));
        actions.Add(() => AddNewEntry(targetFolder, EntryKind.Music));
        actions.Add(() => AddNewEntry(targetFolder, EntryKind.Narration));

        if (it.entry is SfxDefinition sfx)
        {
            actions.Add(() => AddVariantFromSfx(sfx, it.parentFolder));
        }
        else if (it.entry is SfxVariantGroup group)
        {
            actions.Add(() => AddVariantToGroup(group));
        }
        else if (it.entry is NarrationGroup narrationGroup)
        {
            actions.Add(() => AddNarrationClipToGroup(narrationGroup));
        }
        else if (it.entry is NarrationClip narrationClip)
        {
            actions.Add(() => ConvertNarrationClipToVariantGroup(narrationClip, it.parentNarrationGroup));
        }
        else if (it.entry is NarrationVariantGroup narrationVariantGroup)
        {
            actions.Add(() => AddNarrationVariantToGroup(narrationVariantGroup));
        }

        if (it.entry is SoundDefinition)
        {
            actions.Add(() => ChangeEntryType(it.entry, it.parentFolder, EntryKind.Music));
            actions.Add(() => ChangeEntryType(it.entry, it.parentFolder, EntryKind.Sfx));
        }

        return actions;
    }

    private HierarchyDragPayload? BuildDragPayload(DrawItem it)
    {
        if (it.entry is SfxVariant || it.entry is NarrationClip || it.entry is NarrationVariantGroup || it.entry is NarrationVariant)
        {
            return null;
        }

        pendingDragEntry = it.entry;
        pendingDragParent = it.parentFolder;

        var timelineObject = CreateTimelineDragObject(it.entry);
        var refs = timelineObject != null ? new[] { timelineObject } : Array.Empty<UnityEngine.Object>();
        return new HierarchyDragPayload("SoundLibraryEntry", it, refs, it.entry.name);
    }

    private RowInfo? ToRowInfo(HierarchyRow? row)
    {
        if (!row.HasValue) return null;
        if (row.Value.item?.Data is not DrawItem it) return null;
        return new RowInfo { item = it, rect = row.Value.rect };
    }

    private Rect DrawNameField(SoundLibraryEntry entry)
    {
        if (renamingEntry == entry)
        {
            GUI.SetNextControlName("RenameField");
            string updated = EditorGUILayout.TextField(renameBuffer, GUILayout.MinWidth(100));
            Rect rect = GUILayoutUtility.GetLastRect();

            if (renameSelectAll)
            {
                renameSelectAll = false;
                EditorGUI.FocusTextInControl("RenameField");
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor?.SelectAll();
            }

            renameBuffer = updated;

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyUp)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename(entry);
                    GUI.FocusControl(null);
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    GUI.FocusControl(null);
                    evt.Use();
                }
            }
            return rect;
        }

        string nameLabel = entry is SoundLibraryFolder
            ? (string.IsNullOrEmpty(entry.name) ? "(no name)" : entry.name)
            : GetNameLabel(entry);
        EditorGUILayout.LabelField(nameLabel, GUILayout.MinWidth(100));
        return GUILayoutUtility.GetLastRect();
    }

    #endregion

    // ----------------------------------------- //
    //            Editing Controls               //
    // ----------------------------------------- //

    #region Editing Controls

    private void HandleRenameKeys(SoundLibraryEntry entry)
    {
        Event evt = Event.current;
        if (evt == null) return;
        if (evt.type != EventType.KeyDown) return;

        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            CommitRename(entry);
            GUI.FocusControl(null);
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            CancelRename();
            GUI.FocusControl(null);
            evt.Use();
        }
    }

    private void DrawVolume(SoundDefinition def)
    {
        EditorGUI.BeginChangeCheck();
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float volume = GUI.HorizontalSlider(rect, def.volume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Sound Volume");
            def.volume = volume;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawVolume(SfxVariant variant)
    {
        EditorGUI.BeginChangeCheck();
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float volume = GUI.HorizontalSlider(rect, variant.volume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Variant Volume");
            variant.volume = volume;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawVolume(NarrationPlayable clip)
    {
        EditorGUI.BeginChangeCheck();
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float volume = GUI.HorizontalSlider(rect, clip.volume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Narration Clip Volume");
            clip.volume = volume;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawGroupVolume(SfxVariantGroup group)
    {
        EditorGUI.BeginChangeCheck();
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float volume = GUI.HorizontalSlider(rect, group.volume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Variant Group Volume");
            group.volume = volume;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawNarrationGroupVolume(NarrationGroup group)
    {
        EditorGUI.BeginChangeCheck();
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float volume = GUI.HorizontalSlider(rect, group.volume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Narration Group Volume");
            group.volume = volume;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawProbability(SfxVariant variant)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Prob", GUILayout.Width(34));
        Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight);
        float probability = GUI.HorizontalSlider(rect, variant.probability, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Variant Probability");
            variant.probability = probability;
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawWaitBeforeStart(NarrationPlayable clip)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Wait", GUILayout.Width(32));
        float wait = EditorGUILayout.FloatField(clip.waitBeforeStart, GUILayout.Width(50));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(library, "Adjust Narration Wait");
            clip.waitBeforeStart = Mathf.Max(0f, wait);
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawNarrationReorderButtons(NarrationGroup group, SoundLibraryEntry entry)
    {
        if (group == null || entry == null || group.entries == null) return;
        int index = group.entries.IndexOf(entry);
        if (index < 0) return;

        GUIContent upIcon = GetIconOrText("^", "d_ArrowNavigationUp", "ArrowNavigationUp", "d_UpArrow", "UpArrow");
        GUIContent downIcon = GetIconOrText("v", "d_ArrowNavigationDown", "ArrowNavigationDown", "d_DownArrow", "DownArrow");

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUILayout.Button(upIcon, GUILayout.Width(18), GUILayout.Height(18)))
            {
                MoveNarrationEntry(group, index, index - 1);
            }
        }

        using (new EditorGUI.DisabledScope(index >= group.entries.Count - 1))
        {
            if (GUILayout.Button(downIcon, GUILayout.Width(18), GUILayout.Height(18)))
            {
                MoveNarrationEntry(group, index, index + 1);
            }
        }
    }

    #endregion

    // ----------------------------------------- //
    //         Interaction and Input             //
    // ----------------------------------------- //

    #region Interaction and Input

    private static GUIContent GetIcon(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var content = EditorGUIUtility.IconContent(names[i]);
            if (content != null && content.image != null)
            {
                return content;
            }
        }

        return GUIContent.none;
    }

    private static GUIContent GetIconOrText(string fallback, params string[] names)
    {
        var icon = GetIcon(names);
        if (icon != null && icon.image != null)
        {
            return icon;
        }

        return new GUIContent(fallback);
    }


    private void HandleKeyboardShortcuts(IReadOnlyList<DrawItem> items)
    {
        Event evt = Event.current;
        if (evt == null || evt.type != EventType.KeyDown) return;
        if (GUI.GetNameOfFocusedControl() == "RenameField") return;
        if (SoundLibraryEditorSelection.ActiveLibrary != library) return;

        var entry = SoundLibraryEditorSelection.ActiveEntry;
        if (entry == null) return;

        if (!TryGetDrawItem(items, entry, out var item)) return;

        if (entryItems.TryGetValue(entry, out var hierarchyItem))
        {
            hierarchy.HandleKeybinds(hierarchyItem);
        }
    }


    private void DrawSelectionHighlight(DrawItem item, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (SoundLibraryEditorSelection.ActiveEntry != item.entry) return;

        Color color = EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.45f, 0.85f, 0.25f)
            : new Color(0.24f, 0.45f, 0.85f, 0.35f);

        EditorGUI.DrawRect(rect, color);
    }

    private void DrawTypeFilterDropdown()
    {
        GUIContent icon = EditorGUIUtility.IconContent("d_FilterByType");
        if (GUILayout.Button(new GUIContent(icon.image, "Type Filter"), EditorStyles.toolbarButton, GUILayout.Width(28)))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Music"), false, () => SetSearchToken("t:music"));
            menu.AddItem(new GUIContent("SFX"), false, () => SetSearchToken("t:sfx"));
            menu.AddItem(new GUIContent("Narration"), false, () => SetSearchToken("t:narration"));
            menu.AddItem(new GUIContent("All"), false, () => SetSearchToken("t:all"));
            menu.ShowAsContext();
        }
    }

    private void UpdateHierarchySearchFilter()
    {
        string term = StripSearchToken(searchText).Trim();
        SearchFilter filter = GetSearchFilter(searchText);

        hierarchy.SetSearch(term, item =>
        {
            if (item?.Data is not DrawItem it) return item?.Name ?? string.Empty;
            string name = it.entry != null ? it.entry.name : string.Empty;
            return $"{name} {it.path}";
        });

        hierarchy.SetFilter(item =>
        {
            if (item?.Data is not DrawItem it) return true;
            return filter switch
            {
                SearchFilter.Music => it.entry is MusicDefinition || it.entry is SoundLibraryFolder,
                SearchFilter.Sfx => it.entry is SfxDefinition || it.entry is SfxVariantGroup || it.entry is SfxVariant || it.entry is SoundLibraryFolder,
                SearchFilter.Narration => it.entry is NarrationGroup || it.entry is NarrationClip || it.entry is NarrationVariantGroup || it.entry is NarrationVariant || it.entry is SoundLibraryFolder,
                _ => true
            };
        });
    }

    private void ConfigureHierarchy()
    {
        hierarchy.InternalDragKey = "SoundLibraryEntry";
        hierarchy.SetKeybind(HierarchyAction.Rename, CreateEditorInputItem("<Keyboard>/f2"));
        hierarchy.SetKeybind(HierarchyAction.Delete, CreateEditorInputItem("<Keyboard>/delete"));
        hierarchy.SetActionHandler(HierarchyAction.Rename, item =>
        {
            if (item?.Data is not DrawItem it) return;
            BeginRename(it.entry, it.parentFolder, it.parentVariantGroup, it.parentNarrationGroup, it.parentNarrationVariantGroup);
        });
        hierarchy.SetActionHandler(HierarchyAction.Delete, item =>
        {
            if (item?.Data is not DrawItem it) return;
            RemoveEntry(it.entry, it.parentFolder, it.parentVariantGroup, it.parentNarrationGroup, it.parentNarrationVariantGroup);
        });
        hierarchy.OnSelect = item =>
        {
            if (item?.Data is not DrawItem it) return;
            Selection.activeObject = library;
            SoundLibraryEditorSelection.Set(library, it.entry);
            Repaint();
        };
        hierarchy.OnDoubleClick = item =>
        {
            if (item?.Data is not DrawItem it) return;
            if (it.entry is SoundDefinition def && def.clip != null)
            {
                EditorGUIUtility.PingObject(def.clip);
            }
            else if (it.entry is SfxVariant variant && variant.clip != null)
            {
                EditorGUIUtility.PingObject(variant.clip);
            }
            else if (it.entry is NarrationPlayable narration && narration.clip != null)
            {
                EditorGUIUtility.PingObject(narration.clip);
            }
            else
            {
                EditorGUIUtility.PingObject(library);
            }
        };
        hierarchy.OnTripleClick = item =>
        {
            if (item?.Data is not DrawItem it) return;
            BeginRename(it.entry, it.parentFolder, it.parentVariantGroup, it.parentNarrationGroup, it.parentNarrationVariantGroup);
        };

        hierarchy.CanStartDrag = item =>
        {
            if (item?.Data is not DrawItem it) return false;
            return it.entry is not SfxVariant
                && it.entry is not NarrationClip
                && it.entry is not NarrationVariantGroup
                && it.entry is not NarrationVariant;
        };

        hierarchy.OnInternalDrop = (data, row) =>
        {
            if (data is not DrawItem source) return;
            pendingDragEntry = source.entry;
            pendingDragParent = source.parentFolder;
            var rowInfo = ToRowInfo(row);
            var targetFolder = GetTargetFolder(rowInfo);
            MoveEntry(source.entry, targetFolder, rowInfo);
        };

        hierarchy.CanAcceptExternal = objects => GetDraggedClips(objects).Count > 0;
        hierarchy.OnExternalDrop = (objects, row) =>
        {
            var clips = GetDraggedClips(objects);
            if (clips.Count == 0) return;
            pendingDropClips = clips;
            pendingDropFolder = GetTargetFolder(ToRowInfo(row));
            ShowDropTypeMenu();
        };

        hierarchy.ContextMenu("Add", menu =>
        {
            menu.AddItem(new GUIContent("Add/Folder"), false, () => AddNewEntry(null, EntryKind.Folder));
            menu.AddItem(new GUIContent("Add/SFX"), false, () => AddNewEntry(null, EntryKind.Sfx));
            menu.AddItem(new GUIContent("Add/Music"), false, () => AddNewEntry(null, EntryKind.Music));
            menu.AddItem(new GUIContent("Add/Narration"), false, () => AddNewEntry(null, EntryKind.Narration));
        });
    }

    private static InputItem CreateEditorInputItem(string bindingPath)
    {
        var action = new InputAction("EditorHierarchyAction", binding: bindingPath);
        return new InputItem(action);
    }

    private void HandleContextMenu(IReadOnlyList<RowInfo> rows)
    {
        Event evt = Event.current;
        if (evt == null || evt.type != EventType.ContextClick) return;

        var row = FindRowUnderMouse(rows, evt.mousePosition);
        if (row.HasValue && entryItems.TryGetValue(row.Value.item.entry, out var item))
        {
            hierarchy.ShowContextMenu(item);
        }
        else
        {
            hierarchy.ShowContextMenu();
        }
        evt.Use();
    }

    #endregion

    private RowInfo? FindRowUnderMouse(IReadOnlyList<RowInfo> rows, Vector2 mousePosition)
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

    private SoundLibraryFolder GetTargetFolder(RowInfo? row)
    {
        if (row.HasValue)
        {
            var entry = row.Value.item.entry;
            if (entry is SoundLibraryFolder folder)
            {
                return folder;
            }

            return row.Value.item.parentFolder;
        }

        return null;
    }

    private void MoveEntry(SoundLibraryEntry entry, SoundLibraryFolder targetFolder, RowInfo? row)
    {
        if (entry is SfxVariant || entry is NarrationClip || entry is NarrationVariantGroup || entry is NarrationVariant) return;
        var currentList = GetTargetList(pendingDragParent);
        if (currentList == null || entry == null) return;
        if (entry is SoundLibraryFolder folder && IsInvalidFolderMove(folder, targetFolder))
        {
            return;
        }

        if (!currentList.Remove(entry))
        {
            return;
        }

        var targetList = GetTargetList(targetFolder);
        if (targetList == null) return;

        Undo.RecordObject(library, "Move Sound Entry");

        if (row.HasValue && row.Value.item.entry is SoundLibraryFolder)
        {
            targetList.Add(entry);
        }
        else if (row.HasValue)
        {
            var rowParent = row.Value.item.parentFolder;
            if (rowParent == targetFolder)
            {
                int index = targetList.IndexOf(row.Value.item.entry);
                if (index < 0) targetList.Add(entry);
                else targetList.Insert(index, entry);
            }
            else
            {
                targetList.Add(entry);
            }
        }
        else
        {
            targetList.Add(entry);
        }

        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, entry);
        Repaint();
        hierarchyDirty = true;
    }

    // ----------------------------------------- //
    //          Entry Operations                 //
    // ----------------------------------------- //

    #region Entry Operations

    private void RemoveEntry(SoundLibraryEntry entry, SoundLibraryFolder parentFolder, SfxVariantGroup parentVariantGroup, NarrationGroup parentNarrationGroup, NarrationVariantGroup parentNarrationVariantGroup)
    {
        if (entry == null) return;
        if (entry is SfxVariant variant)
        {
            var list = GetVariantList(parentVariantGroup);
            if (list == null) return;
            Undo.RecordObject(library, "Remove Sound Variant");
            list.Remove(variant);
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationClip clip)
        {
            var list = GetNarrationEntryList(parentNarrationGroup);
            if (list == null) return;
            Undo.RecordObject(library, "Remove Narration Clip");
            list.Remove(clip);
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationVariant narrationVariant)
        {
            var list = GetNarrationVariantList(parentNarrationVariantGroup);
            if (list == null) return;
            Undo.RecordObject(library, "Remove Narration Variant");
            list.Remove(narrationVariant);
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationVariantGroup narrationVariantGroup)
        {
            var list = GetNarrationEntryList(parentNarrationGroup);
            if (list == null) return;
            Undo.RecordObject(library, "Remove Narration Variant Group");
            list.Remove(narrationVariantGroup);
            EditorUtility.SetDirty(library);
        }
        else
        {
            var list = GetTargetList(parentFolder);
            if (list == null) return;
            Undo.RecordObject(library, "Remove Sound Entry");
            list.Remove(entry);
            EditorUtility.SetDirty(library);
        }

        if (SoundLibraryEditorSelection.ActiveEntry == entry)
        {
            SoundLibraryEditorSelection.Clear();
        }

        Selection.activeObject = library;
        Repaint();
        hierarchyDirty = true;
    }

    private void MoveNarrationEntry(NarrationGroup group, int fromIndex, int toIndex)
    {
        var list = GetNarrationEntryList(group);
        if (list == null) return;
        if (fromIndex < 0 || fromIndex >= list.Count) return;
        if (toIndex < 0 || toIndex >= list.Count) return;
        if (fromIndex == toIndex) return;

        Undo.RecordObject(library, "Reorder Narration Entry");
        var clip = list[fromIndex];
        list.RemoveAt(fromIndex);
        if (toIndex > fromIndex) toIndex--;
        list.Insert(toIndex, clip);
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, clip);
        Repaint();
        hierarchyDirty = true;
    }

    private void ChangeEntryType(SoundLibraryEntry entry, SoundLibraryFolder parentFolder, EntryKind kind)
    {
        if (entry is SfxVariant || entry is SfxVariantGroup) return;
        if (entry is not SoundDefinition def) return;
        var list = GetTargetList(parentFolder);
        if (list == null) return;

        SoundDefinition replacement = kind == EntryKind.Music ? new MusicDefinition() : new SfxDefinition();
        replacement.name = def.name;
        replacement.clip = def.clip;
        replacement.volume = def.volume;
        replacement.loop = def.loop;

        if (replacement is SfxDefinition sfx)
        {
            sfx.pitchRange = def is SfxDefinition src ? src.pitchRange : new Vector2(1f, 1f);
            sfx.spatialBlend = def is SfxDefinition src2 ? src2.spatialBlend : 1f;
            sfx.minDistance = def is SfxDefinition src3 ? src3.minDistance : 1f;
            sfx.maxDistance = def is SfxDefinition src4 ? src4.maxDistance : 25f;
            sfx.mixerGroup = def is SfxDefinition src5 ? src5.mixerGroup : null;
        }

        if (replacement is MusicDefinition music)
        {
            music.pitchRange = def is MusicDefinition src ? src.pitchRange : new Vector2(1f, 1f);
            music.mixerGroup = def is MusicDefinition src2 ? src2.mixerGroup : null;
        }

        replacement.SetId(def.Id);
        Undo.RecordObject(library, "Change Sound Type");
        int index = list.IndexOf(entry);
        if (index >= 0)
        {
            list[index] = replacement;
        }
        else
        {
            list.Add(replacement);
        }

        if (SoundLibraryEditorSelection.ActiveEntry == entry)
        {
            SoundLibraryEditorSelection.Set(library, replacement);
        }

        EditorUtility.SetDirty(library);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddNewEntry(SoundLibraryFolder folder, EntryKind kind)
    {
        var list = GetTargetList(folder);
        if (list == null) return;

        SoundLibraryEntry entry = kind switch
        {
            EntryKind.Folder => new SoundLibraryFolder(),
            EntryKind.Music => new MusicDefinition(),
            EntryKind.Narration => new NarrationGroup(),
            _ => new SfxDefinition()
        };

        string baseName = kind switch
        {
            EntryKind.Folder => "New Folder",
            EntryKind.Music => "New Music",
            EntryKind.Narration => "New Narration",
            _ => "New Sfx"
        };

        entry.name = GetUniqueName(list, baseName);
        entry.EnsureId();

        Undo.RecordObject(library, "Add Sound Entry");
        list.Add(entry);
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, entry);
        BeginRename(entry, folder);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddVariantFromSfx(SfxDefinition sfx, SoundLibraryFolder parentFolder)
    {
        if (sfx == null) return;
        var list = GetTargetList(parentFolder);
        if (list == null) return;

        int index = list.IndexOf(sfx);
        if (index < 0) return;

        var group = new SfxVariantGroup
        {
            name = sfx.name,
            volume = sfx.volume,
            pitchRange = sfx.pitchRange,
            spatialBlend = sfx.spatialBlend,
            minDistance = sfx.minDistance,
            maxDistance = sfx.maxDistance,
            mixerGroup = sfx.mixerGroup,
            loop = sfx.loop
        };
        group.EnsureId();

        if (sfx.clip != null)
        {
            var baseVariant = new SfxVariant
            {
                name = GetUniqueVariantName(group.variants, "Original"),
                clip = sfx.clip,
                volume = 1f,
                pitchRange = sfx.pitchRange,
                probability = 1f
            };
            baseVariant.EnsureId();
            group.variants.Add(baseVariant);
        }

        var variant = new SfxVariant();
        variant.name = GetUniqueVariantName(group.variants, "Variant");
        variant.EnsureId();
        group.variants.Add(variant);

        Undo.RecordObject(library, "Convert SFX To Variants");
        list[index] = group;
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, variant);
        BeginRename(variant, null, group);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddVariantToGroup(SfxVariantGroup group)
    {
        if (group == null) return;
        var list = GetVariantList(group);
        if (list == null) return;

        var variant = new SfxVariant();
        variant.name = GetUniqueVariantName(list, "Variant");
        variant.EnsureId();

        Undo.RecordObject(library, "Add Sound Variant");
        list.Add(variant);
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, variant);
        BeginRename(variant, null, group);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddNarrationClipToGroup(NarrationGroup group)
    {
        if (group == null) return;
        var list = GetNarrationEntryList(group);
        if (list == null) return;

        var clip = new NarrationClip();
        clip.name = GetUniqueNarrationClipName(list, "Clip");
        clip.EnsureId();

        Undo.RecordObject(library, "Add Narration Clip");
        list.Add(clip);
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, clip);
        BeginRename(clip, null, null, group);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddNarrationVariantToGroup(NarrationVariantGroup group)
    {
        if (group == null) return;
        var list = GetNarrationVariantList(group);
        if (list == null) return;

        var variant = new NarrationVariant();
        variant.name = GetUniqueNarrationVariantName(list, "Variant");
        variant.EnsureId();

        Undo.RecordObject(library, "Add Narration Variant");
        list.Add(variant);
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, variant);
        BeginRename(variant, null, null, null, group);
        Repaint();
        hierarchyDirty = true;
    }

    private void ConvertNarrationClipToVariantGroup(NarrationClip clip, NarrationGroup parentGroup)
    {
        if (clip == null || parentGroup == null) return;
        var list = GetNarrationEntryList(parentGroup);
        if (list == null) return;

        int index = list.IndexOf(clip);
        if (index < 0) return;

        var group = new NarrationVariantGroup
        {
            name = clip.name
        };
        group.EnsureId();

        if (clip.clip != null)
        {
            var original = new NarrationVariant
            {
                name = GetUniqueNarrationVariantName(group.variants, "Original"),
                clip = clip.clip,
                volume = clip.volume,
                waitBeforeStart = clip.waitBeforeStart
            };
            original.EnsureId();
            group.variants.Add(original);
        }

        var variant = new NarrationVariant();
        variant.name = GetUniqueNarrationVariantName(group.variants, "Variant");
        variant.EnsureId();
        group.variants.Add(variant);

        Undo.RecordObject(library, "Convert Narration Clip To Variants");
        list[index] = group;
        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        SoundLibraryEditorSelection.Set(library, variant);
        BeginRename(variant, null, null, null, group);
        Repaint();
        hierarchyDirty = true;
    }

    private void AddClipEntry(SoundLibraryFolder folder, AudioClip clip, SearchFilter filter)
    {
        var list = GetTargetList(folder);
        if (list == null || clip == null) return;

        if (filter == SearchFilter.Narration)
        {
            var group = new NarrationGroup();
            group.name = GetUniqueName(list, clip.name);
            group.EnsureId();
            var narrationClip = new NarrationClip
            {
                name = GetUniqueNarrationClipName(group.entries, clip.name),
                clip = clip
            };
            narrationClip.EnsureId();
            group.entries.Add(narrationClip);
            list.Add(group);
            hierarchyDirty = true;
            return;
        }

        SoundDefinition entry = filter == SearchFilter.Music ? new MusicDefinition() : new SfxDefinition();
        entry.name = GetUniqueName(list, clip.name);
        entry.clip = clip;
        entry.EnsureId();
        list.Add(entry);
        hierarchyDirty = true;
    }

    #endregion

    // ----------------------------------------- //
    //     Drag And Drop Helpers                 //
    // ----------------------------------------- //

    #region Drag And Drop Helpers

    private static List<AudioClip> GetDraggedClips(UnityEngine.Object[] objects)
    {
        var clips = new List<AudioClip>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is AudioClip clip)
            {
                clips.Add(clip);
            }
        }

        return clips;
    }

    private static bool TryGetDrawItem(IReadOnlyList<DrawItem> items, SoundLibraryEntry entry, out DrawItem found)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i].entry, entry))
            {
                found = items[i];
                return true;
            }
        }

        found = default;
        return false;
    }

    private static UnityEngine.Object CreateTimelineDragObject(SoundLibraryEntry entry)
    {
        if (entry == null) return null;

        if (entry is SfxDefinition || entry is SfxVariantGroup || entry is SfxVariant)
        {
            var clip = ScriptableObject.CreateInstance<SoundSfxClip>();
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.sfx.entry = entry;
            clip.sfx.entryName = entry.name;
            return clip;
        }

        if (entry is MusicDefinition)
        {
            var clip = ScriptableObject.CreateInstance<SoundMusicClip>();
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.music.entry = entry;
            clip.music.entryName = entry.name;
            return clip;
        }

        if (entry is NarrationGroup || entry is NarrationPlayable || entry is NarrationVariantGroup)
        {
            var clip = ScriptableObject.CreateInstance<SoundNarrationClip>();
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.narration.entry = entry;
            clip.narration.entryName = entry.name;
            return clip;
        }

        return null;
    }

    #endregion
    // ----------------------------------------- //
    //              Data Helpers                 //
    // ----------------------------------------- //
    #region Data Helpers

    private List<SoundLibraryEntry> GetTargetList(SoundLibraryFolder folder)
    {
        if (folder == null)
        {
            if (library.entries == null)
            {
                library.entries = new List<SoundLibraryEntry>();
            }

            return library.entries;
        }

        if (folder.children == null)
        {
            folder.children = new List<SoundLibraryEntry>();
        }

        return folder.children;
    }

    private List<SfxVariant> GetVariantList(SfxVariantGroup group)
    {
        if (group == null) return null;
        if (group.variants == null)
        {
            group.variants = new List<SfxVariant>();
        }

        return group.variants;
    }

    private List<SoundLibraryEntry> GetNarrationEntryList(NarrationGroup group)
    {
        if (group == null) return null;
        if (group.entries == null)
        {
            group.entries = new List<SoundLibraryEntry>();
        }

        return group.entries;
    }

    private List<NarrationVariant> GetNarrationVariantList(NarrationVariantGroup group)
    {
        if (group == null) return null;
        if (group.variants == null)
        {
            group.variants = new List<NarrationVariant>();
        }

        return group.variants;
    }

    private static string GetUniqueName(List<SoundLibraryEntry> list, string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (ContainsName(list, candidate))
        {
            candidate = $"{baseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    private static bool ContainsName(List<SoundLibraryEntry> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInvalidFolderMove(SoundLibraryFolder movingFolder, SoundLibraryFolder targetFolder)
    {
        if (movingFolder == null) return false;
        if (targetFolder == null) return false;
        if (ReferenceEquals(movingFolder, targetFolder)) return true;
        return IsDescendantFolder(movingFolder, targetFolder);
    }

    private bool IsDescendantFolder(SoundLibraryFolder root, SoundLibraryFolder candidate)
    {
        if (root == null || candidate == null) return false;
        if (root.children == null) return false;
        for (int i = 0; i < root.children.Count; i++)
        {
            if (root.children[i] is SoundLibraryFolder childFolder)
            {
                if (ReferenceEquals(childFolder, candidate)) return true;
                if (IsDescendantFolder(childFolder, candidate)) return true;
            }
        }

        return false;
    }

    private static string GetUniqueVariantName(List<SfxVariant> list, string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (ContainsVariantName(list, candidate))
        {
            candidate = $"{baseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    private static bool ContainsVariantName(List<SfxVariant> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetUniqueNarrationClipName(List<SoundLibraryEntry> list, string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (ContainsNarrationClipName(list, candidate))
        {
            candidate = $"{baseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    private static bool ContainsNarrationClipName(List<SoundLibraryEntry> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetUniqueNarrationVariantName(List<NarrationVariant> list, string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (ContainsNarrationVariantName(list, candidate))
        {
            candidate = $"{baseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    private static bool ContainsNarrationVariantName(List<NarrationVariant> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    // ----------------------------------------- //
    //                   Rename                  //
    // ----------------------------------------- //

    #region Rename

    private void BeginRename(SoundLibraryEntry entry, SoundLibraryFolder parentFolder, SfxVariantGroup parentVariantGroup = null, NarrationGroup parentNarrationGroup = null, NarrationVariantGroup parentNarrationVariantGroup = null)
    {
        if (entry == null) return;
        renamingEntry = entry;
        renamingParent = parentFolder;
        renamingParentVariantGroup = parentVariantGroup;
        renamingParentNarrationGroup = parentNarrationGroup;
        renamingParentNarrationVariantGroup = parentNarrationVariantGroup;
        renameBuffer = entry.name ?? string.Empty;
        renameSelectAll = true;
        Repaint();
    }

    private void CommitRename(SoundLibraryEntry entry)
    {
        if (entry == null) return;

        string trimmed = (renameBuffer ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            CancelRename();
            return;
        }

        if (string.Equals(entry.name, trimmed, StringComparison.Ordinal))
        {
            CancelRename();
            return;
        }

        if (entry is SfxVariant variant)
        {
            var list = GetVariantList(renamingParentVariantGroup);
            if (list == null) return;
            string unique = GetUniqueVariantName(list, trimmed);
            Undo.RecordObject(library, "Rename Sound Variant");
            variant.name = unique;
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationClip clip)
        {
            var list = GetNarrationEntryList(renamingParentNarrationGroup);
            if (list == null) return;
            string unique = GetUniqueNarrationClipName(list, trimmed);
            Undo.RecordObject(library, "Rename Narration Clip");
            clip.name = unique;
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationVariantGroup narrationVariantGroup)
        {
            var list = GetNarrationEntryList(renamingParentNarrationGroup);
            if (list == null) return;
            string unique = GetUniqueNarrationClipName(list, trimmed);
            Undo.RecordObject(library, "Rename Narration Variant Group");
            narrationVariantGroup.name = unique;
            EditorUtility.SetDirty(library);
        }
        else if (entry is NarrationVariant narrationVariant)
        {
            var list = GetNarrationVariantList(renamingParentNarrationVariantGroup);
            if (list == null) return;
            string unique = GetUniqueNarrationVariantName(list, trimmed);
            Undo.RecordObject(library, "Rename Narration Variant");
            narrationVariant.name = unique;
            EditorUtility.SetDirty(library);
        }
        else
        {
            var list = GetTargetList(renamingParent);
            if (list == null) return;
            string unique = GetUniqueName(list, trimmed);
            Undo.RecordObject(library, "Rename Sound Entry");
            entry.name = unique;
            EditorUtility.SetDirty(library);
        }
        CancelRename();
        hierarchyDirty = true;
    }

    private void CancelRename()
    {
        renamingEntry = null;
        renamingParent = null;
        renamingParentVariantGroup = null;
        renamingParentNarrationGroup = null;
        renamingParentNarrationVariantGroup = null;
        renameBuffer = string.Empty;
        renameSelectAll = false;
        Repaint();
    }

    #endregion
    // ----------------------------------------- //
    //            Clip Drop Menu                 //
    // ----------------------------------------- //
    #region Clip Drop Menu

    private void ShowDropTypeMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add as Music"), false, () => ApplyDropClips(EntryKind.Music));
        menu.AddItem(new GUIContent("Add as SFX"), false, () => ApplyDropClips(EntryKind.Sfx));
        menu.AddItem(new GUIContent("Add as Narration"), false, () => ApplyDropClips(EntryKind.Narration));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Cancel"), false, ClearPendingDrop);
        menu.ShowAsContext();
    }

    private void ApplyDropClips(EntryKind kind)
    {
        if (pendingDropClips == null || pendingDropClips.Count == 0) return;

        Undo.RecordObject(library, "Add Audio Clips");
        for (int i = 0; i < pendingDropClips.Count; i++)
        {
            var filter = kind == EntryKind.Music
                ? SearchFilter.Music
                : kind == EntryKind.Narration ? SearchFilter.Narration : SearchFilter.Sfx;
            AddClipEntry(pendingDropFolder, pendingDropClips[i], filter);
        }

        EditorUtility.SetDirty(library);
        Selection.activeObject = library;
        ClearPendingDrop();
        hierarchyDirty = true;
    }

    private void ClearPendingDrop()
    {
        pendingDropClips = null;
        pendingDropFolder = null;
    }

    #endregion
    // ----------------------------------------- //
    //             Search Filter                 //
    // ----------------------------------------- //
    #region Search Filter

    private void SetSearchToken(string token)
    {
        string withoutType = StripSearchToken(searchText);
        if (string.IsNullOrWhiteSpace(token) || token.Equals("t:all", StringComparison.OrdinalIgnoreCase))
        {
            searchText = withoutType.Trim();
            return;
        }

        searchText = $"{token} {withoutType}".Trim();
    }

    private static SearchFilter GetSearchFilter(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return SearchFilter.All;
        string[] parts = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (part.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
            {
                string value = part.Substring(2).Trim().ToLowerInvariant();
                if (value == "music") return SearchFilter.Music;
                if (value == "sfx") return SearchFilter.Sfx;
                if (value == "narration") return SearchFilter.Narration;
                if (value == "all") return SearchFilter.All;
            }
        }

        return SearchFilter.All;
    }

    private static string StripSearchToken(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return string.Empty;
        string[] parts = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (string part in parts)
        {
            if (!part.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(part);
            }
        }

        return string.Join(" ", kept);
    }

    private enum SearchFilter
    {
        All,
        Music,
        Sfx,
        Narration
    }

    #endregion
    // ----------------------------------------- //
    //                Build List                 //
    // ----------------------------------------- //
    #region Build List

    private void Collect(List<DrawItem> list, SoundLibraryEntry entry, int depth, string parentPath, SoundLibraryFolder parentFolder, List<string> ancestorFolderIds = null)
    {
        if (entry == null) return;

        string name = string.IsNullOrEmpty(entry.name) ? "(no name)" : entry.name;
        string path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

        list.Add(new DrawItem
        {
            entry = entry,
            depth = depth,
            path = path,
            parentFolder = parentFolder,
            parentVariantGroup = null,
            parentNarrationGroup = null,
            parentNarrationVariantGroup = null,
            ancestorFolderIds = ancestorFolderIds
        });

        if (entry is SoundLibraryFolder folder && folder.children != null && folder.children.Count > 0)
        {
            string folderKey = path;

            var nextAncestors = ancestorFolderIds == null ? new List<string>() : new List<string>(ancestorFolderIds);
            nextAncestors.Add(folderKey);

            for (int i = 0; i < folder.children.Count; i++)
                Collect(list, folder.children[i], depth + 1, path, folder, nextAncestors);
        }

        if (entry is SfxVariantGroup group && group.variants != null && group.variants.Count > 0)
        {
            string groupPath = path;
            var variantAncestors = ancestorFolderIds == null ? new List<string>() : new List<string>(ancestorFolderIds);
            variantAncestors.Add(groupPath);

            for (int i = 0; i < group.variants.Count; i++)
            {
                var variant = group.variants[i];
                if (variant == null) continue;
                string varName = string.IsNullOrEmpty(variant.name) ? "(no name)" : variant.name;
                string varPath = $"{groupPath}/{varName}";
                list.Add(new DrawItem
                {
                    entry = variant,
                    depth = depth + 1,
                    path = varPath,
                    parentFolder = parentFolder,
                    parentVariantGroup = group,
                    parentNarrationGroup = null,
                    parentNarrationVariantGroup = null,
                    ancestorFolderIds = variantAncestors
                });
            }
        }

        if (entry is NarrationGroup narrationGroup && narrationGroup.entries != null && narrationGroup.entries.Count > 0)
        {
            string groupPath = path;
            var clipAncestors = ancestorFolderIds == null ? new List<string>() : new List<string>(ancestorFolderIds);
            clipAncestors.Add(groupPath);

            for (int i = 0; i < narrationGroup.entries.Count; i++)
            {
                var clipEntry = narrationGroup.entries[i];
                if (clipEntry == null) continue;
                string clipName = string.IsNullOrEmpty(clipEntry.name) ? "(no name)" : clipEntry.name;
                string clipPath = $"{groupPath}/{clipName}";
                list.Add(new DrawItem
                {
                    entry = clipEntry,
                    depth = depth + 1,
                    path = clipPath,
                    parentFolder = parentFolder,
                    parentVariantGroup = null,
                    parentNarrationGroup = narrationGroup,
                    parentNarrationVariantGroup = null,
                    ancestorFolderIds = clipAncestors
                });

                if (clipEntry is NarrationVariantGroup narrationVariantGroup && narrationVariantGroup.variants != null && narrationVariantGroup.variants.Count > 0)
                {
                    string variantGroupPath = clipPath;
                    var variantAncestors = new List<string>(clipAncestors) { variantGroupPath };
                    for (int j = 0; j < narrationVariantGroup.variants.Count; j++)
                    {
                        var variant = narrationVariantGroup.variants[j];
                        if (variant == null) continue;
                        string varName = string.IsNullOrEmpty(variant.name) ? "(no name)" : variant.name;
                        string varPath = $"{variantGroupPath}/{varName}";
                        list.Add(new DrawItem
                        {
                            entry = variant,
                            depth = depth + 2,
                            path = varPath,
                            parentFolder = parentFolder,
                            parentVariantGroup = null,
                            parentNarrationGroup = narrationGroup,
                            parentNarrationVariantGroup = narrationVariantGroup,
                            ancestorFolderIds = variantAncestors
                        });
                    }
                }
            }
        }
    }

    #endregion
    // ----------------------------------------- //
    //                    Types                  //
    // ----------------------------------------- //
    #region Types

    private enum EntryKind
    {
        Folder,
        Sfx,
        Music,
        Narration
    }

    private struct RowInfo
    {
        public DrawItem item;
        public Rect rect;
    }

    private struct DrawItem
    {
        public SoundLibraryEntry entry;
        public int depth;
        public string path;
        public SoundLibraryFolder parentFolder;
        public SfxVariantGroup parentVariantGroup;
        public NarrationGroup parentNarrationGroup;
        public NarrationVariantGroup parentNarrationVariantGroup;
        public List<string> ancestorFolderIds;
    }

    #endregion
}
