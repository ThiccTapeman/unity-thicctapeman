using UnityEngine;
using UnityEngine.Timeline;

[TrackClipType(typeof(SoundSfxClip))]
[TrackBindingType(typeof(Transform))]
[TrackColor(0.2f, 0.7f, 0.9f)]
public class SoundSfxTrack : TrackAsset { }

[TrackClipType(typeof(SoundMusicClip))]
[TrackColor(0.9f, 0.6f, 0.2f)]
public class SoundMusicTrack : TrackAsset { }

[TrackClipType(typeof(SoundNarrationClip))]
[TrackBindingType(typeof(Transform))]
[TrackColor(0.7f, 0.3f, 0.9f)]
public class SoundNarrationTrack : TrackAsset { }

[TrackClipType(typeof(SoundMusicLayerFadeClip))]
[TrackColor(0.2f, 0.9f, 0.5f)]
public class SoundMusicLayerFadeTrack : TrackAsset { }

[TrackColor(0.6f, 0.9f, 0.4f)]
public class SoundBeatMarkerTrack : MarkerTrack { }
