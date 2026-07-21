using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.UI
{
    // A rounded card with a bold title — the outer grouping box used throughout the Hub.
    public class MTUIPanel : VisualElement
    {
        public Label TitleLabel { get; }

        public MTUIPanel(string title)
        {
            MTUIStyle.ApplyRoundedBox(this, 12);
            MTUIStyle.ApplyPadding(this, 10, 10);
            MTUIStyle.ApplyMargin(this, 6, 6);

            style.backgroundColor = MTUIColors.PanelBackground;
            MTUIStyle.ApplyBorderColor(this, MTUIColors.NeutralBorder);

            TitleLabel = new Label(title);
            TitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            TitleLabel.style.fontSize = 14;
            TitleLabel.style.marginBottom = 6;
            TitleLabel.style.whiteSpace = WhiteSpace.Normal;

            if (!string.IsNullOrEmpty(title))
                Add(TitleLabel);
            else
                TitleLabel.style.display = DisplayStyle.None;
        }
    }
}
