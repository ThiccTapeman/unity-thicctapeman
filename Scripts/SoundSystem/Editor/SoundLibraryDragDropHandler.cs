using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SoundLibraryDragDropHandler
{
    private const string DragEntryKey = "SoundLibraryEntry";

    static SoundLibraryDragDropHandler()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event evt = Event.current;
        if (evt == null) return;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;

        var entry = DragAndDrop.GetGenericData(DragEntryKey) as SoundLibraryEntry;
        if (entry is not SfxDefinition && entry is not SfxVariantGroup) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            GameObject target = HandleUtility.PickGameObject(evt.mousePosition, false);
            ApplyToGameObject(target, entry.name);
        }

        evt.Use();
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        Event evt = Event.current;
        if (evt == null) return;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!selectionRect.Contains(evt.mousePosition)) return;

        var entry = DragAndDrop.GetGenericData(DragEntryKey) as SoundLibraryEntry;
        if (entry is not SfxDefinition && entry is not SfxVariantGroup) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
#pragma warning disable CS0618
            GameObject target = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
#pragma warning restore CS0618
            ApplyToGameObject(target, entry.name);
        }

        evt.Use();
    }

    private static void ApplyToGameObject(GameObject target, string sfxName)
    {
        if (target == null || string.IsNullOrWhiteSpace(sfxName)) return;

        var emitter = target.GetComponent<SoundEmitter>();
        if (emitter == null)
        {
            emitter = Undo.AddComponent<SoundEmitter>(target);
        }

        var serialized = new SerializedObject(emitter);
        var prop = serialized.FindProperty("sfxName");
        if (prop != null)
        {
            prop.stringValue = sfxName;
            serialized.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(target);
    }
}
