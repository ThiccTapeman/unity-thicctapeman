using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[ExecuteAlways]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Library")]
    [SerializeField] private SoundLibrary library;
    public SoundLibrary Library => library;

    [Header("SFX")]
    [SerializeField] private int sfxPoolSize = 24;
    [SerializeField] private Transform sfxRoot;
    [SerializeField] private AudioMixerGroup defaultSfxMixer;

    [Header("Music")]
    [SerializeField] private Transform musicRoot;
    [SerializeField, Min(1)] private int musicLayerCount = 3;
    [SerializeField] private AudioMixerGroup defaultMusicMixer;

    [Header("Narration")]
    [SerializeField] private int narrationPoolSize = 2;
    [SerializeField] private Transform narrationRoot;
    [SerializeField] private AudioMixerGroup defaultNarrationMixer;

    [Header("Lifecycle")]
    [SerializeField] private bool persistAcrossScenes = true;

    private readonly List<AudioSource> sfxPool = new();
    private readonly Dictionary<AudioSource, int> sfxTokens = new();
    private readonly List<AudioSource> narrationPool = new();
    private readonly Dictionary<AudioSource, Coroutine> narrationRoutines = new();
    private readonly Dictionary<string, int> mixerParamTokens = new();
    private int sfxNextIndex;
    private AudioSource[] musicSources;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            InitializeIfNeeded();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public AudioSource PlaySfx(string sfxName, Vector3 position, Transform follow = null, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (library == null)
        {
            Debug.LogWarning("SoundManager: No sound library assigned.");
            return null;
        }

        var sfx = library.GetSfx(sfxName);
        SfxVariantGroup variantGroup = null;
        SfxVariant forcedVariant = null;
        if (sfx == null)
        {
            variantGroup = library.GetSfxVariantGroup(sfxName);
            if (variantGroup == null && library.TryGetSfxVariantByPath(sfxName, out var foundGroup, out var foundVariant))
            {
                variantGroup = foundGroup;
                forcedVariant = foundVariant;
            }
        }

        if (sfx == null && variantGroup == null)
        {
            Debug.LogWarning($"SoundManager: Missing SFX '{sfxName}'.");
            return null;
        }

        var source = GetSfxSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No SFX sources available.");
            return null;
        }

        CancelPendingFade(source);

        source.transform.position = position;
        var followComp = source.GetComponent<SoundFollow>();
        if (followComp != null)
        {
            if (follow != null) followComp.SetTarget(follow);
            else followComp.Clear();
        }

        AudioClip clip;
        float baseVolume;
        Vector2 pitchRange;
        bool loop;
        float spatialBlend;
        float minDistance;
        float maxDistance;
        AudioMixerGroup mixerGroup;
        float variantVolume = 1f;

        if (sfx != null)
        {
            clip = sfx.clip;
            baseVolume = sfx.volume;
            pitchRange = sfx.pitchRange;
            loop = sfx.loop;
            spatialBlend = sfx.spatialBlend;
            minDistance = sfx.minDistance;
            maxDistance = sfx.maxDistance;
            mixerGroup = sfx.mixerGroup;
        }
        else
        {
            var chosenVariant = forcedVariant ?? PickVariant(variantGroup);
            clip = chosenVariant != null ? chosenVariant.clip : null;
            baseVolume = variantGroup.volume;
            pitchRange = chosenVariant != null ? chosenVariant.pitchRange : variantGroup.pitchRange;
            loop = variantGroup.loop;
            spatialBlend = variantGroup.spatialBlend;
            minDistance = variantGroup.minDistance;
            maxDistance = variantGroup.maxDistance;
            mixerGroup = variantGroup.mixerGroup;
            if (chosenVariant != null)
            {
                variantVolume = chosenVariant.volume;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for SFX '{sfxName}'.");
            return null;
        }

        source.clip = clip;
        source.volume = Mathf.Clamp01(baseVolume * variantVolume * volumeMultiplier);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.loop = loopOverride ?? loop;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.outputAudioMixerGroup = mixerGroup != null ? mixerGroup : defaultSfxMixer;
        source.Play();
        return source;
    }

    public AudioSource PlaySfx(SoundLibraryEntry entry, Vector3 position, Transform follow = null, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (entry == null)
        {
            Debug.LogWarning("SoundManager: Missing SFX entry.");
            return null;
        }

        if (entry is SfxDefinition sfx)
        {
            return PlaySfx(sfx, position, follow, volumeMultiplier, loopOverride);
        }

        if (entry is SfxVariantGroup group)
        {
            return PlaySfx(group, position, follow, volumeMultiplier, loopOverride);
        }

        if (entry is SfxVariant variant)
        {
            return PlaySfx(variant, position, follow, volumeMultiplier, loopOverride);
        }

        Debug.LogWarning($"SoundManager: Entry '{entry.name}' is not a SFX type.");
        return null;
    }

    public AudioSource PlaySfx(SfxDefinition sfx, Vector3 position, Transform follow = null, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (sfx == null)
        {
            Debug.LogWarning("SoundManager: Missing SFX definition.");
            return null;
        }

        if (sfx.clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for SFX '{sfx.name}'.");
            return null;
        }

        var source = GetSfxSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No SFX sources available.");
            return null;
        }

        CancelPendingFade(source);
        SetupFollowAndPosition(source, position, follow);

        source.clip = sfx.clip;
        source.volume = Mathf.Clamp01(sfx.volume * volumeMultiplier);
        source.pitch = Random.Range(sfx.pitchRange.x, sfx.pitchRange.y);
        source.loop = loopOverride ?? sfx.loop;
        source.spatialBlend = sfx.spatialBlend;
        source.minDistance = sfx.minDistance;
        source.maxDistance = sfx.maxDistance;
        source.outputAudioMixerGroup = sfx.mixerGroup != null ? sfx.mixerGroup : defaultSfxMixer;
        source.Play();
        return source;
    }

    public AudioSource PlaySfx(SfxVariantGroup group, Vector3 position, Transform follow = null, float volumeMultiplier = 1f, bool? loopOverride = null, SfxVariant forcedVariant = null)
    {
        if (group == null)
        {
            Debug.LogWarning("SoundManager: Missing SFX variant group.");
            return null;
        }

        var source = GetSfxSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No SFX sources available.");
            return null;
        }

        var chosenVariant = forcedVariant ?? PickVariant(group);
        var clip = chosenVariant != null ? chosenVariant.clip : null;
        if (clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for SFX group '{group.name}'.");
            return null;
        }

        CancelPendingFade(source);
        SetupFollowAndPosition(source, position, follow);

        float variantVolume = chosenVariant != null ? chosenVariant.volume : 1f;
        source.clip = clip;
        source.volume = Mathf.Clamp01(group.volume * variantVolume * volumeMultiplier);
        source.pitch = Random.Range(group.pitchRange.x, group.pitchRange.y);
        source.loop = loopOverride ?? group.loop;
        source.spatialBlend = group.spatialBlend;
        source.minDistance = group.minDistance;
        source.maxDistance = group.maxDistance;
        source.outputAudioMixerGroup = group.mixerGroup != null ? group.mixerGroup : defaultSfxMixer;
        source.Play();
        return source;
    }

    public AudioSource PlaySfx(SfxVariant variant, Vector3 position, Transform follow = null, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (variant == null)
        {
            Debug.LogWarning("SoundManager: Missing SFX variant.");
            return null;
        }

        if (variant.clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for SFX variant '{variant.name}'.");
            return null;
        }

        var source = GetSfxSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No SFX sources available.");
            return null;
        }

        CancelPendingFade(source);
        SetupFollowAndPosition(source, position, follow);

        source.clip = variant.clip;
        source.volume = Mathf.Clamp01(variant.volume * volumeMultiplier);
        source.pitch = Random.Range(variant.pitchRange.x, variant.pitchRange.y);
        source.loop = loopOverride ?? false;
        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 25f;
        source.outputAudioMixerGroup = defaultSfxMixer;
        source.Play();
        return source;
    }

    public void StopSfx(AudioSource source, float fadeSeconds = 0f)
    {
        if (source == null) return;

        if (fadeSeconds <= 0f)
        {
            source.Stop();
            ClearFollow(source);
            return;
        }

        int token = BumpToken(source);
        StartCoroutine(FadeOutSfx(source, token, fadeSeconds));
    }

    public AudioSource PlayMusic(string musicName, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        return PlayMusicLayer(0, musicName, volumeMultiplier, loopOverride);
    }

    public AudioSource PlayMusic(SoundLibraryEntry entry, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (entry == null)
        {
            Debug.LogWarning("SoundManager: Missing music entry.");
            return null;
        }

        if (entry is MusicDefinition music)
        {
            return PlayMusic(music, volumeMultiplier, loopOverride);
        }

        Debug.LogWarning($"SoundManager: Entry '{entry.name}' is not a music type.");
        return null;
    }

    public AudioSource PlayMusic(MusicDefinition music, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        return PlayMusicLayer(0, music, volumeMultiplier, loopOverride);
    }

    public void StopMusic(float fadeSeconds = 0f)
    {
        if (musicSources == null) return;
        for (int i = 0; i < musicSources.Length; i++)
        {
            StopMusicLayer(i, fadeSeconds);
        }
    }

    public AudioSource PlayMusicLayer(int layerIndex, string musicName, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (library == null)
        {
            Debug.LogWarning("SoundManager: No sound library assigned.");
            return null;
        }

        var music = library.GetMusic(musicName);
        if (music == null || music.clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing music '{musicName}'.");
            return null;
        }

        EnsureMusicSources();
        if (!TryGetMusicSource(layerIndex, out var source))
        {
            Debug.LogWarning($"SoundManager: Invalid music layer {layerIndex}.");
            return null;
        }

        CancelPendingFade(source);
        int token = BumpToken(source);
        source.clip = music.clip;
        source.volume = Mathf.Clamp01(music.volume * volumeMultiplier);
        source.pitch = Random.Range(music.pitchRange.x, music.pitchRange.y);
        source.loop = loopOverride ?? music.loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = music.mixerGroup != null ? music.mixerGroup : defaultMusicMixer;
        if (music.introClip != null && music.loopClip != null)
        {
            source.loop = false;
            source.clip = music.introClip;
            source.Play();
            StartCoroutine(PlayMusicIntroThenLoop(source, token, music.loopClip, source.pitch, source.volume, source.outputAudioMixerGroup, loopOverride ?? true));
        }
        else
        {
            source.Play();
        }
        return source;
    }

    public AudioSource PlayMusicLayer(int layerIndex, MusicDefinition music, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (music == null)
        {
            Debug.LogWarning("SoundManager: Missing music definition.");
            return null;
        }

        if (music.clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for music '{music.name}'.");
            return null;
        }

        EnsureMusicSources();
        if (!TryGetMusicSource(layerIndex, out var source))
        {
            Debug.LogWarning($"SoundManager: Invalid music layer {layerIndex}.");
            return null;
        }

        CancelPendingFade(source);
        int token = BumpToken(source);
        source.clip = music.clip;
        source.volume = Mathf.Clamp01(music.volume * volumeMultiplier);
        source.pitch = Random.Range(music.pitchRange.x, music.pitchRange.y);
        source.loop = loopOverride ?? music.loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = music.mixerGroup != null ? music.mixerGroup : defaultMusicMixer;
        if (music.introClip != null && music.loopClip != null)
        {
            source.loop = false;
            source.clip = music.introClip;
            source.Play();
            StartCoroutine(PlayMusicIntroThenLoop(source, token, music.loopClip, source.pitch, source.volume, source.outputAudioMixerGroup, loopOverride ?? true));
        }
        else
        {
            source.Play();
        }
        return source;
    }

    public AudioSource PlayMusicLayerClip(int layerIndex, MusicDefinition music, AudioClip clip, float volumeMultiplier = 1f, bool? loopOverride = null)
    {
        if (music == null)
        {
            Debug.LogWarning("SoundManager: Missing music definition.");
            return null;
        }

        if (clip == null)
        {
            Debug.LogWarning($"SoundManager: Missing clip for music '{music.name}'.");
            return null;
        }

        EnsureMusicSources();
        if (!TryGetMusicSource(layerIndex, out var source))
        {
            Debug.LogWarning($"SoundManager: Invalid music layer {layerIndex}.");
            return null;
        }

        CancelPendingFade(source);
        source.clip = clip;
        source.volume = Mathf.Clamp01(music.volume * volumeMultiplier);
        source.pitch = Random.Range(music.pitchRange.x, music.pitchRange.y);
        source.loop = loopOverride ?? music.loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = music.mixerGroup != null ? music.mixerGroup : defaultMusicMixer;
        source.Play();
        return source;
    }

    public AudioSource PlayNarration(string narrationName, Vector3 position, Transform follow = null, float volumeMultiplier = 1f)
    {
        if (library == null)
        {
            Debug.LogWarning("SoundManager: No sound library assigned.");
            return null;
        }

        var group = library.GetNarrationGroup(narrationName);
        NarrationPlayable forcedEntry = null;
        if (group == null && library.TryGetNarrationEntryByPath(narrationName, out var foundGroup, out var foundEntry))
        {
            group = foundGroup;
            forcedEntry = foundEntry;
        }

        if (group == null)
        {
            Debug.LogWarning($"SoundManager: Missing narration '{narrationName}'.");
            return null;
        }

        if (group.entries == null || group.entries.Count == 0)
        {
            Debug.LogWarning($"SoundManager: Narration group '{narrationName}' has no clips.");
            return null;
        }

        var source = GetNarrationSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No narration sources available.");
            return null;
        }

        CancelPendingNarration(source);

        source.transform.position = position;
        var followComp = source.GetComponent<SoundFollow>();
        if (followComp != null)
        {
            if (follow != null) followComp.SetTarget(follow);
            else followComp.Clear();
        }

        int token = BumpToken(source);
        var routine = StartCoroutine(PlayNarrationSequence(source, token, group, forcedEntry, volumeMultiplier));
        narrationRoutines[source] = routine;
        return source;
    }

    public AudioSource PlayNarration(SoundLibraryEntry entry, Vector3 position, Transform follow = null, float volumeMultiplier = 1f)
    {
        if (entry == null)
        {
            Debug.LogWarning("SoundManager: Missing narration entry.");
            return null;
        }

        if (entry is NarrationGroup group)
        {
            return PlayNarration(group, position, follow, volumeMultiplier);
        }

        if (entry is NarrationPlayable playable)
        {
            return PlayNarration(playable, position, follow, volumeMultiplier);
        }

        Debug.LogWarning($"SoundManager: Entry '{entry.name}' is not a narration type.");
        return null;
    }

    public AudioSource PlayNarration(NarrationGroup group, Vector3 position, Transform follow = null, float volumeMultiplier = 1f)
    {
        if (group == null)
        {
            Debug.LogWarning("SoundManager: Missing narration group.");
            return null;
        }

        if (group.entries == null || group.entries.Count == 0)
        {
            Debug.LogWarning($"SoundManager: Narration group '{group.name}' has no clips.");
            return null;
        }

        var source = GetNarrationSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No narration sources available.");
            return null;
        }

        CancelPendingNarration(source);
        SetupFollowAndPosition(source, position, follow);

        int token = BumpToken(source);
        var routine = StartCoroutine(PlayNarrationSequence(source, token, group, null, volumeMultiplier));
        narrationRoutines[source] = routine;
        return source;
    }

    public AudioSource PlayNarration(NarrationPlayable playable, Vector3 position, Transform follow = null, float volumeMultiplier = 1f)
    {
        if (playable == null || playable.clip == null)
        {
            Debug.LogWarning("SoundManager: Missing narration playable.");
            return null;
        }

        var source = GetNarrationSource();
        if (source == null)
        {
            Debug.LogWarning("SoundManager: No narration sources available.");
            return null;
        }

        CancelPendingNarration(source);
        SetupFollowAndPosition(source, position, follow);

        source.clip = playable.clip;
        source.volume = Mathf.Clamp01(playable.volume * volumeMultiplier);
        source.pitch = 1f;
        source.loop = false;
        source.spatialBlend = 0f;
        source.minDistance = 1f;
        source.maxDistance = 25f;
        source.outputAudioMixerGroup = defaultNarrationMixer;
        source.Play();
        return source;
    }

    public void StopMusicLayer(int layerIndex, float fadeSeconds = 0f)
    {
        if (musicSources == null) return;
        if (!TryGetMusicSource(layerIndex, out var source)) return;
        StopSfx(source, fadeSeconds);
    }

    public void FadeMusicLayerVolume(int layerIndex, float targetVolume, float fadeSeconds)
    {
        if (musicSources == null) return;
        if (!TryGetMusicSource(layerIndex, out var source)) return;
        targetVolume = Mathf.Clamp01(targetVolume);

        int token = BumpToken(source);
        if (fadeSeconds <= 0f)
        {
            source.volume = targetVolume;
            return;
        }

        StartCoroutine(FadeMusicVolume(source, token, targetVolume, fadeSeconds));
    }

    public void FadeMixerFilter(AudioMixerGroup group, string exposedParam, float targetValue, float fadeSeconds)
    {
        if (group == null)
        {
            Debug.LogWarning("SoundManager: Missing mixer group for fade.");
            return;
        }

        FadeMixerFilter(group.audioMixer, exposedParam, targetValue, fadeSeconds);
    }

    public void FadeMixerFilter(AudioMixer mixer, string exposedParam, float targetValue, float fadeSeconds)
    {
        if (mixer == null)
        {
            Debug.LogWarning("SoundManager: Missing mixer for fade.");
            return;
        }
        if (string.IsNullOrWhiteSpace(exposedParam))
        {
            Debug.LogWarning("SoundManager: Missing exposed param name for fade.");
            return;
        }

        if (!mixer.GetFloat(exposedParam, out float startValue))
        {
            startValue = targetValue;
        }

        int token = BumpMixerToken(mixer, exposedParam);
        if (fadeSeconds <= 0f)
        {
            mixer.SetFloat(exposedParam, targetValue);
            return;
        }

        StartCoroutine(FadeMixerParam(mixer, exposedParam, token, startValue, targetValue, fadeSeconds));
    }

    private void EnsureRoots()
    {
        if (sfxRoot == null)
        {
            var sfxRootObj = new GameObject("SFX");
            sfxRootObj.transform.SetParent(transform);
            sfxRoot = sfxRootObj.transform;
        }

        if (musicRoot == null)
        {
            var musicRootObj = new GameObject("Music");
            musicRootObj.transform.SetParent(transform);
            musicRoot = musicRootObj.transform;
        }

        if (narrationRoot == null)
        {
            var narrationRootObj = new GameObject("Narration");
            narrationRootObj.transform.SetParent(transform);
            narrationRoot = narrationRootObj.transform;
        }
    }

    private void InitializeIfNeeded()
    {
        if (library != null)
        {
            library.BuildLookup();
        }

        EnsureRoots();
        BuildSfxPool();
        BuildNarrationPool();
        EnsureMusicSources();
    }

    private void BuildSfxPool()
    {
        if (sfxPool.Count > 0) return;

        int count = Mathf.Max(1, sfxPoolSize);
        for (int i = 0; i < count; i++)
        {
            var obj = new GameObject($"SFX Source {i + 1:00}");
            obj.transform.SetParent(sfxRoot);
            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            obj.AddComponent<SoundFollow>().enabled = false;
            sfxPool.Add(source);
            sfxTokens[source] = 0;
        }
    }

    private void BuildNarrationPool()
    {
        if (narrationPool.Count > 0) return;

        int count = Mathf.Max(1, narrationPoolSize);
        for (int i = 0; i < count; i++)
        {
            var obj = new GameObject($"Narration Source {i + 1:00}");
            obj.transform.SetParent(narrationRoot);
            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            obj.AddComponent<SoundFollow>().enabled = false;
            narrationPool.Add(source);
            sfxTokens[source] = 0;
        }
    }

    private void EnsureMusicSources()
    {
        if (musicSources != null && musicSources.Length > 0) return;

        int count = Mathf.Max(1, musicLayerCount);
        musicSources = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            var obj = new GameObject($"Music Layer {i + 1:00}");
            obj.transform.SetParent(musicRoot);
            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            musicSources[i] = source;
            sfxTokens[source] = 0;
        }
    }

    private bool TryGetMusicSource(int layerIndex, out AudioSource source)
    {
        source = null;
        if (musicSources == null) return false;
        if (layerIndex < 0 || layerIndex >= musicSources.Length) return false;
        source = musicSources[layerIndex];
        return source != null;
    }

    public bool TryGetMusicLayerSource(int layerIndex, out AudioSource source)
    {
        return TryGetMusicSource(layerIndex, out source);
    }

    private AudioSource GetSfxSource()
    {
        for (int i = 0; i < sfxPool.Count; i++)
        {
            if (!sfxPool[i].isPlaying)
            {
                return sfxPool[i];
            }
        }

        if (sfxPool.Count == 0) return null;
        var source = sfxPool[sfxNextIndex % sfxPool.Count];
        sfxNextIndex++;
        source.Stop();
        ClearFollow(source);
        return source;
    }

    private AudioSource GetNarrationSource()
    {
        for (int i = 0; i < narrationPool.Count; i++)
        {
            var source = narrationPool[i];
            if (source == null) continue;
            if (source.isPlaying) continue;
            if (narrationRoutines.ContainsKey(source)) continue;
            return source;
        }

        if (narrationPool.Count == 0) return null;
        var fallback = narrationPool[0];
        CancelPendingNarration(fallback);
        fallback.Stop();
        ClearFollow(fallback);
        return fallback;
    }

    private void ClearFollow(AudioSource source)
    {
        var followComp = source.GetComponent<SoundFollow>();
        if (followComp != null)
        {
            followComp.Clear();
        }
    }

    private void CancelPendingFade(AudioSource source)
    {
        if (source == null) return;
        BumpToken(source);
    }

    private void CancelPendingNarration(AudioSource source)
    {
        if (source == null) return;
        if (narrationRoutines.TryGetValue(source, out var routine) && routine != null)
        {
            StopCoroutine(routine);
        }
        narrationRoutines.Remove(source);
        BumpToken(source);
    }

    private int BumpToken(AudioSource source)
    {
        if (source == null) return 0;
        if (!sfxTokens.TryGetValue(source, out var token))
        {
            token = 0;
        }
        token++;
        sfxTokens[source] = token;
        return token;
    }

    private int BumpMixerToken(AudioMixer mixer, string param)
    {
        if (mixer == null) return 0;
        string key = GetMixerTokenKey(mixer, param);
        if (!mixerParamTokens.TryGetValue(key, out int token))
        {
            token = 0;
        }
        token++;
        mixerParamTokens[key] = token;
        return token;
    }

    private IEnumerator FadeOutSfx(AudioSource source, int token, float duration)
    {
        if (source == null) yield break;
        float start = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsTokenValid(source, token)) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        if (!IsTokenValid(source, token)) yield break;
        source.Stop();
        ClearFollow(source);
    }

    private IEnumerator FadeMusicVolume(AudioSource source, int token, float targetVolume, float duration)
    {
        if (source == null) yield break;
        float start = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsTokenValid(source, token)) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(start, targetVolume, t);
            yield return null;
        }

        if (!IsTokenValid(source, token)) yield break;
        source.volume = targetVolume;
    }

    private IEnumerator FadeMixerParam(AudioMixer mixer, string param, int token, float start, float target, float duration)
    {
        if (mixer == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsMixerTokenValid(mixer, param, token)) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float value = Mathf.Lerp(start, target, t);
            mixer.SetFloat(param, value);
            yield return null;
        }

        if (!IsMixerTokenValid(mixer, param, token)) yield break;
        mixer.SetFloat(param, target);
    }

    private IEnumerator PlayMusicIntroThenLoop(AudioSource source, int token, AudioClip loopClip, float pitch, float volume, AudioMixerGroup mixerGroup, bool loop)
    {
        if (source == null || loopClip == null) yield break;
        float duration = source.clip != null ? source.clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)) : 0f;
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        if (!IsTokenValid(source, token)) yield break;
        source.clip = loopClip;
        source.pitch = pitch;
        source.volume = volume;
        source.outputAudioMixerGroup = mixerGroup;
        source.loop = loop;
        source.Play();
    }

    private IEnumerator PlayNarrationSequence(AudioSource source, int token, NarrationGroup group, NarrationPlayable forcedEntry, float volumeMultiplier)
    {
        if (source == null || group == null) yield break;

        List<SoundLibraryEntry> entries = forcedEntry != null
            ? new List<SoundLibraryEntry> { forcedEntry }
            : group.entries;
        if (entries == null || entries.Count == 0) yield break;

        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!IsTokenValid(source, token)) yield break;
                var entry = entries[i];
                if (entry == null) continue;

                var playable = GetNarrationPlayable(entry);
                if (playable == null || playable.clip == null) continue;

                float wait = Mathf.Max(0f, playable.waitBeforeStart);
                if (wait > 0f)
                {
                    yield return new WaitForSeconds(wait);
                }

                if (!IsTokenValid(source, token)) yield break;

                source.clip = playable.clip;
                source.volume = Mathf.Clamp01(group.volume * playable.volume * volumeMultiplier);
                source.pitch = Random.Range(group.pitchRange.x, group.pitchRange.y);
                source.loop = false;
                source.spatialBlend = group.spatialBlend;
                source.minDistance = group.minDistance;
                source.maxDistance = group.maxDistance;
                source.outputAudioMixerGroup = group.mixerGroup != null ? group.mixerGroup : defaultNarrationMixer;
                source.Play();

                float duration = source.clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
                if (duration > 0f)
                {
                    yield return new WaitForSeconds(duration);
                }
            }

            if (!IsTokenValid(source, token)) yield break;
            source.Stop();
            ClearFollow(source);
        }
        finally
        {
            narrationRoutines.Remove(source);
        }
    }

    private void SetupFollowAndPosition(AudioSource source, Vector3 position, Transform follow)
    {
        if (source == null) return;
        source.transform.position = position;
        var followComp = source.GetComponent<SoundFollow>();
        if (followComp != null)
        {
            if (follow != null) followComp.SetTarget(follow);
            else followComp.Clear();
        }
    }

    private static NarrationPlayable GetNarrationPlayable(SoundLibraryEntry entry)
    {
        if (entry is NarrationPlayable playable)
        {
            return playable;
        }

        if (entry is NarrationVariantGroup group)
        {
            var variant = PickNarrationVariant(group);
            return variant;
        }

        return null;
    }

    private static NarrationVariant PickNarrationVariant(NarrationVariantGroup group)
    {
        if (group == null || group.variants == null || group.variants.Count == 0) return null;

        int start = Random.Range(0, group.variants.Count);
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

    private bool IsTokenValid(AudioSource source, int token)
    {
        return source != null && sfxTokens.TryGetValue(source, out var current) && current == token;
    }

    private bool IsMixerTokenValid(AudioMixer mixer, string param, int token)
    {
        if (mixer == null) return false;
        string key = GetMixerTokenKey(mixer, param);
        return mixerParamTokens.TryGetValue(key, out int current) && current == token;
    }

    private string GetMixerTokenKey(AudioMixer mixer, string param)
    {
        return $"{mixer.GetInstanceID()}:{param}";
    }

    private static SfxVariant PickVariant(SfxVariantGroup group)
    {
        if (group == null || group.variants == null || group.variants.Count == 0) return null;

        float total = 0f;
        for (int i = 0; i < group.variants.Count; i++)
        {
            var variant = group.variants[i];
            if (variant == null || variant.clip == null) continue;
            if (variant.probability <= 0f) continue;
            total += variant.probability;
        }

        if (total <= 0f) return null;

        float pick = Random.value * total;
        float running = 0f;
        for (int i = 0; i < group.variants.Count; i++)
        {
            var variant = group.variants[i];
            if (variant == null || variant.clip == null) continue;
            if (variant.probability <= 0f) continue;
            running += variant.probability;
            if (pick <= running) return variant;
        }

        return null;
    }
}

internal class SoundFollow : MonoBehaviour
{
    private Transform target;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        enabled = target != null;
    }

    public void Clear()
    {
        target = null;
        enabled = false;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            enabled = false;
            return;
        }

        transform.position = target.position;
    }
}
