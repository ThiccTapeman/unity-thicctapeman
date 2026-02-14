using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundTimelinePlayer : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private SoundTimeline timeline;
    [SerializeField] private SoundLibrary libraryOverride;
    [SerializeField] private bool playOnStart = true;

    [Header("Emitter")]
    [SerializeField] private Transform emitterOverride;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool logBeatEvents = false;

    public event Action<int, int, int> OnBeat;
    public event Action<string> OnBeatMarker;

    private readonly List<ScheduledEvent> schedule = new();
    private readonly List<ActiveAutomation> activeAutomations = new();
    private readonly Dictionary<string, AudioSource> eventSources = new(StringComparer.Ordinal);

    private double startDspTime;
    private bool isPlaying;
    private float secondsPerBeat = 0.5f;
    private float loopLengthSeconds;
    private int nextEventIndex;
    private int lastBeatIndex = -1;

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (timeline == null)
        {
            Debug.LogWarning("SoundTimelinePlayer: Missing timeline.");
            return;
        }

        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("SoundTimelinePlayer: Missing SoundManager instance.");
            return;
        }

        BuildSchedule();
        startDspTime = AudioSettings.dspTime;
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
        activeAutomations.Clear();
        eventSources.Clear();
    }

    private void Update()
    {
        if (!isPlaying || timeline == null)
        {
            return;
        }

        double now = AudioSettings.dspTime;
        float timeSeconds = (float)(now - startDspTime);
        if (timeSeconds < 0f)
        {
            return;
        }

        if (timeline.loop && loopLengthSeconds > 0f && timeSeconds >= loopLengthSeconds)
        {
            while (timeSeconds >= loopLengthSeconds)
            {
                startDspTime += loopLengthSeconds;
                timeSeconds -= loopLengthSeconds;
            }

            nextEventIndex = 0;
            activeAutomations.Clear();
            lastBeatIndex = -1;
        }

        UpdateBeats(timeSeconds);
        FireScheduledEvents(timeSeconds);
        UpdateAutomations(timeSeconds);
    }

    private void UpdateBeats(float timeSeconds)
    {
        if (secondsPerBeat <= 0f) return;

        int beatIndex = Mathf.FloorToInt(timeSeconds / secondsPerBeat);
        if (beatIndex <= lastBeatIndex) return;

        int beatsPerBar = Mathf.Max(1, timeline.beatsPerBar);
        for (int i = lastBeatIndex + 1; i <= beatIndex; i++)
        {
            int barIndex = i / beatsPerBar;
            int beatInBar = i % beatsPerBar;
            OnBeat?.Invoke(i, barIndex, beatInBar);
            if (logBeatEvents)
            {
                Debug.Log($"SoundTimelinePlayer: Beat {i} (bar {barIndex}, beat {beatInBar}).");
            }
        }

        lastBeatIndex = beatIndex;
    }

    private void FireScheduledEvents(float timeSeconds)
    {
        while (nextEventIndex < schedule.Count && timeSeconds >= schedule[nextEventIndex].startSeconds)
        {
            var scheduled = schedule[nextEventIndex];
            nextEventIndex++;
            if (scheduled.eventData == null || !scheduled.eventData.enabled)
            {
                continue;
            }

            FireEvent(scheduled, timeSeconds);
        }
    }

    private void FireEvent(ScheduledEvent scheduled, float timeSeconds)
    {
        switch (scheduled.eventData)
        {
            case PlaySfxEvent sfxEvent:
                {
                    var source = PlaySfxEvent(sfxEvent);
                    if (source != null)
                    {
                        eventSources[scheduled.eventId] = source;
                    }
                    break;
                }
            case PlayMusicEvent musicEvent:
                {
                    if (musicEvent.alignToBeat && secondsPerBeat > 0f)
                    {
                        float beatPosition = timeSeconds / secondsPerBeat;
                        float beatFraction = beatPosition - Mathf.Floor(beatPosition);
                        float delay = beatFraction < 0.001f ? 0f : ((Mathf.Floor(beatPosition) + 1) * secondsPerBeat - timeSeconds);
                        delay = Mathf.Max(0f, delay);
                        StartCoroutine(PlayMusicDelayed(musicEvent, scheduled.eventId, delay));
                    }
                    else
                    {
                        var source = PlayMusicEvent(musicEvent);
                        if (source != null)
                        {
                            eventSources[scheduled.eventId] = source;
                        }
                    }
                    break;
                }
            case PlayNarrationEvent narrationEvent:
                {
                    var source = PlayNarrationEvent(narrationEvent);
                    if (source != null)
                    {
                        eventSources[scheduled.eventId] = source;
                    }
                    break;
                }
            case AutomationEvent automationEvent:
                {
                    if (scheduled.durationSeconds <= 0f)
                    {
                        ApplyAutomationValue(automationEvent, scheduled.targetEventId, 1f);
                    }
                    else
                    {
                        activeAutomations.Add(new ActiveAutomation
                        {
                            eventData = automationEvent,
                            targetEventId = scheduled.targetEventId,
                            startSeconds = scheduled.startSeconds,
                            durationSeconds = scheduled.durationSeconds
                        });
                    }
                    break;
                }
            case BeatMarkerEvent markerEvent:
                {
                    if (!string.IsNullOrWhiteSpace(markerEvent.label))
                    {
                        OnBeatMarker?.Invoke(markerEvent.label);
                    }
                    break;
                }
        }
    }

    private void UpdateAutomations(float timeSeconds)
    {
        if (activeAutomations.Count == 0) return;

        for (int i = activeAutomations.Count - 1; i >= 0; i--)
        {
            var automation = activeAutomations[i];
            float elapsed = timeSeconds - automation.startSeconds;
            if (elapsed < 0f) continue;

            float t = automation.durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / automation.durationSeconds);
            ApplyAutomationValue(automation.eventData, automation.targetEventId, t);

            if (t >= 1f)
            {
                activeAutomations.RemoveAt(i);
            }
        }
    }

    private void ApplyAutomationValue(AutomationEvent automation, string targetEventId, float t)
    {
        if (automation == null) return;

        float curveValue = automation.curve != null ? automation.curve.Evaluate(t) : t;
        float value = Mathf.Lerp(automation.minValue, automation.maxValue, curveValue);

        switch (automation.target)
        {
            case TimelineAutomationTarget.SourceVolume:
                if (TryGetSource(targetEventId, out var volumeSource))
                {
                    volumeSource.volume = value;
                }
                break;
            case TimelineAutomationTarget.SourcePitch:
                if (TryGetSource(targetEventId, out var pitchSource))
                {
                    pitchSource.pitch = value;
                }
                break;
            case TimelineAutomationTarget.MixerParam:
                ApplyMixerValue(automation.mixerGroup, automation.mixerParam, value);
                break;
        }
    }

    private void ApplyMixerValue(AudioMixerGroup group, string param, float value)
    {
        if (group == null || group.audioMixer == null) return;
        if (string.IsNullOrWhiteSpace(param)) return;
        group.audioMixer.SetFloat(param, value);
    }

    private bool TryGetSource(string eventId, out AudioSource source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        return eventSources.TryGetValue(eventId, out source) && source != null;
    }

    private AudioSource PlaySfxEvent(PlaySfxEvent evt)
    {
        var entry = ResolveEntry(evt.sfx);
        var emitter = emitterOverride != null ? emitterOverride : transform;
        Vector3 position = evt.useEmitterPosition
            ? emitter.position + positionOffset + evt.positionOffset
            : positionOffset + evt.positionOffset;
        Transform follow = evt.followEmitter ? emitter : null;
        bool? loopOverride = evt.overrideLoop ? evt.loop : (bool?)null;
        return entry != null
            ? SoundManager.Instance.PlaySfx(entry, position, follow, evt.volumeMultiplier, loopOverride)
            : SoundManager.Instance.PlaySfx(evt.sfx.entryName, position, follow, evt.volumeMultiplier, loopOverride);
    }

    private AudioSource PlayMusicEvent(PlayMusicEvent evt)
    {
        var entry = ResolveEntry(evt.music);
        bool? loopOverride = evt.overrideLoop ? evt.loop : (bool?)null;
        if (entry is MusicDefinition musicDef)
        {
            return SoundManager.Instance.PlayMusicLayer(evt.layerIndex, musicDef, evt.volumeMultiplier, loopOverride);
        }

        return SoundManager.Instance.PlayMusicLayer(evt.layerIndex, evt.music.entryName, evt.volumeMultiplier, loopOverride);
    }

    private AudioSource PlayNarrationEvent(PlayNarrationEvent evt)
    {
        var entry = ResolveEntry(evt.narration);
        var emitter = emitterOverride != null ? emitterOverride : transform;
        Vector3 position = evt.useEmitterPosition
            ? emitter.position + positionOffset + evt.positionOffset
            : positionOffset + evt.positionOffset;
        Transform follow = evt.followEmitter ? emitter : null;
        return entry != null
            ? SoundManager.Instance.PlayNarration(entry, position, follow, evt.volumeMultiplier)
            : SoundManager.Instance.PlayNarration(evt.narration.entryName, position, follow, evt.volumeMultiplier);
    }

    private System.Collections.IEnumerator PlayMusicDelayed(PlayMusicEvent evt, string eventId, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        var source = PlayMusicEvent(evt);
        if (source != null)
        {
            eventSources[eventId] = source;
        }
    }

    private SoundLibraryEntry ResolveEntry(SoundEntryReference reference)
    {
        if (reference == null) return null;
        if (reference.entry != null) return reference.entry;
        if (string.IsNullOrWhiteSpace(reference.entryName)) return null;
        var lib = ResolveLibrary();
        return lib != null ? lib.GetEntry(reference.entryName) : null;
    }

    private SoundLibrary ResolveLibrary()
    {
        if (libraryOverride != null) return libraryOverride;
        return SoundManager.Instance != null ? SoundManager.Instance.Library : null;
    }

    private void BuildSchedule()
    {
        schedule.Clear();
        activeAutomations.Clear();
        eventSources.Clear();
        nextEventIndex = 0;
        lastBeatIndex = -1;

        secondsPerBeat = 60f / Mathf.Max(1f, ResolveBpm());
        BuildScheduleRecursive(timeline, string.Empty, timeline.startOffsetSeconds, 1f);
        schedule.Sort((a, b) => a.startSeconds.CompareTo(b.startSeconds));
        loopLengthSeconds = ResolveLoopLengthSeconds();
    }

    private void BuildScheduleRecursive(SoundTimeline source, string prefix, float timeOffsetSeconds, float timeScale)
    {
        if (source == null || source.events == null) return;

        for (int i = 0; i < source.events.Count; i++)
        {
            var evt = source.events[i];
            if (evt == null || !evt.enabled) continue;

            evt.EnsureId();
            float startSeconds = timeOffsetSeconds + ConvertToSeconds(evt.timeMode, evt.startTime, timeScale);
            float durationSeconds = ConvertToSeconds(evt.timeMode, evt.duration, timeScale);

            if (evt is NestedTimelineEvent nested && nested.timeline != null)
            {
                float nestedOffset = startSeconds;
                if (nested.includeChildStartOffset)
                {
                    nestedOffset += nested.timeline.startOffsetSeconds;
                }

                string nestedPrefix = string.IsNullOrWhiteSpace(nested.idPrefix)
                    ? $"{prefix}{evt.eventId}/"
                    : $"{prefix}{nested.idPrefix}";

                BuildScheduleRecursive(nested.timeline, nestedPrefix, nestedOffset, timeScale * Mathf.Max(0.01f, nested.timeScale));
                continue;
            }

            schedule.Add(new ScheduledEvent
            {
                eventData = evt,
                eventId = $"{prefix}{evt.eventId}",
                targetEventId = ResolveTargetEventId(evt, prefix),
                startSeconds = startSeconds,
                durationSeconds = durationSeconds
            });
        }
    }

    private float ConvertToSeconds(TimelineTimeMode mode, float value, float timeScale)
    {
        if (mode == TimelineTimeMode.Seconds)
        {
            return value * timeScale;
        }

        return value * secondsPerBeat * timeScale;
    }

    private string ResolveTargetEventId(TimelineEvent evt, string prefix)
    {
        if (evt is AutomationEvent automation && !string.IsNullOrWhiteSpace(automation.targetEventId))
        {
            return $"{prefix}{automation.targetEventId}";
        }

        return null;
    }

    private float ResolveLoopLengthSeconds()
    {
        if (!timeline.loop) return 0f;
        if (timeline.loopLengthBeats > 0f)
        {
            return timeline.loopLengthBeats * secondsPerBeat;
        }

        float maxEnd = 0f;
        for (int i = 0; i < schedule.Count; i++)
        {
            var scheduled = schedule[i];
            float end = scheduled.startSeconds + Mathf.Max(0f, scheduled.durationSeconds);
            if (end > maxEnd) maxEnd = end;
        }

        return maxEnd;
    }

    private float ResolveBpm()
    {
        if (timeline == null) return 120f;
        if (timeline.bpm > 0f) return timeline.bpm;
        if (!timeline.autoDetectBpm) return 120f;

        var musicDef = FindFirstMusicDefinition(timeline);
        AudioClip clip = musicDef != null ? (musicDef.loopClip != null ? musicDef.loopClip : musicDef.clip) : null;
        if (clip != null && clip.length > 0f)
        {
            float beats = Mathf.Max(1, timeline.beatsPerBar) * Mathf.Max(1, timeline.barsForBpmFallback);
            return (beats / clip.length) * 60f;
        }

        return 120f;
    }

    private MusicDefinition FindFirstMusicDefinition(SoundTimeline source)
    {
        if (source == null || source.events == null) return null;

        for (int i = 0; i < source.events.Count; i++)
        {
            if (source.events[i] is PlayMusicEvent musicEvent)
            {
                var entry = ResolveEntry(musicEvent.music);
                if (entry is MusicDefinition musicDef)
                {
                    return musicDef;
                }
            }

            if (source.events[i] is NestedTimelineEvent nested && nested.timeline != null)
            {
                var nestedResult = FindFirstMusicDefinition(nested.timeline);
                if (nestedResult != null) return nestedResult;
            }
        }

        return null;
    }

    private class ScheduledEvent
    {
        public TimelineEvent eventData;
        public string eventId;
        public string targetEventId;
        public float startSeconds;
        public float durationSeconds;
    }

    private class ActiveAutomation
    {
        public AutomationEvent eventData;
        public string targetEventId;
        public float startSeconds;
        public float durationSeconds;
    }
}
