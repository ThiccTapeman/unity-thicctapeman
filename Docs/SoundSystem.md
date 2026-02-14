# Sound System

## Scope
This document covers the project sound stack built around `SoundManager`, `SoundLibrary`, and `DynamicMusic`.

## Core Assets
- `SoundLibrary` is a ScriptableObject that stores all SFX, music, and narration entries.
- `SoundManager` is the runtime singleton that plays sounds from the library.
- `DynamicMusic` is a runtime controller for layered music states.

## SoundLibrary Structure
`SoundLibrary` stores entries in a tree and builds a lookup by name at runtime. Entries are typed:
- `SfxDefinition` for single SFX clips.
- `SfxVariantGroup` for SFX that pick a random variant by probability.
- `MusicDefinition` for music clips, including optional intro + loop.
- `NarrationGroup` for ordered or randomized narration lists.
- `NarrationVariantGroup` for narration variants.
- `SoundLibraryFolder` to organize entries.

Naming rules and lookup behavior:
- All entries are stored by their `name` field.
- Duplicate names keep the first entry and log a warning.
- Variant lookups can use `GroupName.ItemName` to address a specific variant inside a group.

## SoundManager Overview
`SoundManager` provides pooled audio sources and helper APIs for SFX, music, and narration.

### Pools
- SFX uses a pool of `AudioSource` objects (`sfxPoolSize`).
- Narration uses a smaller pool (`narrationPoolSize`).
- Music uses a fixed array of layers controlled by `musicLayerCount`.

### Playback APIs
- `PlaySfx(name, position, follow, volumeMultiplier, loopOverride)`
- `PlayMusic(name, volumeMultiplier, loopOverride)`
- `PlayMusicLayer(layerIndex, name, volumeMultiplier, loopOverride)`
- `PlayNarration(name, position, follow, volumeMultiplier)`
- `StopMusicLayer(layerIndex, fadeSeconds)`
- `StopMusic(fadeSeconds)`
- `FadeMusicLayerVolume(layerIndex, targetVolume, fadeSeconds)`
- `FadeMixerFilter(mixerOrGroup, exposedParam, targetValue, fadeSeconds)`

### Music intros
If a `MusicDefinition` has both `introClip` and `loopClip`, `SoundManager` plays the intro once, then switches to the loop clip.

### Variants
- `SfxVariantGroup` randomly picks a variant by probability.
- `NarrationVariantGroup` randomly picks a valid clip.
- You can request a specific variant using `GroupName.ItemName`.

## DynamicMusic (Layered Music)
`DynamicMusic` runs layered music and fades layers on or off based on variables.

### Data Model
- `MusicLayer` defines a `layerName` and the `musicName` to play on that layer index.
- `MusicVariable` defines how a value maps to a `VariableLayerEntry`.
- `VariableLayerEntry` contains `LayerRule` entries (layer name + play true/false).

### Variable Mapping
- `isInt = true` uses `currentValue` as an index into entries.
- `isInt = false` maps `currentValue` across `floatMin` to `floatMax` into entry indices.
- `playAllAhead = true` applies all entries from `0` to the resolved index.
- `priority` resolves conflicts per layer. Higher priority wins. Ties prefer later rules.

### Runtime Usage
- Call `DynamicMusic.GetInstance().SetVariable("Intensity", 0)` or `SetVariable("Intensity", 0.5f)`.
- Layers not explicitly set by any variable default to off.

### Important Constraints
- `SoundManager.musicLayerCount` must be at least the number of `musicLayers` in `DynamicMusic`.
- Each `MusicLayer` must have a unique `layerName` and a valid `musicName` in the `SoundLibrary`.

## Example Usage
```csharp
// SFX
SoundManager.Instance.PlaySfx("UI.Click", transform.position);

// Music
SoundManager.Instance.PlayMusic("Combat.Main");

// Narration
SoundManager.Instance.PlayNarration("Story.Intro", transform.position);

// Dynamic music variables
DynamicMusic.GetInstance().SetVariable("Intensity", 1);
DynamicMusic.GetInstance().SetVariable("IsIndoors", 0);
DynamicMusic.GetInstance().SetVariable("CustomVariable", 1.4);
```

## Common Pitfalls
- `SoundManager` needs a `SoundLibrary` assigned in the inspector.
- `SoundLibrary` uses entry `name` for lookup, not the asset filename.
- `musicLayerCount` must be large enough for the highest layer used.
- `DynamicMusic` fades layer volume, so base volume comes from the `SoundLibrary` entry.

## File References
- `Assets/ThiccTapeman/Scripts/SoundSystem/SoundManager.cs`
- `Assets/ThiccTapeman/Scripts/SoundSystem/SoundLibrary.cs`
- `Assets/ThiccTapeman/Scripts/SoundSystem/DynamicMusic.cs`
