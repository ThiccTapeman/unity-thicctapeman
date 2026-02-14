using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;


namespace ThiccTapeman.Input
{
    /// <summary>
    /// InputManager class to handle inputs alongside unitys new InputSystem
    /// 
    /// <example>
    /// <code>
    /// InputItem item = InputManager.GetInstance().FindAction("TestMap", "TestAction");
    /// if(item.GetTriggered()) Debug.Log("Hello World!");
    /// </code>
    /// </example>
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ------------------------------------ //
        // Instance Handling                    //
        // ------------------------------------ //
        #region Instance Handling

        private static InputManager instance;

        /// <summary>
        /// Gets or creates a new instance for InputManager
        /// </summary>
        /// <returns>The instance for InputManager</returns>
        public static InputManager GetInstance()
        {
            if (instance != null) return instance;

            instance = FindFirstObjectByType<InputManager>();

            if (instance != null) return instance;

            GameObject gameObject = new GameObject("InputManager");
            instance = gameObject.AddComponent<InputManager>();

            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            inputActions = new Dictionary<string, InputItem>();
            tempInputActions = new Dictionary<string, InputItem>();

            tempActions = new InputActionMap();
            EnsureActionMapLoaded();
        }

        #endregion
        // ------------------------------------ //
        // Setting Action Map                   //
        // ------------------------------------ //
        #region

        public string actionMapPath = "Inputs/ControllScheme";
        public InputActionAsset actions;
        public InputActionMap tempActions;
        [Header("Rebinding")]
        [SerializeField] private bool autoLoadBindingOverrides = true;
        [SerializeField] private string bindingOverridesKey = "InputManager.BindingOverrides";
        public int deviceCheckIntervalFrames = 5;
        public float defaultInputBufferSeconds = 0.2f;
        public enum InputDeviceGroup
        {
            None,
            KeyboardMouse,
            Gamepad
        }

        public void SetActionMapPath(string actionMapPath)
        {
            this.actionMapPath = actionMapPath;

            LoadActionMap(actionMapPath);
        }

        private void EnsureActionMapLoaded()
        {
            if (actions == null)
            {
                LoadActionMap(actionMapPath);
                return;
            }

            if (!loaded)
            {
                TriggerLoad();
            }

            EnableActions();
        }

        private void LoadActionMap(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("InputManager: Action map path is empty.");
                return;
            }

            actions = Resources.Load<InputActionAsset>(path);
            if (actions == null)
            {
                Debug.LogWarning($"InputManager: Could not load InputActionAsset at Resources path '{path}'.");
                return;
            }

