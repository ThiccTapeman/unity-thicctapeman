using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class SoundSfxClip : PlayableAsset, ITimelineClipAsset
{
    public SoundEntryReference sfx = new();
    public ExposedReference<Transform> emitter;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    public bool overrideLoop;
    public bool loop;
    [Min(0f)] public float startOffsetSeconds = 0f;
    [Min(0f)] public float loopStartSeconds = 0f;
    [Min(0f)] public float loopEndSeconds = 0f;
    public bool syncToTimeline = true;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
    [Min(0f)] public float stopFadeSeconds = 0.1f;
    [Min(0f)] public float fadeInDuration = 0f;
    [Min(0f)] public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override double duration
    {
        get
        {
            double length = ResolveClipLengthSeconds();
            return length > 0d ? length : base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundSfxBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.sfx = sfx;
        behaviour.emitter = emitter.Resolve(graph.GetResolver());
        behaviour.volumeMultiplier = volumeMultiplier;
        behaviour.overrideLoop = overrideLoop;
        behaviour.loop = loop;
        behaviour.startOffsetSeconds = startOffsetSeconds;
        behaviour.loopStartSeconds = loopStartSeconds;
        behaviour.loopEndSeconds = loopEndSeconds;
        behaviour.syncToTimeline = syncToTimeline;
        behaviour.followEmitter = followEmitter;
        behaviour.useEmitterPosition = useEmitterPosition;
        behaviour.positionOffset = positionOffset;
        behaviour.stopFadeSeconds = stopFadeSeconds;
        behaviour.fadeInDuration = fadeInDuration;
        behaviour.fadeOutDuration = fadeOutDuration;
        behaviour.fadeInCurve = fadeInCurve;
        behaviour.fadeOutCurve = fadeOutCurve;
        return playable;
    }

    private double ResolveClipLengthSeconds()
    {
        var entry = sfx != null ? sfx.entry : null;
        if (entry is SfxDefinition sfxDef)
        {
            return sfxDef.clip != null ? sfxDef.clip.length : 0d;
        }

        if (entry is SfxVariant variant)
        {
            return variant.clip != null ? variant.clip.length : 0d;
        }

        if (entry is SfxVariantGroup group && group.variants != null)
        {
            for (int i = 0; i < group.variants.Count; i++)
            {
                var candidate = group.variants[i];
                if (candidate != null && candidate.clip != null)
                {
                    return candidate.clip.length;
                }
            }
        }

        return 0d;
    }
}

public class SoundSfxBehaviour : PlayableBehaviour
{
    public SoundEntryReference sfx;
    public Transform emitter;
    public float volumeMultiplier = 1f;
    public bool overrideLoop;
    public bool loop;
    public float startOffsetSeconds = 0f;
    public float loopStartSeconds = 0f;
    public float loopEndSeconds = 0f;
    public bool syncToTimeline = true;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
    public float stopFadeSeconds = 0.1f;
    public float fadeInDuration = 0f;
    public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private AudioSource activeSource;
    private bool fired;
    private float baseVolume;
    private bool manualLoop;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired) return;
        fired = true;

        EnsureActiveSource(playable);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (activeSource != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopSfx(activeSource, stopFadeSeconds);
        }

        activeSource = null;
        fired = false;
        manualLoop = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (activeSource == null)
        {
            if (info.weight > 0f)
            {
                EnsureActiveSource(playable);
            }
            else
            {
                return;
            }
        }

        float localTime = (float)playable.GetTime();
        float duration = (float)playable.GetDuration();

        bool forceSync = info.evaluationType == FrameData.EvaluationType.Evaluate;
        ApplyLoop(localTime, forceSync);
        ApplyFade(localTime, duration, info.weight);
    }

    private void EnsureActiveSource(Playable playable)
    {
        if (activeSource != null) return;
        if (SoundManager.Instance == null) return;

        Transform emit = emitter != null ? emitter : SoundManager.Instance.transform;
        Vector3 position = useEmitterPosition ? emit.position + positionOffset : positionOffset;
        Transform follow = followEmitter ? emit : null;
        bool? loopOverride = overrideLoop ? loop : (bool?)null;

        var entry = ResolveEntry(sfx);
        activeSource = entry != null
            ? SoundManager.Instance.PlaySfx(entry, position, follow, volumeMultiplier, loopOverride)
            : SoundManager.Instance.PlaySfx(sfx.entryName, position, follow, volumeMultiplier, loopOverride);

        if (activeSource != null)
        {
            baseVolume = activeSource.volume;
            ConfigureStartAndLoop();
            if (!activeSource.isPlaying)
            {
                activeSource.Play();
            }
        }
    }

    private void ConfigureStartAndLoop()
    {
        if (activeSource.clip == null) return;

        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, activeSource.clip.length - 0.01f);
        if (startTime > 0f)
        {
            activeSource.time = startTime;
        }

        manualLoop = loop && loopEndSeconds > loopStartSeconds;
        if (manualLoop)
        {
            activeSource.loop = false;
            activeSource.time = Mathf.Clamp(loopStartSeconds, 0f, activeSource.clip.length - 0.01f);
        }
    }

    private void ApplyLoop(float localTime, bool forceSync)
    {
        if (!syncToTimeline || activeSource == null || activeSource.clip == null) return;

        float clipLength = activeSource.clip.length;
        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clipLength - 0.01f));
        float loopStart = Mathf.Clamp(loopStartSeconds, 0f, Mathf.Max(0f, clipLength - 0.01f));
        float loopEnd = loopEndSeconds > loopStart ? Mathf.Min(loopEndSeconds, clipLength) : clipLength;

        if (loop)
        {
            float actualStart = loopEndSeconds > loopStartSeconds ? loopStart : startTime;
            float actualEnd = loopEndSeconds > loopStartSeconds ? loopEnd : clipLength;
            float length = Mathf.Max(0.01f, actualEnd - actualStart);
            float t = localTime % length;
            float desired = actualStart + t;
            float drift = Mathf.Abs(activeSource.time - desired);
            if (forceSync || drift > 0.05f)
            {
                activeSource.time = desired;
            }
            return;
        }

        float nonLoopTime = Mathf.Clamp(startTime + localTime, 0f, clipLength);
        float nonLoopDrift = Mathf.Abs(activeSource.time - nonLoopTime);
        if (forceSync || nonLoopDrift > 0.05f)
        {
            activeSource.time = nonLoopTime;
        }
    }

    private void ApplyFade(float localTime, float duration, float weight)
    {
        if (activeSource == null) return;
        float fadeIn = 1f;
        if (fadeInDuration > 0f)
        {
            float tIn = Mathf.Clamp01(localTime / fadeInDuration);
            fadeIn = fadeInCurve != null ? fadeInCurve.Evaluate(tIn) : tIn;
        }

        float fadeOut = 1f;
        if (fadeOutDuration > 0f && duration > 0f)
        {
            float tOut = Mathf.Clamp01((duration - localTime) / fadeOutDuration);
            fadeOut = fadeOutCurve != null ? fadeOutCurve.Evaluate(tOut) : tOut;
        }

        activeSource.volume = baseVolume * fadeIn * fadeOut * weight;
    }

    private SoundLibraryEntry ResolveEntry(SoundEntryReference reference)
    {
        if (reference == null) return null;
        if (reference.entry != null) return reference.entry;
        if (string.IsNullOrWhiteSpace(reference.entryName)) return null;
        return SoundManager.Instance != null && SoundManager.Instance.Library != null
            ? SoundManager.Instance.Library.GetEntry(reference.entryName)
            : null;
    }
}

