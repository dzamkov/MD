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
        public PlotControl()
        {

        }

        public override unsafe void Draw(byte* Ptr, int Width, int Height, int StridePadding)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Ptr[0] = 255;
                    Ptr[1] = (byte)x;
                    Ptr[2] = (byte)(y * 3);
                    Ptr += 3;
                }
                Ptr += StridePadding;
            }
        }
    }
}
