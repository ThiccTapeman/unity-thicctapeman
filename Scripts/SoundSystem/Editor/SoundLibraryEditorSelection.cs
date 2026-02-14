using System;
using UnityEngine;

public static class SoundLibraryEditorSelection
{
    public static SoundLibrary ActiveLibrary { get; private set; }
    public static SoundLibraryEntry ActiveEntry { get; private set; }
    public static event Action SelectionChanged;

    public static void Set(SoundLibrary library, SoundLibraryEntry entry)
    {
        ActiveLibrary = library;
        ActiveEntry = entry;
        SelectionChanged?.Invoke();
    }

    public static void Clear()
    {
        ActiveLibrary = null;
        ActiveEntry = null;
        SelectionChanged?.Invoke();
    }
}