[Serializable]
public class SoundMusicClip : PlayableAsset, ITimelineClipAsset
{
    public SoundEntryReference music = new();
    [Min(0)] public int layerIndex = 0;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    public bool overrideLoop = true;
    public bool loop = true;
    [Min(0f)] public float startOffsetSeconds = 0f;
    [Min(0f)] public float loopStartSeconds = 0f;
    [Min(0f)] public float loopEndSeconds = 0f;
    public bool syncToTimeline = true;
    public bool alignToBeat = true;
    public bool useLoopClipIfPresent = true;

    [Header("Tempo")]
    [Min(0f)] public float bpm = 120f;
    public bool autoDetectBpm = true;
    [Min(1)] public int beatsPerBar = 4;
    [Min(1)] public int barsForBpmFallback = 4;
    [Min(0f)] public float fadeInDuration = 0f;
    [Min(0f)] public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override double duration
    {
        get
        {
            double length = ResolveClipLengthSeconds();
            return length > 0d ? length : base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundMusicBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.music = music;
        behaviour.layerIndex = layerIndex;
        behaviour.volumeMultiplier = volumeMultiplier;
        behaviour.overrideLoop = overrideLoop;
        behaviour.loop = loop;
        behaviour.startOffsetSeconds = startOffsetSeconds;
        behaviour.loopStartSeconds = loopStartSeconds;
        behaviour.loopEndSeconds = loopEndSeconds;
        behaviour.syncToTimeline = syncToTimeline;
        behaviour.alignToBeat = alignToBeat;
        behaviour.useLoopClipIfPresent = useLoopClipIfPresent;
        behaviour.bpm = bpm;
        behaviour.autoDetectBpm = autoDetectBpm;
        behaviour.beatsPerBar = beatsPerBar;
        behaviour.barsForBpmFallback = barsForBpmFallback;
        behaviour.fadeInDuration = fadeInDuration;
        behaviour.fadeOutDuration = fadeOutDuration;
        behaviour.fadeInCurve = fadeInCurve;
        behaviour.fadeOutCurve = fadeOutCurve;
        behaviour.director = owner != null ? owner.GetComponent<PlayableDirector>() : null;
        return playable;
    }

    private double ResolveClipLengthSeconds()
    {
        var entry = music != null ? music.entry : null;
        if (entry is MusicDefinition musicDef)
        {
            var clip = useLoopClipIfPresent && musicDef.loopClip != null ? musicDef.loopClip : musicDef.clip;
            return clip != null ? clip.length : 0d;
        }

        return 0d;
    }
}

public class SoundMusicBehaviour : PlayableBehaviour
{
    public SoundEntryReference music;
    public int layerIndex;
    public float volumeMultiplier = 1f;
    public bool overrideLoop = true;
    public bool loop = true;
    public float startOffsetSeconds = 0f;
    public float loopStartSeconds = 0f;
    public float loopEndSeconds = 0f;
    public bool syncToTimeline = true;
    public bool alignToBeat = true;
    public bool useLoopClipIfPresent = true;
    public float bpm = 120f;
    public bool autoDetectBpm = true;
    public int beatsPerBar = 4;
    public int barsForBpmFallback = 4;
    public float fadeInDuration = 0f;
    public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public PlayableDirector director;

