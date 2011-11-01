using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MD.UI
{
    /// <summary>
    /// A gradient that maps real values to colors.
    /// </summary>
    public sealed class Gradient
    {
        public Gradient(Stop[] Stops)
        {
            this._Stops = Stops;
        }

        /// <summary>
        /// Gets the color for the given value.
        /// </summary>
        public Color GetColor(double Value)
        {
            int l = 0;
            int h = this._Stops.Length - 1;
            Stop low = this._Stops[l];
            Stop high = this._Stops[h];
            if (Value < low.Value)
                return low.Color;
            if (Value > high.Value)
                return high.Color;
            while (h > l)
            {
                int s = (l + h) / 2;
                Stop cur = this._Stops[s];
                if (Value < cur.Value)
                {
                    h = s;
                    high = cur;
                }
                else
                {
                    l = s;
                    low = cur;
                }
            }
            return Color.Mix(low.Color, high.Color, (Value - low.Value) / (high.Value - low.Value));
        }

        /// <summary>
        /// A stop in a color gradient.
        /// </summary>
        public struct Stop
        {
            public Stop(double Value, Color Color)
            {
                this.Value = Value;
                this.Color = Color;
            }

            /// <summary>
            /// The value this stop is for.
            /// </summary>
            public double Value;

            /// <summary>
            /// The color at the stop.
            /// </summary>
            public Color Color;
        }

        private Stop[] _Stops;
    }
}
