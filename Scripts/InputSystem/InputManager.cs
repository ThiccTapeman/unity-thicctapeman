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
    public class InputManager
    {
        // ------------------------------------ //
        // Instance Handling                    //
        // ------------------------------------ //
        #region Instance Handling

        private static InputManager instance;
        private InputManagerMonoBehaviour inputSystemMonoBehaviour;

        /// <summary>
        /// Gets or creates a new instance for InputManager
        /// </summary>
        /// <returns>The instance for InputManager</returns>
        public static InputManager GetInstance()
        {
            if (instance == null) instance = new InputManager();

            return instance;
        }

        // Private constructor
        private InputManager()
        {
            // Creates the InputManager object and attaches the monobehaviour script to it
            GameObject gameObject = new GameObject("InputManager");
            inputSystemMonoBehaviour = gameObject.AddComponent<InputManagerMonoBehaviour>();

            inputActions = new Dictionary<string, InputItem>();
            tempInputActions = new Dictionary<string, InputItem>();
        }

        #endregion
        // ------------------------------------ //
        // Setting Action Map                   //
        // ------------------------------------ //
        #region

        public string actionMapPath = "Inputs/ControllScheme";

        public void SetActionMapPath(string actionMapPath)
        {
            this.actionMapPath = actionMapPath;

            inputSystemMonoBehaviour.LoadActionMap(actionMapPath);
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
            // Try's to find the action
            InputAction inputAction = inputSystemMonoBehaviour.actions.FindAction(map + "/" + action);

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
            // Try's to find the action
            InputAction inputAction = inputSystemMonoBehaviour.tempActions.FindAction(action);

            if (inputAction == null)
            {
                // Adds the action to the tempAction
                inputAction = inputSystemMonoBehaviour.tempActions.AddAction(action, InputActionType.Value, binding);

                inputAction.Enable();
            }

            InputItem inputItem = GetFromDictionary(action, tempInputActions);

            // If there wasn't already a item inside the dictionary, create a new one and add it
            if (inputItem == null)
            {
                inputItem = new InputItem(inputAction);

                AddToDictionary(action, inputItem, tempInputActions);

                return inputItem;
            }

            // Otherwise return the item it found
            return inputItem;
        }

        public bool GetLoaded()
        {
            return loaded;
        }

        #endregion
        // ------------------------------------ //
        // Mouse Position                       //
        // ------------------------------------ //
        public Vector3 GetMouseWorldPosition(LayerMask layerMask)
        {
            if(HasDevice<Gamepad>())
            {
                Vector2 mousePosition = new Vector2(Screen.width / 2, Screen.height / 2);

                return Utils.MouseUtils.GetScreenToWorldPosition(mousePosition, Camera.main, layerMask);
            } 

            return Utils.MouseUtils.GetScreenToWorldPosition(Mouse.current.position.value, Camera.main, layerMask);
        }

        private bool HasDevice<T>() where T : InputDevice
        {
            if (inputSystemMonoBehaviour.actions.devices == null) return false;

            for (int i = 0; i < inputSystemMonoBehaviour.actions.devices.Value.Count; i++)
            {
                if (inputSystemMonoBehaviour.actions.devices.Value[i] is T) return true;
            }

            return false;
        }

        // ------------------------------------ //
        // Changing bindings                    //
        // ------------------------------------ //
        #region Changing Bindings

        private InputActionRebindingExtensions.RebindingOperation rebindingAction;

        public void ChangeBinding(BindingClass changeBinding, int bindingIndex, bool allCompositeParts = false)
        {
            rebindingAction?.Dispose();

            void CleanUp()
            {
                rebindingAction?.Dispose();
                rebindingAction = null;
            }

            InputAction inputAction = changeBinding.inputItem.inputAction;

            inputAction.Disable();
            changeBinding.OnStart();

            rebindingAction = inputAction.PerformInteractiveRebinding(bindingIndex).WithCancelingThrough("<Keyboard>/escape").WithCancelingThrough("<Gamepad>/button south")
            .OnCancel(operation =>
            {
                inputAction.Enable();
                changeBinding.OnCancel();
                CleanUp();
            })
            .OnComplete(operation =>
            {
                inputAction.Enable();

                if(CheckDuplicateBindings(inputAction, bindingIndex, allCompositeParts))
                {
                    inputAction.RemoveBindingOverride(bindingIndex);
                    CleanUp();
                    ChangeBinding(changeBinding, bindingIndex, allCompositeParts);

                    return;
                }

                changeBinding.OnChanged();

                CleanUp();
            });

            changeBinding.OnEnd();
        }

        private bool CheckDuplicateBindings(InputAction action, int bindingIndex, bool allCompositeParts = false)
        {
            InputBinding newBinding = action.bindings[bindingIndex];

            foreach(InputBinding binding in action.actionMap.bindings)
            {
                if (binding.action == newBinding.action) continue;

                if (binding.effectivePath == newBinding.effectivePath) return true;
            }

            if(allCompositeParts )
            {
                for(int i = 1; i < bindingIndex; i++)
                {
                    if (action.bindings[i].effectivePath == newBinding.effectivePath)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion
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

                if(loaded) value.Invoke();

                _OnLoad += value;
            }
            remove
            {
                if (value == null) return;

                _OnLoad -= value;
            }
        }

        private event Action _OnLoad;

        private void TriggerLoad()
        {
            loaded = true;
            _OnLoad?.Invoke();
        }

        #endregion
        // ------------------------------------ //
        // InputManager MonoBehaviour Class     //
        // ------------------------------------ //
        #region MonoBehaviour Class

        public class InputManagerMonoBehaviour : MonoBehaviour
        {
            public InputActionAsset actions;
            public InputActionMap tempActions;

            private void Start()
            {
                // Creates a new acitonmap for the temporary actions
                tempActions = new InputActionMap();

                // Loads the preconfigured acitonmap
                LoadActionMap(GetInstance().actionMapPath);
            }

            public void LoadActionMap(string path)
            {
                actions = Resources.Load<InputActionAsset>(path);

                // Invokes the InputManagers onLoad
                InputManager instance = GetInstance();
                instance.TriggerLoad();

                OnEnable();
            }

            private void OnEnable()
            {
                if(actions != null) actions.Enable();
                if(tempActions != null) tempActions.Enable();
            }

            private void OnDisable()
            {
                if(actions != null) actions.Disable();
                if(tempActions != null) tempActions.Disable();
            }
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

        private bool triggered;

        public InputItem(InputAction inputAction)
        {
            this.inputAction = inputAction;
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
        /// <returns>Returns true every other time it get's triggered</returns>
        public bool GetTriggered(bool everyTime = false)
        {
            if (everyTime) return inputAction.triggered;

            if (inputAction.triggered) triggered = !triggered;

            if (triggered)
            {
                return inputAction.triggered;
            }

            return false;
        }
    }

    #endregion

    // ------------------------------------ //
    // ChangeBinding                        //
    // ------------------------------------ //
    #region ChangeBinding Class

    public class BindingClass
    {
        public event Action OnStartChangeBinding;
        public event Action OnBindingChanged;
        public event Action OnEndChangeBinding;
        public event Action OnCanceledChangeBinding;

        public InputItem inputItem;

        public BindingClass(InputItem inputItem)
        {
            this.inputItem = inputItem;
        }

        public void OnStart()
        {
            if(OnStartChangeBinding != null) OnStartChangeBinding();
        }

        public void OnChanged()
        {
            if (OnBindingChanged != null) OnBindingChanged();
        }

        public void OnEnd()
        {
            if (OnEndChangeBinding != null) OnEndChangeBinding();
        }

        public void OnCancel()
        {
            if (OnCanceledChangeBinding != null) OnCanceledChangeBinding();
        }
    }

    #endregion
}