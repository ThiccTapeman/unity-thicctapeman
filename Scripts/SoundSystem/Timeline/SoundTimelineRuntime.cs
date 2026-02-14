using System.Collections;
using UnityEngine;

public class SoundTimelineRuntime : MonoBehaviour
{
    private static SoundTimelineRuntime instance;

    public static SoundTimelineRuntime Instance
    {
        get
        {
            if (instance != null) return instance;
            var obj = new GameObject("SoundTimelineRuntime");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<SoundTimelineRuntime>();
            return instance;
        }
    }

    public Coroutine Run(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }
}
