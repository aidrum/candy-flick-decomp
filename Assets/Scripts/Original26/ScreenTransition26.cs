using System;
namespace CandyFlick.Original26
{
    // BlackScreen.OnGUI 0x2a370 and ScreenSwitcher.OnGUI 0x4f584.
    // These counters advance per OnGUI invocation, including Layout and Repaint.
    public sealed class ScreenTransition26
    {
        public float Alpha { get; private set; } = 1.2f;
        public bool Waiting => next != null;
        public bool Fading { get; private set; } = true;
        bool fadingIn;
        int calls;
        Action next;
        public void Begin(Action action)
        {
            if (Waiting) return;
            Alpha = 0; fadingIn = Fading = true; calls = 0; next = action;
        }
        public bool BeforeGUI()
        {
            if (next == null || ++calls < 20) return false;
            Action action = next; next = null; calls = 0; action();
            return true;
        }
        public void AfterGUI()
        {
            if (!Fading) return;
            Alpha += fadingIn ? .06f : -.06f;
            if (fadingIn && Alpha >= 1.2f) fadingIn = false;
            else if (!fadingIn && Alpha <= 0) Fading = false;
        }
    }
}
