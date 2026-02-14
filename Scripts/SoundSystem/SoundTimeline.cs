using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sound Timeline", fileName = "SoundTimeline")]
public class SoundTimeline : ScriptableObject
{
    [Header("Tempo")]
    [Min(0f)] public float bpm = 120f;
    public bool autoDetectBpm = true;
    [Min(1)] public int beatsPerBar = 4;
    [Min(1)] public int barsForBpmFallback = 4;

    [Header("Playback")]
    [Min(0f)] public float startOffsetSeconds = 0f;
    public bool loop;
    [Min(0f)] public float loopLengthBeats = 0f;

    [Header("Events")]
    [SerializeReference] public List<TimelineEvent> events = new();
}

public enum TimelineTimeMode
{
    Beats,
    Seconds
}

public enum TimelineAutomationTarget
{
    SourceVolume,
    SourcePitch,
    MixerParam
}

[Serializable]
public class SoundEntryReference
{
    public string entryName;
    [SerializeReference] public SoundLibraryEntry entry;
}

[Serializable]
public abstract class TimelineEvent
{
    public string eventId;
    public bool enabled = true;
    public TimelineTimeMode timeMode = TimelineTimeMode.Beats;
    [Min(0f)] public float startTime = 0f;
    [Min(0f)] public float duration = 0f;

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            eventId = Guid.NewGuid().ToString("N");
        }
    }
}

[Serializable]
public class PlaySfxEvent : TimelineEvent
{
    public SoundEntryReference sfx = new();
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    public bool overrideLoop;
    public bool loop;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
}

[Serializable]
public class PlayMusicEvent : TimelineEvent
{
    public SoundEntryReference music = new();
    [Min(0)] public int layerIndex = 0;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    public bool overrideLoop = true;
    public bool loop = true;
    public bool alignToBeat = true;
}

[Serializable]
public class PlayNarrationEvent : TimelineEvent
{
    public SoundEntryReference narration = new();
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
}

[Serializable]
public class AutomationEvent : TimelineEvent
{
    public string targetEventId;
    public TimelineAutomationTarget target = TimelineAutomationTarget.SourceVolume;
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float minValue = 0f;
    public float maxValue = 1f;
    public AudioMixerGroup mixerGroup;
    public string mixerParam;
}

[Serializable]
public class BeatMarkerEvent : TimelineEvent
{
    public string label;
}

[Serializable]
public class NestedTimelineEvent : TimelineEvent
{
    public SoundTimeline timeline;
    [Min(0.01f)] public float timeScale = 1f;
    public string idPrefix = "";
    public bool includeChildStartOffset = true;
}
