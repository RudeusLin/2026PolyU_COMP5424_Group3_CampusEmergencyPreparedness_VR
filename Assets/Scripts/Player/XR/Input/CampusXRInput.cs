using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace CampusEmergency.Player.XR
{
    [DefaultExecutionOrder(-100)]
    public sealed class CampusXRInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        public InputActionAsset Actions => actions;
        public bool HeadTracked => Button("HeadTracked");
        public bool LeftTracked => HeadTracked && Button("LeftTracked");
        public bool RightTracked => HeadTracked && Button("RightTracked");
        public bool TeleportHeld => Button("Teleport");
        public bool InteractionHeld => Button("Interact");
        public Vector2 TurnAxis => Read<Vector2>("Turn");
        public bool CancelTeleport => LeftTracked && Button("CancelTeleport");
        public bool InteractPressed => Action("Interact")?.WasPressedThisFrame() == true;
        public bool HasInteractionFocus
        {
            get
            {
                bool openXR = XRGeneralSettings.Instance != null
                    && XRGeneralSettings.Instance.Manager != null
                    && XRGeneralSettings.Instance.Manager.activeLoader is OpenXRLoader;
                return ResolveInteractionFocus(openXR, openXR && OpenXRUtility.IsSessionFocused, Application.isFocused);
            }
        }
        // A desktop window losing focus must not disable an active headset session.
        // Conversely, opening the headset system menu must suspend interactions.
        public static bool ResolveInteractionFocus(bool openXR, bool sessionFocused, bool desktopFocused)
            => openXR ? sessionFocused : desktopFocused;

        public void Configure(InputActionAsset asset) => actions = asset;
        private void OnEnable()
        {
            if (actions == null) return;
            foreach (string name in new[] { "HeadTracked", "LeftTracked", "RightTracked" })
            {
                var action = Action(name);
                if (action != null) action.wantsInitialStateCheck = true;
            }
            actions.Enable();
        }
        private void OnDisable() { if (actions != null) actions.Disable(); }
        private InputAction Action(string name) => actions != null ? actions.FindAction("Campus/" + name) : null;
        private T Read<T>(string name) where T : struct
        { var action = Action(name); return action != null ? action.ReadValue<T>() : default; }
        private bool Button(string name) => Read<float>(name) > .5f;
    }
}
