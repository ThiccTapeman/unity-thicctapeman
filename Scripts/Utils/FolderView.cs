using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThiccTapeman.FolderView
{
#if UNITY_EDITOR
    using UnityEditor;

    public class FolderViewWidget
    {
        private readonly Dictionary<string, bool> foldouts = new();

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

        public void DrawRow(string key, int depth, int childCount, Func<Rect> drawName, out Rect foldoutRect, out Rect nameRect)
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
    }
#endif
}
