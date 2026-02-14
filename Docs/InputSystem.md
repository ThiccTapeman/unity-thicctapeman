# Input System

## Scope
This document covers the custom input wrapper built on Unity's Input System.

## Core Types
- `InputManager` is a singleton that loads an `InputActionAsset` from Resources and provides cached access to actions.
- `InputItem` wraps an `InputAction` with buffer helpers and value reads.

## InputManager Overview
`InputManager` lives in `ThiccTapeman.Input` and is created automatically if missing.

### Action Map Loading
- `actionMapPath` defaults to `Inputs/ControllScheme`.
- The `InputActionAsset` must be placed in `Assets/Resources/Inputs/ControllScheme.inputactions` or the configured path.
- On load, `InputManager` enables actions and fires `OnLoad` once.
- Subscribing to `OnLoad` will invoke immediately if the input system has already loaded.

### Device Tracking
`InputManager` checks devices every `deviceCheckIntervalFrames` frames.
- If a gamepad updates this frame, `OnChangedInputDevice(Gamepad)` fires.
- If keyboard or mouse updates, it fires `OnChangedInputDevice(KeyboardMouse)`.

### Getting Actions
- `GetAction(map, action)` returns a cached `InputItem` for the loaded asset.
- `GetTempAction(action, binding)` creates a runtime-only action for the session.

### Rebinding and Persistence
`InputManager` supports runtime rebinding and saves overrides to `PlayerPrefs`.
- `StartRebind(map, action, bindingIndex, onComplete, onCancel)` starts an interactive rebind.
- `CancelRebind()` cancels the current rebind.
- `SaveBindingOverrides()` writes overrides to `PlayerPrefs` using `bindingOverridesKey`.
- `LoadBindingOverrides()` applies saved overrides (auto-called if `autoLoadBindingOverrides` is true).
- `ClearBindingOverrides()` clears overrides and removes the saved key.

## InputItem Overview
`InputItem` wraps `InputAction` and adds buffering.

### Common Calls
- `ReadValue<T>()` reads the current value.
- `Triggered()` returns `inputAction.triggered`.
- `BufferedTriggered()` returns true if triggered within a buffer window.
- `ReadBuffer<T>()` returns the buffered value.
- `SetBufferSeconds(seconds)` overrides the default buffer window.

Buffering behavior:
- When the action performs, `InputItem` stores the value and timestamp.
- Buffer is consumed once read or when it expires.
- Default buffer seconds is `InputManager.defaultInputBufferSeconds`.

## Mouse World Position
`InputManager.GetMouseWorldPosition(layerMask)`:
- Uses the screen center when a gamepad is active.
- Otherwise uses the current mouse position.

## Example Usage
```csharp
using ThiccTapeman.Input;

var input = InputManager.GetInstance();
InputItem move;
InputItem jump;
InputItem temp;

input.OnLoad += OnLoad;

void OnLoad() {
    move = input.GetAction("Gameplay", "Move");
    jump = input.GetAction("Gameplay", "Jump");

    temp = input.GetTempAction("Inventory.LeftMouse", "<Mouse>/leftButton");

}

void Update() {
    if(!input.HasLoaded) return;
    if(!move || !jump || !temp) return;

    Vector2 moveVec = move.ReadValue<Vector2>();
    if (jump.BufferedTriggered())
    {
        DoJump();
    }

    if (temp.Triggered())
    {
        Debug.Log("Clicked");
    }
}
```

## Common Pitfalls
- The InputActionAsset must be under `Resources` or `InputManager` will not load it.
- `GetAction` will return null if the map or action name does not exist.
- `InputItem.BufferedTriggered` consumes the buffered trigger once read.

## File References
- `Assets/ThiccTapeman/Scripts/InputSystem/InputManager.cs`
