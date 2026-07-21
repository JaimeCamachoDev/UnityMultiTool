using UnityEngine;

namespace JaimeCamachoDev.Multitool.UI
{
    // Palette for the Multitool's UI Toolkit components — same visual language as
    // VzFolders' UI Toolkit windows (rounded cards, blue actions, green/red status chips).
    public static class MTUIColors
    {
        public static readonly Color PanelBackground = new(0.16f, 0.16f, 0.16f, 1f);

        public static readonly Color BlueBackground = new(0.18f, 0.28f, 0.42f, 1f);
        public static readonly Color BlueBorder = new(0.45f, 0.65f, 1f, 1f);
        public static readonly Color BlueText = new(0.85f, 0.9f, 1f, 1f);

        public static readonly Color EnabledBackground = new(0.15f, 0.38f, 0.22f, 1f);
        public static readonly Color EnabledBorder = new(0.35f, 0.9f, 0.45f, 1f);
        public static readonly Color EnabledText = new(0.65f, 1f, 0.7f, 1f);

        public static readonly Color NeutralBackground = new(0.25f, 0.25f, 0.25f, 1f);
        public static readonly Color NeutralBorder = new(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color NeutralText = new(0.8f, 0.8f, 0.8f, 1f);

        public static readonly Color InfoText = new(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color HeaderBackground = new(0.11f, 0.12f, 0.15f, 1f);
    }
}
