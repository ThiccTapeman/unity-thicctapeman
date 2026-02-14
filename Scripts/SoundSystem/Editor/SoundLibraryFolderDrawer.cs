using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(SoundLibraryFolder), true)]
public class SoundLibraryFolderDrawer : PropertyDrawer
{
    private readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null)
        {
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;

        Rect foldRect = new Rect(position.x, position.y, position.width, line);
        EditorGUI.LabelField(foldRect, label);

        EditorGUI.indentLevel++;

        var nameProp = property.FindPropertyRelative("name");
        var childrenProp = property.FindPropertyRelative("children");

        Rect nameRect = new Rect(position.x, foldRect.y + line + spacing, position.width, line);
        EditorGUI.PropertyField(nameRect, nameProp);

        if (childrenProp != null)
        {
            var list = GetList(property, childrenProp);
            Rect listRect = new Rect(position.x, nameRect.y + line + spacing, position.width, list.GetHeight());
            list.DoList(listRect);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        if (property == null)
        {
            return line;
        }

        float spacing = 2f;
        float height = line + spacing; // header line
        height += line + spacing; // name

        var childrenProp = property.FindPropertyRelative("children");
        if (childrenProp != null)
        {
            var list = GetList(property, childrenProp);
            height += list.GetHeight();
        }

        return height;
    }

    private ReorderableList GetList(SerializedProperty folderProperty, SerializedProperty childrenProperty)
    {
        string key = folderProperty.propertyPath;
        if (lists.TryGetValue(key, out var list))
        {
            return list;
        }

        list = new ReorderableList(folderProperty.serializedObject, childrenProperty, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Children");
        list.elementHeightCallback = index =>
        {
            var element = childrenProperty.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 4f;
        };
        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            rect.y += 2f;
            var element = childrenProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, new GUIContent($"Entry {index}"), true);
        };
        list.onAddDropdownCallback = (rect, l) => ShowAddMenu(rect, childrenProperty);

        lists[key] = list;
        return list;
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
        if (listProperty == null)
        {
            return;
        }

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
}
