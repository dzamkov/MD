using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.UI
{
    /// <summary>
    /// A visual, interactive display of data on a continous two-dimensional domain.
    /// </summary>
    public abstract class Plot
    {
        public Plot(Rectangle Domain, Axis Horizontal, Axis Vertical)
        {
            this.Domain = Domain;
            this.Horizontal = Horizontal;
            this.Vertical = Vertical;
        }

        /// <summary>
        /// The domain of this plot.
        /// </summary>
        public readonly Rectangle Domain;

        /// <summary>
        /// The units for the horizontal axis of this plot.
        /// </summary>
        public readonly Axis Horizontal;

        /// <summary>
        /// The units for the vertical axis of this plot.
        /// </summary>
        public readonly Axis Vertical;
    }

    /// <summary>
    /// Identifies the units of an axis of a plot.
    /// </summary>
    public enum Axis
    {
        /// <summary>
        /// Time in seconds.
        /// </summary>
        Time,

        /// <summary>
        /// Frequency in hertz.
        /// </summary>
        Frequency,

        /// <summary>
        /// Amplitude.
        /// </summary>
        Amplitude
    }
}
