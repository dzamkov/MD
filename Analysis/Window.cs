using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MD.Analysis
{
    /// <summary>
    /// Contains functions related to window functions.
    /// </summary>
    public static class Window
    {
        /// <summary>
        /// Constructs half of a gabor window with the given size and scale such that the first sample in the window
        /// is 1.0 and descends as the index increases.
        /// </summary>
        public static unsafe void CreateHalfGabor(int Size, double Scale, double* Output)
        {
            double iscale = 1.0 / Scale;
            for (int t = 0; t < Size; t++)
            {
                *Output = Math.Exp(-Math.PI * t * iscale);
                Output++;
            }
        }

        /// <summary>
        /// Constructs a gabor window with the given size and scale.
        /// </summary>
        public static unsafe void CreateGabor(int Size, double Scale, double* Output)
        {
            int hsize = Size / 2;
            Window.CreateHalfGabor(hsize, Scale, Output + hsize);

            // Copy the half window  to the beginning in reverse to make the full window
            for (int t = 0; t < hsize; t++)
            {
                Output[t] = Output[Size - t - 1];
            }
        }

        /// <summary>
        /// Applies a window to real data.
        /// </summary>
        /// <param name="Size">The size of the window and data.</param>
        public static unsafe void ApplyReal(int Size, double* Window, double* Data)
        {
            while (Size-- > 0)
            {
                *Data = *Data * *Window;
                Data++;
                Window++;
            }
        }

        /// <summary>
        /// Applies a window to complex data.
        /// </summary>
        /// <param name="Size">The size of the window and data, in samples.</param>
        public static unsafe void ApplyComplex(int Size, double* Window, double* Data)
        {
            while (Size-- > 0)
            {
                double win = *Window;
                *Data = *Data * win;
                Data++;
                *Data = *Data * win;
                Data++;
                Window++;
            }
        }
    }
}