    private AudioSource activeSource;
    private bool fired;
    private float baseVolume;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired) return;
        fired = true;

        if (SoundManager.Instance == null) return;

        float delaySeconds = 0f;
        if (alignToBeat)
        {
            float resolvedBpm = ResolveBpm();
            if (resolvedBpm > 0f)
            {
                float secondsPerBeat = 60f / resolvedBpm;
                double currentTime = director != null ? director.time : 0d;
                double nextBeat = Math.Ceiling(currentTime / secondsPerBeat) * secondsPerBeat;
                delaySeconds = Mathf.Max(0f, (float)(nextBeat - currentTime));
            }
        }

        if (delaySeconds > 0f)
        {
            SoundTimelineRuntime.Instance.Run(PlayDelayed(delaySeconds));
        }
        else
        {
            PlayMusic();
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (activeSource != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopMusicLayer(layerIndex, 0.1f);
        }

        activeSource = null;
        fired = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (activeSource == null) return;

        float localTime = (float)playable.GetTime();
        float duration = (float)playable.GetDuration();
        ApplyLoop(localTime);
        ApplyFade(localTime, duration, info.weight);
    }

    private System.Collections.IEnumerator PlayDelayed(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        PlayMusic();
    }

    private void PlayMusic()
    {
        bool? loopOverride = overrideLoop ? loop : (bool?)null;
        var entry = ResolveEntry(music);
        if (entry is MusicDefinition musicDef)
        {
            AudioClip clip = SelectClip(musicDef);
            if (clip != null)
            {
                activeSource = SoundManager.Instance.PlayMusicLayerClip(layerIndex, musicDef, clip, volumeMultiplier, loopOverride);
            }
            else
            {
                activeSource = SoundManager.Instance.PlayMusicLayer(layerIndex, musicDef, volumeMultiplier, loopOverride);
            }
            if (activeSource != null)
            {
                baseVolume = activeSource.volume;
                ConfigureStartAndLoop();
            }
            return;
        }

        activeSource = SoundManager.Instance.PlayMusicLayer(layerIndex, music.entryName, volumeMultiplier, loopOverride);
        if (activeSource != null)
        {
            baseVolume = activeSource.volume;
            ConfigureStartAndLoop();
        }
    }

