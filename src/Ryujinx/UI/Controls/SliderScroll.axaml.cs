using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace Ryujinx.Ava.UI.Controls
{
    public class SliderScroll : Slider
    {
        protected override Type StyleKeyOverride => typeof(Slider);

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            var newValue = Value + e.Delta.Y * TickFrequency;

            if (newValue < Minimum)
            {
                Value = Minimum;
            }
            else if (newValue > Maximum)
            {
                Value = Maximum;
            }
            else
            {
                Value = newValue;
            }

            e.Handled = true;
        }
    }
}
