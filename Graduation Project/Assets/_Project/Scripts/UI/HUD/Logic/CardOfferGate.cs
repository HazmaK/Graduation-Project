using RetroFPS.Run.Cards;

namespace RetroFPS.UI.HUD
{
    /// <summary>Delayed, single-use card selection independent of presentation.</summary>
    public sealed class CardOfferGate
    {
        /// <summary>Offer input readiness.</summary>
        public enum GateState { Closed, Arming, Ready }
        private CardOffer offer;
        private double remaining;
        private int selection;
        private GateState state;
        /// <summary>Current readiness.</summary>
        public GateState State => state;
        /// <summary>Selected card index; Count selects refusal.</summary>
        public int Selection => selection;
        /// <summary>Number of offered cards.</summary>
        public int CardCount => offer.Count;
        /// <summary>Opens with first card selected and a fresh 400 ms guard.</summary>
        public void Open(CardOffer value) { offer = value; selection = 0; remaining = 0.4d; state = GateState.Arming; }
        /// <summary>Advances the guard without allocating.</summary>
        public void Tick(double unscaledDeltaTime)
        {
            if (state != GateState.Arming || double.IsNaN(unscaledDeltaTime) || unscaledDeltaTime <= 0d) return;
            remaining -= unscaledDeltaTime;
            if (remaining <= 0d) state = GateState.Ready;
        }
        /// <summary>Wraps left or right over cards and refusal.</summary>
        public void Move(int direction)
        {
            if (state == GateState.Closed || direction == 0) return;
            selection = (selection + (direction < 0 ? offer.Count : 1)) % (offer.Count + 1);
        }
        /// <summary>Selects a hovered element, clamped to the available choices.</summary>
        public void Select(int index)
        {
            if (state != GateState.Closed) selection = index < 0 ? 0 : index > offer.Count ? offer.Count : index;
        }
        /// <summary>Consumes exactly one armed confirmation; null represents refusal.</summary>
        public bool TryConfirm(out CurseBlessingDefinition chosenOrNull)
        {
            chosenOrNull = null;
            if (state != GateState.Ready) return false;
            if (selection < offer.Count) chosenOrNull = offer.Get(selection);
            Close();
            return true;
        }
        /// <summary>Closes without resolving the gameplay offer.</summary>
        public void Close() { state = GateState.Closed; offer = default; selection = 0; remaining = 0d; }
    }
}