            TriggerLoad();
            EnableActions();
            if (autoLoadBindingOverrides)
            {
                LoadBindingOverrides();
            }
        }

        private void OnEnable()
        {
            EnsureActionMapLoaded();
        }

        private void OnDisable()
        {
            if (actions != null) actions.Disable();
            if (tempActions != null) tempActions.Disable();
        }

        private int deviceCheckFrameCounter;

        private void Update()
        {
            deviceCheckFrameCounter++;
            if (deviceCheckIntervalFrames > 1 && deviceCheckFrameCounter % deviceCheckIntervalFrames != 0)
            {
                return;
            }

            InputDevice newDevice = null;
            InputDeviceGroup newGroup = InputDeviceGroup.None;

            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
            {
                newDevice = Gamepad.current;
                newGroup = InputDeviceGroup.Gamepad;
            }
            else if (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame)
            {
                newDevice = Keyboard.current;
                newGroup = InputDeviceGroup.KeyboardMouse;
            }
            else if (Mouse.current != null && Mouse.current.wasUpdatedThisFrame)
            {
                newDevice = Mouse.current;
                newGroup = InputDeviceGroup.KeyboardMouse;
            }

            if (newDevice != null && newGroup != lastInputDeviceGroup)
            {
                lastInputDevice = newDevice;
                lastInputDeviceGroup = newGroup;
                OnChangedInputDevice?.Invoke(newGroup);
            }
        }

        private void EnableActions()
        {
            if (actions != null) actions.Enable();
            if (tempActions != null) tempActions.Enable();
        }

        #endregion
        // ------------------------------------ //
        // Getting actions                      //
        // ------------------------------------ //
        #region Getting Actions

        /// <summary>
        /// Gets an action from the preconfigured actionmap
        /// </summary>
        /// <param name="map">The map</param>
        /// <param name="action">The action inside the map</param>
        /// <returns>The InputItem of that action</returns>
        public InputItem GetAction(string map, string action)
        {
            if (actions == null)
            {
                Debug.LogWarning($"InputManager: Action map asset not loaded. Cannot get action '{map}/{action}'.");
                return null;
            }

            // Try's to find the action
            InputAction inputAction = actions.FindAction(map + "/" + action);

            // There weren't an action there
            if (inputAction == null) return NoActionMapFound(map, action);

            InputItem inputItem = GetFromDictionary(map + "/" + action, inputActions);

            // If there wasn't already a item inside the dictionary, create a new one and add it
            if (inputItem == null)
            {
                inputItem = new InputItem(inputAction);

                AddToDictionary(map + "/" + action, inputItem, inputActions);

                return inputItem;
            }

            // Otherwise return the item it found
            return inputItem;
        }

        /// <summary>
        /// Creates a temporary action which is only aviable that same session as it was created
        /// 
        /// <example>
        /// <code>
        /// This example will create a temporary action for space
        /// 
        /// GetTempAction("Test", "<Keyboard>/space");
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="map">The map</param>
        /// <param name="action">The action inside the map</param>
        /// <param name="binding">The binding</param>
        /// <returns>The newly created temporary action</returns>
        public InputItem GetTempAction(string action, string binding)
        {
            if (tempActions == null) return null;
            bool wasEnabled = tempActions.enabled;
            if (wasEnabled) tempActions.Disable();

            // Try's to find the action
            InputAction inputAction = tempActions.FindAction(action);

            if (inputAction == null)
            {
                // Adds the action to the tempAction
                inputAction = tempActions.AddAction(action, InputActionType.Value, binding);

                inputAction.Enable();
            }

            InputItem inputItem = GetFromDictionary(action, tempInputActions);

            // If there wasn't already a item inside the dictionary, create a new one and add it
            if (inputItem == null)
            {
                inputItem = new InputItem(inputAction);

                AddToDictionary(action, inputItem, tempInputActions);

                if (wasEnabled) tempActions.Enable();
                return inputItem;
            }

            // Otherwise return the item it found
            if (wasEnabled) tempActions.Enable();
            return inputItem;
        }

        public bool GetLoaded()
        {
            return loaded;
        }

        public bool HasLoaded => loaded;

        #endregion
        // ------------------------------------ //
        // Rebinding                            //
        // ------------------------------------ //
        #region Rebinding

        private InputActionRebindingExtensions.RebindingOperation currentRebind;

        public bool IsRebinding => currentRebind != null;

        public bool StartRebind(string map, string action, int bindingIndex, Action<InputAction> onComplete = null, Action<InputAction> onCancel = null)
        {
            if (actions == null)
            {
                Debug.LogWarning("InputManager: Cannot rebind, action map asset not loaded.");
                return false;
            }

            var inputAction = actions.FindAction(map + "/" + action);
            if (inputAction == null)
            {
                Debug.LogWarning($"InputManager: Cannot rebind, action '{map}/{action}' not found.");
                return false;
            }

            if (bindingIndex < 0 || bindingIndex >= inputAction.bindings.Count)
            {
                Debug.LogWarning($"InputManager: Binding index {bindingIndex} is out of range for '{map}/{action}'.");
                return false;
            }

            CancelRebind();

            inputAction.Disable();

            currentRebind = inputAction.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation =>
                {
                    operation.Dispose();
                    currentRebind = null;
                    inputAction.Enable();
                    onCancel?.Invoke(inputAction);
                })
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    currentRebind = null;
                    inputAction.Enable();
                    SaveBindingOverrides();
                    onComplete?.Invoke(inputAction);
                });

            currentRebind.Start();
            return true;
        }

        public void CancelRebind()
        {
            if (currentRebind == null) return;
            currentRebind.Cancel();
            currentRebind.Dispose();
            currentRebind = null;
        }

        public void SaveBindingOverrides()
        {
            if (actions == null) return;
            string json = actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(bindingOverridesKey, json);
            PlayerPrefs.Save();
        }

        public void LoadBindingOverrides()
        {
            if (actions == null) return;
            if (!PlayerPrefs.HasKey(bindingOverridesKey)) return;
            string json = PlayerPrefs.GetString(bindingOverridesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;
            actions.LoadBindingOverridesFromJson(json);
        }

        public void ClearBindingOverrides()
        {
            if (actions == null) return;
            actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(bindingOverridesKey);
        }

        #endregion
        // ------------------------------------ //
        // Mouse Position                       //
        // ------------------------------------ //
        public Vector3 GetMouseWorldPosition(LayerMask layerMask)
        {
            if (HasDevice<Gamepad>())
            {
                Vector2 mousePosition = new Vector2(Screen.width / 2, Screen.height / 2);

                return Utils.MouseUtils.GetScreenToWorldPosition(mousePosition, Camera.main, layerMask);
            }

            return Utils.MouseUtils.GetScreenToWorldPosition(Mouse.current.position.value, Camera.main, layerMask);
        }

        private bool HasDevice<T>() where T : InputDevice
        {
            if (actions.devices == null) return false;

            for (int i = 0; i < actions.devices.Value.Count; i++)
            {
                if (actions.devices.Value[i] is T) return true;
            }

            return false;
        }

        // ------------------------------------ //
        // Dictionary Handling                  //
        // ------------------------------------ //
        #region Dictionary Handling
        // Storing the InputItems so we don't create a bunch of them for the same actions
        private Dictionary<string, InputItem> inputActions;
        private Dictionary<string, InputItem> tempInputActions;

        private InputItem GetFromDictionary(string path, Dictionary<string, InputItem> dictionary)
        {
            // Just checks if the dictionary contains the action
            dictionary.TryGetValue(path, out InputItem item);

            if (item == null) return null;

            return item;
        }

        private void AddToDictionary(string path, InputItem inputItem, Dictionary<string, InputItem> dictionary)
        {
            // If the map isn't already there, add a new one
            if (!dictionary.ContainsKey(path)) dictionary.Add(path, inputItem);
        }

        #endregion
        // ------------------------------------ //
        // Error Handling                       //
        // ------------------------------------ //
        #region Error Handling

        private InputItem NoActionMapFound(string map, string action)
        {
            Debug.LogError("Action '" + action + "' not found in map '" + map + "'.");
            return null;
        }

        #endregion
        // ------------------------------------ //
        // Events                               //
        // ------------------------------------ //
        #region Events

        public bool loaded = false;
        public event Action OnLoad
        {
            add
            {
                if (value == null) return;

                if (loaded) value.Invoke();

                _OnLoad += value;
            }
            remove
            {
                if (value == null) return;

                _OnLoad -= value;
            }
        }

        private event Action _OnLoad;

        public event Action<InputDeviceGroup> OnChangedInputDevice;
        private InputDevice lastInputDevice;
        private InputDeviceGroup lastInputDeviceGroup = InputDeviceGroup.None;

        private void TriggerLoad()
        {
            loaded = true;
            _OnLoad?.Invoke();
        }

        #endregion
    }

    // ------------------------------------ //
    // Input Item class                     //
    // ------------------------------------ //
    #region Input Item class

    public class InputItem
    {
        public InputAction inputAction;
        private float lastTriggeredTime = -999f;
        private bool hasBufferedTrigger;
        private object bufferedValue;
        private float bufferSecondsOverride = -1f;
        private readonly InputManager inputManager;

        public InputItem(InputAction inputAction)
        {
            this.inputAction = inputAction;
            inputManager = Application.isPlaying ? InputManager.GetInstance() : null;
            if (this.inputAction != null)
            {
                this.inputAction.performed += OnPerformed;
            }
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            lastTriggeredTime = Time.time;
            hasBufferedTrigger = true;
            bufferedValue = context.ReadValueAsObject();
        }

        /// <summary>
        /// Reads the value
        /// </summary>
        /// <typeparam name="T">The type of variable that should be read and returned</typeparam>
        /// <returns>Returns the value in T</returns>
        public T ReadValue<T>() where T : struct
        {
            return inputAction.ReadValue<T>();
        }

        /// <summary>
        /// Makes buttons easier
        /// </summary>
        /// <returns>Returns true every time it get's triggered</returns>
        public bool Triggered(bool everyTime = false)
        {
            return inputAction.triggered;
        }

        /// <summary>
        /// Returns true if the action was triggered within the buffer window.
        /// Consumes the buffered trigger on success or when it expires.
        /// </summary>
        public bool BufferedTriggered(float bufferSeconds)
        {
            if (!hasBufferedTrigger) return false;

            if (Time.time - lastTriggeredTime <= bufferSeconds)
            {
                hasBufferedTrigger = false;
                return true;
            }

            hasBufferedTrigger = false;
            return false;
        }

        public bool BufferedTriggered()
        {
            return BufferedTriggered(GetBufferSeconds());
        }

        /// <summary>
        /// Lets you read from the buffer instead.
        /// </summary>
        /// <typeparam name="T">The type of variable that should be read and returned</typeparam>
        /// <returns>Returns the value in T</returns>
        public T ReadBuffer<T>() where T : struct
        {
            if (!hasBufferedTrigger) return default;

            if (Time.time - lastTriggeredTime > GetBufferSeconds())
            {
                hasBufferedTrigger = false;
                bufferedValue = null;
                return default;
            }

            hasBufferedTrigger = false;

            if (bufferedValue is T value)
            {
                bufferedValue = null;
                return value;
            }

            bufferedValue = null;
            return default;
        }

        public void SetBufferSeconds(float bufferSeconds)
        {
            bufferSecondsOverride = Mathf.Max(0f, bufferSeconds);
        }

        private float GetBufferSeconds()
        {
            if (bufferSecondsOverride >= 0f) return bufferSecondsOverride;
            if (inputManager != null) return inputManager.defaultInputBufferSeconds;
            return 0f;
        }
    }

    #endregion

}
