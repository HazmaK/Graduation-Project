using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RetroFPS.UI.Menu
{
    /// <summary>Central Back, Tab navigation, and active-context focus repair.</summary>
    public sealed class MenuInputRouter : MonoBehaviour
    {
        [SerializeField] private MainMenuController controller;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputActionReference cancel;
        [SerializeField] private InputActionReference rightClick;
        private BaseEventData submitEventData;

        private void Awake() => submitEventData = new BaseEventData(eventSystem);

        private void OnEnable()
        {
            if (cancel != null) cancel.action.performed += OnBack;
            if (rightClick != null) rightClick.action.performed += OnBack;
            // The UI module owns action enable/disable, including shared actions.
        }

        private void OnDisable()
        {
            if (cancel != null) cancel.action.performed -= OnBack;
            if (rightClick != null) rightClick.action.performed -= OnBack;
        }

        private void OnBack(InputAction.CallbackContext context)
        {
            // RightClick is PassThrough: its release also performs. Only the press means Back.
            if (context.ReadValueAsButton() && !controller.InputLocked) controller.Back();
        }

        private void Update()
        {
            if (controller.InputLocked) return;
            UnityEngine.UI.Selectable selected = controller.FindActive(eventSystem.currentSelectedGameObject);
            if (selected == null)
            {
                selected = controller.DefaultFocus;
                if (selected != null) eventSystem.SetSelectedGameObject(selected.gameObject);
            }
            Keyboard keyboard = Keyboard.current;
            if (selected == null || keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                submitEventData.Reset();
                ExecuteEvents.Execute<ISubmitHandler>(eventSystem.currentSelectedGameObject,
                    submitEventData, ExecuteEvents.submitHandler);
                if (controller.InputLocked) return;
            }
            if (!keyboard.tabKey.wasPressedThisFrame) return;
            UnityEngine.UI.Navigation navigation = selected.navigation;
            UnityEngine.UI.Selectable next = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? navigation.selectOnUp : navigation.selectOnDown;
            if (next != null && next.IsInteractable()) eventSystem.SetSelectedGameObject(next.gameObject);
        }
    }
}
