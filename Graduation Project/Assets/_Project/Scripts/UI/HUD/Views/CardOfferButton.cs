using UnityEngine.EventSystems;

namespace RetroFPS.UI.HUD
{
    /// <summary>Pointer button whose keyboard/gamepad submit is exclusively handled by CardOfferView.</summary>
    public sealed class CardOfferButton : UnityEngine.UI.Button
    {
        /// <summary>Prevents stale EventSystem focus from overriding the modal's current selection.</summary>
        public override void OnSubmit(BaseEventData eventData) { }
    }
}
