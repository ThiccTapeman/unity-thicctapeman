using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SoundLibrary))]
public class SoundLibraryEditor : Editor
{
    private ReorderableList entriesList;
    private SerializedProperty entriesProperty;

    private void OnEnable()
    {
        entriesProperty = serializedObject.FindProperty("entries");
        entriesList = new ReorderableList(serializedObject, entriesProperty, true, true, true, true);
        entriesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Entries");
        entriesList.elementHeightCallback = index =>
        {
            var element = entriesProperty.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 4f;
        };
        entriesList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            rect.y += 2f;
            var element = entriesProperty.GetArrayElementAtIndex(index);
            rect.height = EditorGUI.GetPropertyHeight(element, true);
            EditorGUI.PropertyField(rect, element, new GUIContent($"Entry {index}"), true);
        };
        entriesList.onAddDropdownCallback = (rect, list) => ShowAddMenu(rect, entriesProperty);
        SoundLibraryEditorSelection.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        SoundLibraryEditorSelection.SelectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (!DrawSelectedEntry())
        {
            entriesList.DoLayoutList();
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddMenu(Rect rect, SerializedProperty listProperty)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Folder"), false, () => AddEntry(listProperty, typeof(SoundLibraryFolder)));
        menu.AddItem(new GUIContent("SFX"), false, () => AddEntry(listProperty, typeof(SfxDefinition)));
        menu.AddItem(new GUIContent("Music"), false, () => AddEntry(listProperty, typeof(MusicDefinition)));
        menu.AddItem(new GUIContent("Narration"), false, () => AddEntry(listProperty, typeof(NarrationGroup)));
        menu.ShowAsContext();
    }

    private void AddEntry(SerializedProperty listProperty, Type type)
    {
        if (listProperty == null) return;

        listProperty.serializedObject.Update();
        Undo.RecordObject(listProperty.serializedObject.targetObject, "Add Sound Library Entry");

        int index = listProperty.arraySize;
        listProperty.arraySize++;
        var element = listProperty.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type);
        element.isExpanded = true;

        listProperty.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(listProperty.serializedObject.targetObject);
    }

    private bool DrawSelectedEntry()
    {
        var library = target as SoundLibrary;
        if (library == null) return false;
        if (SoundLibraryEditorSelection.ActiveLibrary != library) return false;

        var entry = SoundLibraryEditorSelection.ActiveEntry;
        if (entry == null) return false;

        var entriesProp = serializedObject.FindProperty("entries");
        if (entriesProp == null) return false;

        if (!TryFindEntryProperty(entriesProp, entry, out var found))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Selected entry not found in this library.", MessageType.Info);
            return true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Entry", EditorStyles.boldLabel);
        if (GUILayout.Button("Show Library", GUILayout.Width(120)))
        {
            SoundLibraryEditorSelection.Clear();
            return true;
        }
        found.isExpanded = true;
        EditorGUILayout.PropertyField(found, true);
        return true;
    }

    private bool TryFindEntryProperty(SerializedProperty listProp, SoundLibraryEntry targetEntry, out SerializedProperty found)
    {
        found = null;
        if (listProp == null || !listProp.isArray) return false;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            if (ReferenceEquals(element.managedReferenceValue, targetEntry))
            {
                found = element;
                return true;
            }

            var childrenProp = element.FindPropertyRelative("children");
            if (childrenProp != null && childrenProp.isArray)
            {
                if (TryFindEntryProperty(childrenProp, targetEntry, out found))
                {
                    return true;
                }
            }

            var variantsProp = element.FindPropertyRelative("variants");
            if (variantsProp != null && variantsProp.isArray)
            {
                if (TryFindEntryProperty(variantsProp, targetEntry, out found))
                {
                    return true;
                }
            }

            var entriesProp = element.FindPropertyRelative("entries");
            if (entriesProp != null && entriesProp.isArray)
            {
                if (TryFindEntryProperty(entriesProp, targetEntry, out found))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
