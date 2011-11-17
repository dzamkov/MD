using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.UI.Input
{
    /// <summary>
    /// Represents mouse-like input in a two-dimensional coordinate space.
    /// </summary>
    public sealed class Probe
    {
        public Probe(SignalFeed<Point> Position, SignalFeed<bool> Primary, SignalFeed<bool> Secondary, EventFeed<double> Scroll)
        {
            this.Position = Position;
            this.Primary = Primary;
            this.Secondary = Secondary;
            this.Scroll = Scroll;
        }

        /// <summary>
        /// The position of the mouse.
        /// </summary>
        public readonly SignalFeed<Point> Position;

        /// <summary>
        /// Indicates wether the primary button of this mouse is active.
        /// </summary>
        public readonly SignalFeed<bool> Primary;

        /// <summary>
        /// Indicates wether the secondary button of this mouse is active.
        /// </summary>
        public readonly SignalFeed<bool> Secondary;

        /// <summary>
        /// Fires events when the scroll wheel is used.
        /// </summary>
        public readonly EventFeed<double> Scroll;
    }
}