    private float ResolveBpm()
    {
        if (bpm > 0f) return bpm;
        if (!autoDetectBpm) return 120f;

        var entry = ResolveEntry(music) as MusicDefinition;
        AudioClip clip = entry != null ? (entry.loopClip != null ? entry.loopClip : entry.clip) : null;
        if (clip != null && clip.length > 0f)
        {
            float beats = Mathf.Max(1, beatsPerBar) * Mathf.Max(1, barsForBpmFallback);
            return (beats / clip.length) * 60f;
        }

        return 120f;
    }

    private SoundLibraryEntry ResolveEntry(SoundEntryReference reference)
    {
        if (reference == null) return null;
        if (reference.entry != null) return reference.entry;
        if (string.IsNullOrWhiteSpace(reference.entryName)) return null;
        return SoundManager.Instance != null && SoundManager.Instance.Library != null
            ? SoundManager.Instance.Library.GetEntry(reference.entryName)
            : null;
    }

    private AudioClip SelectClip(MusicDefinition definition)
    {
        if (definition == null) return null;
        if (useLoopClipIfPresent && definition.loopClip != null) return definition.loopClip;
        return definition.clip;
    }

    private void ConfigureStartAndLoop()
    {
        if (activeSource == null || activeSource.clip == null) return;

        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, activeSource.clip.length - 0.01f);
        if (startTime > 0f)
        {
            activeSource.time = startTime;
        }

        if (loop && loopEndSeconds > loopStartSeconds)
        {
            activeSource.loop = false;
            activeSource.time = Mathf.Clamp(loopStartSeconds, 0f, activeSource.clip.length - 0.01f);
        }
    }

    private void ApplyLoop(float localTime)
    {
        if (!syncToTimeline || activeSource == null || activeSource.clip == null) return;

        float clipLength = activeSource.clip.length;
        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clipLength - 0.01f));
        float loopStart = Mathf.Clamp(loopStartSeconds, 0f, Mathf.Max(0f, clipLength - 0.01f));
        float loopEnd = loopEndSeconds > loopStart ? Mathf.Min(loopEndSeconds, clipLength) : clipLength;

        if (loop)
        {
            float actualStart = loopEndSeconds > loopStartSeconds ? loopStart : startTime;
            float actualEnd = loopEndSeconds > loopStartSeconds ? loopEnd : clipLength;
            float length = Mathf.Max(0.01f, actualEnd - actualStart);
            float t = localTime % length;
            float desired = actualStart + t;
            if (Mathf.Abs(activeSource.time - desired) > 0.01f)
            {
                activeSource.time = desired;
            }
            return;
        }

        float nonLoopTime = Mathf.Clamp(startTime + localTime, 0f, clipLength);
        if (Mathf.Abs(activeSource.time - nonLoopTime) > 0.01f)
        {
            activeSource.time = nonLoopTime;
        }
    }

    private void ApplyFade(float localTime, float duration, float weight)
    {
        float fadeIn = 1f;
        if (fadeInDuration > 0f)
        {
            float tIn = Mathf.Clamp01(localTime / fadeInDuration);
            fadeIn = fadeInCurve != null ? fadeInCurve.Evaluate(tIn) : tIn;
        }

        float fadeOut = 1f;
        if (fadeOutDuration > 0f && duration > 0f)
        {
            float tOut = Mathf.Clamp01((duration - localTime) / fadeOutDuration);
            fadeOut = fadeOutCurve != null ? fadeOutCurve.Evaluate(tOut) : tOut;
        }

        activeSource.volume = baseVolume * fadeIn * fadeOut * weight;
    }
}

