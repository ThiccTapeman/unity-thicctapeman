using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Audio/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [SerializeReference] public List<SoundLibraryEntry> entries = new();

    [NonSerialized] private Dictionary<string, SoundLibraryEntry> entryLookup;

    public void BuildLookup()
    {
        entryLookup = new Dictionary<string, SoundLibraryEntry>(StringComparer.Ordinal);
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            AddEntryRecursive(entries[i]);
        }
    }

    private void AddEntryRecursive(SoundLibraryEntry entry)
    {
        if (entry == null) return;

        entry.EnsureId();

        if (!string.IsNullOrWhiteSpace(entry.name))
        {
            if (!entryLookup.ContainsKey(entry.name))
            {
                entryLookup.Add(entry.name, entry);
            }
            else
            {
                Debug.LogWarning($"SoundLibrary: Duplicate name '{entry.name}'. Keeping first occurrence.");
            }
        }

        if (entry is SoundLibraryFolder folder && folder.children != null)
        {
            for (int i = 0; i < folder.children.Count; i++)
            {
                AddEntryRecursive(folder.children[i]);
            }
        }

        if (entry is NarrationGroup narrationGroup && narrationGroup.entries != null)
        {
            for (int i = 0; i < narrationGroup.entries.Count; i++)
            {
                AddEntryRecursive(narrationGroup.entries[i]);
            }
        }

        if (entry is NarrationVariantGroup narrationVariantGroup && narrationVariantGroup.variants != null)
        {
            for (int i = 0; i < narrationVariantGroup.variants.Count; i++)
            {
                AddEntryRecursive(narrationVariantGroup.variants[i]);
            }
        }
    }

    private void EnsureLookup()
    {
        if (entryLookup == null) BuildLookup();
    }

    public SoundLibraryEntry GetEntry(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        EnsureLookup();
        entryLookup.TryGetValue(name, out var entry);
        return entry;
    }

    public T GetEntry<T>(string name) where T : SoundLibraryEntry
    {
        return GetEntry(name) as T;
    }

    public SfxDefinition GetSfx(string name) => GetEntry<SfxDefinition>(name);

    public MusicDefinition GetMusic(string name) => GetEntry<MusicDefinition>(name);

    public SoundLibraryFolder GetFolder(string name) => GetEntry<SoundLibraryFolder>(name);

    public SfxVariantGroup GetSfxVariantGroup(string name) => GetEntry<SfxVariantGroup>(name);

    public NarrationGroup GetNarrationGroup(string name) => GetEntry<NarrationGroup>(name);

    public bool TryGetSfxVariantByPath(string name, out SfxVariantGroup group, out SfxVariant variant)
    {
        group = null;
        variant = null;
        if (!TrySplitGroupItemName(name, out var groupName, out var itemName)) return false;

        group = GetSfxVariantGroup(groupName);
        if (group == null || group.variants == null) return false;

        for (int i = 0; i < group.variants.Count; i++)
        {
            var candidate = group.variants[i];
            if (candidate == null) continue;
            if (string.Equals(candidate.name, itemName, StringComparison.OrdinalIgnoreCase))
            {
                variant = candidate;
                return true;
            }
        }

        group = null;
        return false;
    }

    public bool TryGetNarrationEntryByPath(string name, out NarrationGroup group, out NarrationPlayable entry)
    {
        group = null;
        entry = null;
        if (!TrySplitGroupItemName(name, out var groupName, out var itemName)) return false;

        group = GetNarrationGroup(groupName);
        if (group == null || group.entries == null) return false;

        for (int i = 0; i < group.entries.Count; i++)
        {
            var candidate = group.entries[i];
            if (candidate == null) continue;
            if (string.Equals(candidate.name, itemName, StringComparison.OrdinalIgnoreCase))
            {
                if (candidate is NarrationVariantGroup variantGroup)
                {
                    var variant = PickNarrationVariant(variantGroup);
                    if (variant != null)
                    {
                        entry = variant;
                        return true;
                    }

                    continue;
                }

                if (candidate is NarrationPlayable playable)
                {
                    entry = playable;
                    return true;
                }
            }

            if (candidate is NarrationVariantGroup groupCandidate && groupCandidate.variants != null)
            {
                for (int j = 0; j < groupCandidate.variants.Count; j++)
                {
                    var variant = groupCandidate.variants[j];
                    if (variant == null) continue;
                    if (string.Equals(variant.name, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = variant;
                        return true;
                    }
                }
            }
        }

        group = null;
        return false;
    }

    private static NarrationVariant PickNarrationVariant(NarrationVariantGroup group)
    {
        if (group == null || group.variants == null || group.variants.Count == 0) return null;

        int start = UnityEngine.Random.Range(0, group.variants.Count);
        for (int i = 0; i < group.variants.Count; i++)
        {
            var candidate = group.variants[(start + i) % group.variants.Count];
            if (candidate != null && candidate.clip != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TrySplitGroupItemName(string name, out string groupName, out string itemName)
    {
        groupName = null;
        itemName = null;
        if (string.IsNullOrWhiteSpace(name)) return false;

        int dotIndex = name.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= name.Length - 1) return false;

        groupName = name.Substring(0, dotIndex).Trim();
        itemName = name.Substring(dotIndex + 1).Trim();
        return !string.IsNullOrWhiteSpace(groupName) && !string.IsNullOrWhiteSpace(itemName);
    }
}

[Serializable]
public abstract class SoundLibraryEntry
{
    [SerializeField, HideInInspector]
    private string id;
    public string name = "";

    public string Id => id;

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
        }
    }

    public void SetId(string value)
    {
        id = value;
    }

    public void ClearId()
    {
        id = null;
    }

    public override string ToString()
    {
        return $"{name}";
    }
}

