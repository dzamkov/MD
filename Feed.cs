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
    /// A feed that plays a signal with one second being one time unit. When the feed reachs the end of the signal,
    /// it will loop back to the beginning.
    /// </summary>
    public sealed class SignalFeed<T> : Feed<T>
    {
        internal SignalFeed(Signal<T> Source, double Time)
        {
            this.Source = Source;
            this._Time = Time;
        }

        /// <summary>
        /// The signal source for this feed.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// The current value of the feed.
        /// </summary>
        public T Current
        {
            get
            {
                return this.Source[this._Time];
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