[Serializable]
public class SoundNarrationClip : PlayableAsset, ITimelineClipAsset
{
    public SoundEntryReference narration = new();
    public ExposedReference<Transform> emitter;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
    [Min(0f)] public float startOffsetSeconds = 0f;
    public bool syncToTimeline = true;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
    [Min(0f)] public float stopFadeSeconds = 0.1f;
    [Min(0f)] public float fadeInDuration = 0f;
    [Min(0f)] public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override double duration
    {
        get
        {
            double length = ResolveClipLengthSeconds();
            return length > 0d ? length : base.duration;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundNarrationBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.narration = narration;
        behaviour.emitter = emitter.Resolve(graph.GetResolver());
        behaviour.volumeMultiplier = volumeMultiplier;
        behaviour.startOffsetSeconds = startOffsetSeconds;
        behaviour.syncToTimeline = syncToTimeline;
        behaviour.followEmitter = followEmitter;
        behaviour.useEmitterPosition = useEmitterPosition;
        behaviour.positionOffset = positionOffset;
        behaviour.stopFadeSeconds = stopFadeSeconds;
        behaviour.fadeInDuration = fadeInDuration;
        behaviour.fadeOutDuration = fadeOutDuration;
        behaviour.fadeInCurve = fadeInCurve;
        behaviour.fadeOutCurve = fadeOutCurve;
        return playable;
    }

    private double ResolveClipLengthSeconds()
    {
        var entry = narration != null ? narration.entry : null;
        if (entry is NarrationPlayable playable)
        {
            return playable.clip != null ? playable.clip.length : 0d;
        }

        if (entry is NarrationVariantGroup variantGroup && variantGroup.variants != null)
        {
            for (int i = 0; i < variantGroup.variants.Count; i++)
            {
                var variant = variantGroup.variants[i];
                if (variant != null && variant.clip != null)
                {
                    return variant.clip.length;
                }
            }
        }

        if (entry is NarrationGroup group && group.entries != null)
        {
            for (int i = 0; i < group.entries.Count; i++)
            {
                if (group.entries[i] is NarrationPlayable candidate && candidate.clip != null)
                {
                    return candidate.clip.length;
                }
            }
        }

        return 0d;
    }
}

public class SoundNarrationBehaviour : PlayableBehaviour
{
    public SoundEntryReference narration;
    public Transform emitter;
    public float volumeMultiplier = 1f;
    public float startOffsetSeconds = 0f;
    public bool syncToTimeline = true;
    public bool followEmitter;
    public bool useEmitterPosition = true;
    public Vector3 positionOffset = Vector3.zero;
    public float stopFadeSeconds = 0.1f;
    public float fadeInDuration = 0f;
    public float fadeOutDuration = 0f;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private AudioSource activeSource;
    private bool fired;
    private float baseVolume;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired) return;
        fired = true;

        if (SoundManager.Instance == null) return;

        Transform emit = emitter != null ? emitter : SoundManager.Instance.transform;
        Vector3 position = useEmitterPosition ? emit.position + positionOffset : positionOffset;
        Transform follow = followEmitter ? emit : null;

        var entry = ResolveEntry(narration);
        activeSource = entry != null
            ? SoundManager.Instance.PlayNarration(entry, position, follow, volumeMultiplier)
            : SoundManager.Instance.PlayNarration(narration.entryName, position, follow, volumeMultiplier);

