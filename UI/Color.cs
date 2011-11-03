using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.UI
{
    /// <summary>
    /// Represents a color.
    /// </summary>
    public struct Color
    {
        public Color(double R, double G, double B)
        {
            this.R = R;
            this.G = G;
            this.B = B;
        }

        /// <summary>
        /// Mixes color A with color B by the given amount between 0.0 and 1.0.
        /// </summary>
        public static Color Mix(Color A, Color B, double Amount)
        {
            double af = 1.0 - Amount;
            double bf = Amount;
            return new Color(
                A.R * af + B.R * bf,
                A.G * af + B.G * bf,
                A.B * af + B.B * bf);
        }

        /// <summary>
        /// Writes the color (in 32 bit argb format) to the given memory location.
        /// </summary>
        public unsafe void Write(int* Ptr)
        {
            const int a = 255;
            int r = (int)(this.R * 255.9);
            int g = (int)(this.G * 255.9);
            int b = (int)(this.B * 255.9);
            *Ptr = (a << 24) | (r << 16) | (g << 8) | b;
        }

        public static implicit operator System.Drawing.Color(Color A)
        {
            return System.Drawing.Color.FromArgb(255,
                (int)(A.R * 255.9),
                (int)(A.G * 255.9),
                (int)(A.B * 255.9));
        }

        /// <summary>
        /// The red component of the color.
        /// </summary>
        public double R;

        /// <summary>
        /// The green component of the color.
        /// </summary>
        public double G;

        /// <summary>
        /// The blue component of the color.
        /// </summary>
        public double B;
    }
}
