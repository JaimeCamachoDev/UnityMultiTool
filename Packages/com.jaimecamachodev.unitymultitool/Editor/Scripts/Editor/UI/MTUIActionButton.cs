using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.UI
{
    // A rounded button with hover-scale and click-punch feedback, colored blue by
    // default (pass neutral colors for a "secondary" style button).
    public class MTUIActionButton : VisualElement
    {
        readonly Label labelElement;
        readonly Action onClick;

        public MTUIActionButton(
            string label,
            Action onClick,
            Color? backgroundColor = null,
            Color? borderColor = null,
            Color? textColor = null,
            TextAnchor alignment = TextAnchor.MiddleCenter
        )
        {
            this.onClick = onClick;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = alignment == TextAnchor.MiddleLeft ? Justify.FlexStart : Justify.Center;

            MTUIStyle.ApplyRoundedBox(this, 8);
            MTUIStyle.ApplyPadding(this, 6, 9);
            MTUIStyle.ApplyMargin(this, 4, 3);
            MTUIStyle.ApplyScaleAndBackgroundTransition(this);
            MTUIStyle.RegisterHoverScale(this, 1.02f);

            SetColors(
                backgroundColor ?? MTUIColors.BlueBackground,
                borderColor ?? MTUIColors.BlueBorder,
                textColor ?? MTUIColors.BlueText
            );

            labelElement = new Label(label);
            labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelElement.style.fontSize = 11;

            Add(labelElement);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                MTUIStyle.ClickPunch(this, 0.97f, 1.02f);
                this.onClick?.Invoke();
                evt.StopPropagation();
            });
        }

        public void SetColors(Color backgroundColor, Color borderColor, Color textColor)
        {
            style.backgroundColor = backgroundColor;
            MTUIStyle.ApplyBorderColor(this, borderColor);

            if (labelElement != null)
                labelElement.style.color = textColor;
        }

        public void SetAvailable(bool available, string unavailableLabel = null)
        {
            base.SetEnabled(available);
            pickingMode = available ? PickingMode.Position : PickingMode.Ignore;
            style.opacity = available ? 1f : 0.45f;

            if (!available && unavailableLabel != null)
                labelElement.text = unavailableLabel;
        }
    }
}
