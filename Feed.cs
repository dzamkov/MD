using System;
using System.Collections.Generic;
using System.Linq;

namespace Spectrogram
{
    /// <summary>
    /// A value that varies continously in real-time.
    /// </summary>
    public class Feed<T>
    {

    }

    /// <summary>
    /// A feed that plays a signal with one second being one time unit
    /// </summary>
    public sealed class SignalFeed<T> : Feed<T>
    {
        internal SignalFeed(Signal<T> Source, T Default, double Time)
        {
            this.Source = Source;
            this.Default = Default;
            this._Time = Time;
        }

        /// <summary>
        /// The signal source for this feed.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The default value of the feed for when the feed is paused or over.
        /// </summary>
        public readonly T Default;

        /// <summary>
        /// The current value of the feed.
        /// </summary>
        public T Current
        {
            get
            {
                return this._Time < Source.Length ? this.Source[this._Time] : this.Default;
            }
        }

        /// <summary>
        /// The current position of the feed in the signal.
        /// </summary>
        public double Time
        {
            get
            {
                return this._Time;
            }
        }

        internal double _Time;
    }
}
