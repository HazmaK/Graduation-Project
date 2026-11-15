using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RetroFPS.UI.Menu
{
    /// <summary>Central Back, Tab navigation, and active-context focus repair.</summary>
    public sealed class MenuInputRouter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour controller;
        [SerializeField] private bool pauseHost;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputActionReference cancel;
        [SerializeField] private InputActionReference rightClick;
        private BaseEventData submitEventData;
        private IMenuInputContext context;

        private void Awake()
        {
            context = controller as IMenuInputContext;
            submitEventData = new BaseEventData(eventSystem);
        }

        /// <summary>Accepts the HUD guard's current system without allocating on additive rebinding.</summary>
        public void BindEventSystem(EventSystem system) => eventSystem = system;

        private void OnEnable()
        {
            if (pauseHost) return; // PauseRuntime is the single early Back/toggle reader.
            if (cancel != null) cancel.action.performed += OnBack;
            if (rightClick != null) rightClick.action.performed += OnBack;
            // The UI module owns action enable/disable, including shared actions.
        }

        private void OnDisable()
        {
            if (pauseHost) return;
            if (cancel != null) cancel.action.performed -= OnBack;
            if (rightClick != null) rightClick.action.performed -= OnBack;
        }

        private void OnBack(InputAction.CallbackContext context)
        {
            // RightClick is PassThrough: its release also performs. Only the press means Back.
            if (context.ReadValueAsButton() && this.context != null && !this.context.InputLocked) this.context.Back();
        }

        private void Update()
        {
            if (context == null || eventSystem == null || context.InputLocked) return;
            UnityEngine.UI.Selectable selected = context.FindActive(eventSystem.currentSelectedGameObject);
            if (selected == null)
            {
                selected = context.DefaultFocus;
                if (selected != null) eventSystem.SetSelectedGameObject(selected.gameObject, submitEventData);
            }
            Keyboard keyboard = Keyboard.current;
            if (selected == null || keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                submitEventData.Reset();
                ExecuteEvents.Execute<ISubmitHandler>(eventSystem.currentSelectedGameObject,
                    submitEventData, ExecuteEvents.submitHandler);
                if (context.InputLocked) return;
            }
            if (!keyboard.tabKey.wasPressedThisFrame) return;
            UnityEngine.UI.Navigation navigation = selected.navigation;
            UnityEngine.UI.Selectable next = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? navigation.selectOnUp : navigation.selectOnDown;
            if (next != null && next.IsInteractable()) eventSystem.SetSelectedGameObject(next.gameObject, submitEventData);
        }
    }
}
