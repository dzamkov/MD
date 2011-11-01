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
        /// Writes the color (in 24 bit bgr format) to the given memory location.
        /// </summary>
        public unsafe void Write(byte* Ptr)
        {
            byte r = (byte)(this.R * 255.0);
            byte g = (byte)(this.G * 255.0);
            byte b = (byte)(this.B * 255.0);
            Ptr[0] = b;
            Ptr[1] = g;
            Ptr[2] = r;
        }

        public static implicit operator System.Drawing.Color(Color A)
        {
            return System.Drawing.Color.FromArgb(255,
                (int)(A.R * 255.0),
                (int)(A.G * 255.0),
                (int)(A.B * 255.0));
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
