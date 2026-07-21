using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.UI
{
    // Muted, wrapping helper text — used for tool descriptions and hint boxes.
    public class MTUIInfoLabel : Label
    {
        public MTUIInfoLabel(string text = null)
        {
            if (!string.IsNullOrEmpty(text)) this.text = text;

            style.whiteSpace = WhiteSpace.Normal;
            style.fontSize = 11;
            style.color = MTUIColors.InfoText;
            style.marginBottom = 6;
        }
    }
}