        if (activeSource != null)
        {
            baseVolume = activeSource.volume;
            ConfigureStart();
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (activeSource != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.StopSfx(activeSource, stopFadeSeconds);
        }

        activeSource = null;
        fired = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (activeSource == null) return;

        float localTime = (float)playable.GetTime();
        float duration = (float)playable.GetDuration();
        ApplySync(localTime);
        ApplyFade(localTime, duration, info.weight);
    }

    private SoundLibraryEntry ResolveEntry(SoundEntryReference reference)
    {
        if (reference == null) return null;
        if (reference.entry != null) return reference.entry;
        if (string.IsNullOrWhiteSpace(reference.entryName)) return null;
        return SoundManager.Instance != null && SoundManager.Instance.Library != null
            ? SoundManager.Instance.Library.GetEntry(reference.entryName)
            : null;
    }

    private void ConfigureStart()
    {
        if (activeSource == null || activeSource.clip == null) return;

        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, activeSource.clip.length - 0.01f);
        if (startTime > 0f)
        {
            activeSource.time = startTime;
        }
    }

    private void ApplySync(float localTime)
    {
        if (!syncToTimeline || activeSource == null || activeSource.clip == null) return;

        float clipLength = activeSource.clip.length;
        float startTime = Mathf.Clamp(startOffsetSeconds, 0f, Mathf.Max(0f, clipLength - 0.01f));
        float desired = Mathf.Clamp(startTime + localTime, 0f, clipLength);
        if (Mathf.Abs(activeSource.time - desired) > 0.01f)
        {
            activeSource.time = desired;
        }
    }

    private void ApplyFade(float localTime, float duration, float weight)
    {
        float fadeIn = 1f;
        if (fadeInDuration > 0f)
        {
            float tIn = Mathf.Clamp01(localTime / fadeInDuration);
            fadeIn = fadeInCurve != null ? fadeInCurve.Evaluate(tIn) : tIn;
        }

        float fadeOut = 1f;
        if (fadeOutDuration > 0f && duration > 0f)
        {
            float tOut = Mathf.Clamp01((duration - localTime) / fadeOutDuration);
            fadeOut = fadeOutCurve != null ? fadeOutCurve.Evaluate(tOut) : tOut;
        }

        activeSource.volume = baseVolume * fadeIn * fadeOut * weight;
    }
}

[Serializable]
public class SoundMusicLayerFadeClip : PlayableAsset, ITimelineClipAsset
{
    [Min(0)] public int layerIndex = 0;
    [Range(0f, 1f)] public float minVolume = 0f;
    [Range(0f, 1f)] public float maxVolume = 1f;
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SoundMusicLayerFadeBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.layerIndex = layerIndex;
        behaviour.minVolume = minVolume;
        behaviour.maxVolume = maxVolume;
        behaviour.curve = curve;
        return playable;
    }
}

public class SoundMusicLayerFadeBehaviour : PlayableBehaviour
{
    public int layerIndex;
    public float minVolume = 0f;
    public float maxVolume = 1f;
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (SoundManager.Instance == null) return;
        if (!SoundManager.Instance.TryGetMusicLayerSource(layerIndex, out var source) || source == null) return;

        float localTime = (float)playable.GetTime();
        float duration = Mathf.Max(0.0001f, (float)playable.GetDuration());
        float t = Mathf.Clamp01(localTime / duration);
        float curveValue = curve != null ? curve.Evaluate(t) : t;
        float value = Mathf.Lerp(minVolume, maxVolume, curveValue) * info.weight;
        source.volume = value;
    }
}

[Serializable]
public class SoundBeatMarker : Marker, INotification
{
    public string label;

    public PropertyName id => new PropertyName("SoundBeatMarker");
}

public class SoundBeatReceiver : MonoBehaviour, INotificationReceiver
{
    public event Action<string> OnBeatMarker;

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is SoundBeatMarker marker)
        {
            OnBeatMarker?.Invoke(marker.label);
        }
    }
}
