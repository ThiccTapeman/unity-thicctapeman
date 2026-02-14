using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicMusic : MonoBehaviour
{
    private static DynamicMusic instance;

    public static DynamicMusic GetInstance()
    {
        if (instance != null) return instance;

        instance = FindFirstObjectByType<DynamicMusic>();
        if (instance != null) return instance;

        GameObject obj = new GameObject("DynamicMusic");
        instance = obj.AddComponent<DynamicMusic>();
        return instance;
    }

    [Serializable]
    public class MusicLayer
    {
        public string layerName;
        public string musicName;
    }

    [Serializable]
    public class LayerRule
    {
        public string layerName;
        public bool play = true;
    }

    [Serializable]
    public class VariableLayerEntry
    {
        public string entryName;
        public List<LayerRule> layers = new();
    }

    [Serializable]
    public class MusicVariable
    {
        public string variableName;
        public float currentValue;
        public bool playAllAhead;
        public float floatMin = 0f;
        public float floatMax = 1f;
        public bool isInt = true;
        public int priority = 0;
        public List<VariableLayerEntry> entries = new();
    }

    [Header("Layers")]
    [SerializeField] private List<MusicLayer> musicLayers = new();

    [Header("Variables")]
    [SerializeField] private List<MusicVariable> variables = new();

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Min(0f)] private float fadeSeconds = 1f;
    [SerializeField, Min(0f)] private float startDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float deathRestartDelaySeconds = 1f;
    [SerializeField] private bool autoStartOnSceneLoad = true;

    [Header("Debug")]
    [SerializeField] private bool isStarted;

    private readonly Dictionary<string, AudioSource> layerSources = new();
    private readonly Dictionary<string, float> layerBaseVolumes = new();
    private readonly Dictionary<string, bool> layerActiveState = new();
    private readonly Dictionary<string, LayerDecision> layerDecisions = new();
    private Coroutine startRoutine;
    private Coroutine deathRestartRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        if (autoStartOnSceneLoad)
        {
            StartMusic();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void StartMusic()
    {
        StartMusicInternal(true);
    }

    private void StartMusicInternal(bool applyStartDelay)
    {
        if (isStarted) return;
        startRoutine = StartCoroutine(StartAfterDelay(applyStartDelay));
    }

    private IEnumerator StartAfterDelay(bool applyStartDelay)
    {
        isStarted = true;

        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("DynamicMusic: SoundManager instance not found.");
            isStarted = false;
            yield break;
        }

        float delay = applyStartDelay ? Mathf.Max(0f, startDelaySeconds) : 0f;
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        StartLayers();
    }

    private void StartLayers()
    {
        if (SoundManager.Instance == null) return;

        layerSources.Clear();
        layerBaseVolumes.Clear();
        layerActiveState.Clear();

        for (int i = 0; i < musicLayers.Count; i++)
        {
            var layer = musicLayers[i];
            if (layer == null || string.IsNullOrWhiteSpace(layer.layerName)) continue;

            AudioSource source = null;
            if (!string.IsNullOrWhiteSpace(layer.musicName))
            {
                source = SoundManager.Instance.PlayMusicLayer(i, layer.musicName, musicVolume, true);
            }

            layerSources[layer.layerName] = source;
            layerBaseVolumes[layer.layerName] = source != null ? source.volume : 0f;
            layerActiveState[layer.layerName] = false;
        }

        ApplyLayerMix(true);
    }

    public void StopMusic()
    {
        if (!isStarted) return;
        StopMusicInternal(fadeSeconds);
    }

    public void RestartAfterDeath()
    {
        if (deathRestartRoutine != null)
        {
            StopCoroutine(deathRestartRoutine);
        }
        deathRestartRoutine = StartCoroutine(RestartAfterDeathRoutine());
    }

    private IEnumerator RestartAfterDeathRoutine()
    {
        StopMusicImmediate();
        float delay = Mathf.Max(0f, deathRestartDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        StartMusicInternal(false);
    }

    private void StopMusicImmediate()
    {
        StopMusicInternal(0f);
    }

    private void StopMusicInternal(float stopFadeSeconds)
    {
        isStarted = false;

        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        if (SoundManager.Instance != null)
        {
            for (int i = 0; i < musicLayers.Count; i++)
            {
                SoundManager.Instance.StopMusicLayer(i, stopFadeSeconds);
            }
        }

        layerSources.Clear();
        layerBaseVolumes.Clear();
        layerActiveState.Clear();
    }

    public void SetVariable(string variableName, int value)
    {
        SetVariable(variableName, (float)value);
    }

    public void SetVariable(string variableName, float value)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            Debug.LogWarning("DynamicMusic: Missing variable name for SetVariable.");
            return;
        }

        var variable = FindVariable(variableName);
        if (variable == null)
        {
            Debug.LogWarning($"DynamicMusic: Missing variable '{variableName}'.");
            return;
        }

        variable.currentValue = value;
        ApplyLayerMix(false);
    }

    private MusicVariable FindVariable(string variableName)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (variable == null) continue;
            if (string.Equals(variable.variableName, variableName, StringComparison.OrdinalIgnoreCase))
            {
                return variable;
            }
        }

        return null;
    }

    private void ApplyLayerMix(bool force)
    {
        if (!isStarted || SoundManager.Instance == null) return;
        BuildDecisions();

        for (int i = 0; i < musicLayers.Count; i++)
        {
            var layer = musicLayers[i];
            if (layer == null || string.IsNullOrWhiteSpace(layer.layerName)) continue;

            bool shouldPlay = layerDecisions.TryGetValue(layer.layerName, out var decision) && decision.play;
            if (!force && layerActiveState.TryGetValue(layer.layerName, out bool current) && current == shouldPlay)
            {
                continue;
            }

            layerActiveState[layer.layerName] = shouldPlay;
            float baseVolume = layerBaseVolumes.TryGetValue(layer.layerName, out var value) ? value : 0f;
            float target = shouldPlay ? baseVolume : 0f;
            SoundManager.Instance.FadeMusicLayerVolume(i, target, fadeSeconds);
        }
    }

    private void BuildDecisions()
    {
        layerDecisions.Clear();

        for (int v = 0; v < variables.Count; v++)
        {
            var variable = variables[v];
            if (variable == null) continue;
            if (variable.entries == null || variable.entries.Count == 0) continue;

            int index = ResolveEntryIndex(variable);
            if (index < 0) continue;

            if (variable.playAllAhead)
            {
                for (int i = 0; i <= index; i++)
                {
                    ApplyEntryRules(variable, variable.entries[i]);
                }
            }
            else
            {
                ApplyEntryRules(variable, variable.entries[index]);
            }
        }
    }

    private void ApplyEntryRules(MusicVariable variable, VariableLayerEntry entry)
    {
        if (entry == null || entry.layers == null) return;

        for (int i = 0; i < entry.layers.Count; i++)
        {
            var rule = entry.layers[i];
            if (rule == null || string.IsNullOrWhiteSpace(rule.layerName)) continue;

            if (!layerDecisions.TryGetValue(rule.layerName, out var existing)
                || variable.priority >= existing.priority)
            {
                layerDecisions[rule.layerName] = new LayerDecision(variable.priority, rule.play);
            }
        }
    }

    private int ResolveEntryIndex(MusicVariable variable)
    {
        if (variable.entries == null || variable.entries.Count == 0) return -1;

        int count = variable.entries.Count;
        if (variable.isInt)
        {
            int index = Mathf.RoundToInt(variable.currentValue);
            return Mathf.Clamp(index, 0, count - 1);
        }

        float min = variable.floatMin;
        float max = variable.floatMax;
        if (Mathf.Approximately(min, max))
        {
            return 0;
        }

        float t = Mathf.InverseLerp(min, max, variable.currentValue);
        int mapped = Mathf.FloorToInt(t * count);
        if (mapped >= count) mapped = count - 1;
        if (mapped < 0) mapped = 0;
        return mapped;
    }

    private readonly struct LayerDecision
    {
        public readonly int priority;
        public readonly bool play;

        public LayerDecision(int priority, bool play)
        {
            this.priority = priority;
            this.play = play;
        }
    }
}
