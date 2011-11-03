using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MD.UI
{
    /// <summary>
    /// A spectrogram plot.
    /// </summary>
    public class Spectrogram : Plot
    {
        public Spectrogram()
            : base(new Rectangle(0.0, 0.0, 10.0, 10.0), Axis.Time, Axis.Frequency)
        {

        }

    }
}