[Serializable]
public class SoundLibraryFolder : SoundLibraryEntry
{
    [SerializeReference]
    public List<SoundLibraryEntry> children = new();

    public SoundLibraryFolder() { }

    public SoundLibraryFolder(string name, List<SoundLibraryEntry> children = null)
    {
        this.name = name;
        if (children != null) this.children = children;
    }
}

[Serializable]
public abstract class SoundDefinition : SoundLibraryEntry
{
    public AudioClip clip;

    [Range(0f, 1f)] public float volume = 1f;

    public bool loop;

    protected SoundDefinition(string name)
    {
        this.name = name;
    }

    protected SoundDefinition() { }
}

[Serializable]
public class SfxDefinition : SoundDefinition
{
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float minDistance = 1f;
    public float maxDistance = 25f;
    public AudioMixerGroup mixerGroup;
    [FormerlySerializedAs("variantGroup")]
    [SerializeField, HideInInspector]
    private SfxVariantGroup legacyVariantGroup;

    public SfxDefinition(string name) : base(name) { }
    public SfxDefinition() { }
}

[Serializable]
public class MusicDefinition : SoundDefinition
{
    public AudioClip introClip;
    public AudioClip loopClip;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    public AudioMixerGroup mixerGroup;

    public MusicDefinition(string name) : base(name) { }
    public MusicDefinition() { }
}

[Serializable]
public class SfxVariant : SoundLibraryEntry
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float probability = 1f;
}

[Serializable]
public class SfxVariantGroup : SoundLibraryEntry
{
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float minDistance = 1f;
    public float maxDistance = 25f;
    public AudioMixerGroup mixerGroup;
    public bool loop;

    [SerializeReference]
    public List<SfxVariant> variants = new();

    public SfxVariantGroup()
    {
        name = ".sfxvariants";
    }
}

[Serializable]
public abstract class NarrationPlayable : SoundLibraryEntry
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Min(0f)] public float waitBeforeStart = 0f;
}

[Serializable]
public class NarrationClip : NarrationPlayable { }

[Serializable]
public class NarrationVariant : NarrationPlayable { }

[Serializable]
public class NarrationVariantGroup : SoundLibraryEntry
{
    [SerializeReference]
    public List<NarrationVariant> variants = new();

    public NarrationVariantGroup()
    {
        name = ".narrationvariants";
    }
}

[Serializable]
public class NarrationGroup : SoundLibraryEntry
{
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float spatialBlend = 0f;
    public float minDistance = 1f;
    public float maxDistance = 25f;
    public AudioMixerGroup mixerGroup;

    [FormerlySerializedAs("clips")]
    [SerializeReference]
    public List<SoundLibraryEntry> entries = new();

    public NarrationGroup()
    {
        name = ".narration";
    }
}
