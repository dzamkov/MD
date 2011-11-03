using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MD.UI
{
    /// <summary>
    /// A control that displays a plot.
    /// </summary>
    public class PlotControl : DisplayControl
    {
        public PlotControl(Plot Plot)
        {
            this._Plot = Plot;
        }

        /// <summary>
        /// The view for the horizontal axis.
        /// </summary>
        public AxisView HorizontalView;

        /// <summary>
        /// The view for the vertical axis.
        /// </summary>
        public AxisView VerticalView;

        /// <summary>
        /// Gets the plot being displayed by this control.
        /// </summary>
        public Plot Plot
        {
            get
            {
                return this._Plot;
            }
        }

        public override unsafe void Draw(int* Ptr, int Width, int Height)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    new Color(0.0, 1.0, 0.0).Write(Ptr);
                    Ptr++;
                }
            }
        }

        private Plot _Plot;
    }

    /// <summary>
    /// A transformation between axis values and view values.
    /// </summary>
    public struct AxisView
    {
        public AxisView(double Min, double Max)
        {
            this.Min = Min;
            this.Max = Max;
        }

        /// <summary>
        /// The axis value that corresponds to 0.0 in view values.
        /// </summary>
        public double Min;

        /// <summary>
        /// The axis value that corresponds to 1.0 in view values.
        /// </summary>
        public double Max;

        /// <summary>
        /// Converts from axis values to view values.
        /// </summary>
        public double Project(double Value)
        {
            return (Value - Min) / (Max - Min);
        }

        /// <summary>
        /// Converts from view values to axis values.
        /// </summary>
        public double Unproject(double Value)
        {
            return Value * (Max - Min) + Min;
        }
    }
}
