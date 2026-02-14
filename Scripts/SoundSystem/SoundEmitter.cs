using UnityEngine;
using UnityEngine.Serialization;

public class SoundEmitter : MonoBehaviour
{
    [FormerlySerializedAs("sfxId")]
    [SerializeField] private string sfxName;
    [SerializeField] private bool playOnEnable = false;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool attachToEmitter = true;
    [Range(0f, 2f)][SerializeField] private float volumeMultiplier = 1f;
    [SerializeField] private float stopFadeSeconds = 0.1f;

    private AudioSource activeSource;

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("SoundEmitter: No SoundManager instance in scene.");
            return;
        }

        var follow = attachToEmitter ? transform : null;
        activeSource = SoundManager.Instance.PlaySfx(
            sfxName,
            transform.position,
            follow,
            volumeMultiplier,
            loop
        );
    }

    public void Stop()
    {
        if (SoundManager.Instance == null || activeSource == null)
        {
            return;
        }

        SoundManager.Instance.StopSfx(activeSource, stopFadeSeconds);
        activeSource = null;
    }
}
