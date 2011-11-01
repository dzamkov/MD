using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.Data
{
    /// <summary>
    /// A value that varies continously in real-time.
    /// </summary>
    public abstract class Feed<T>
    {
        /// <summary>
        /// Gets the current value of this feed.
        /// </summary>
        public abstract T Current { get; }
    }

    /// <summary>
    /// A feed that maintains a manually set value.
    /// </summary>
    public sealed class ControlFeed<T> : Feed<T>
    {
        public ControlFeed(T Initial)
        {
            this._Current = Initial;
        }

        public override T Current
        {
            get
            {
                return this._Current;
            }
        }

        /// <summary>
        /// Sets the current value of the control feed.
        /// </summary>
        public void Set(T Current)
        {
            this._Current = Current;
        }

        private T _Current;
    }

    /// <summary>
    /// A feed that plays a signal with one second being one time unit
    /// </summary>
    public sealed class SignalFeed<T> : Feed<T>, _IAutoTimedSignalFeed
    {
        internal SignalFeed(Signal<T> Source, Feed<double> Rate, double Time)
        {
            this.Source = Source;
            this.Rate = Rate;
            this._Time = Time;
            _AutoTimedSignalFeed.Add(this);
        }

        /// <summary>
        /// The signal source for this feed.
        /// </summary>
        public readonly Signal<T> Source;

        /// <summary>
        /// A feed that gives the rate the source signal is played at.
        /// </summary>
        public readonly Feed<double> Rate;

        public override T Current
        {
            get
            {
                return this._Time < Source.Length ? this.Source[this._Time] : default(T);
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

        void _IAutoTimedSignalFeed.Update(double Time)
        {
            this._Time += Time;
        }

        internal double _Time;
    }

    /// <summary>
    /// Controls signal feeds that are automatically timed by the program.
    /// </summary>
    internal static class _AutoTimedSignalFeed
    {
        /// <summary>
        /// A list of the currently active automatically timed signal feeds.
        /// </summary>
        private static readonly List<_IAutoTimedSignalFeed> _Feeds = new List<_IAutoTimedSignalFeed>();

        /// <summary>
        /// Updates the timing of all automatically timed signal feeds.
        /// </summary>
        public static void Update(double Time)
        {
            foreach (_IAutoTimedSignalFeed feed in _Feeds)
            {
                feed.Update(Time);
            }
        }

        /// <summary>
        /// Marks a signal feed as automatically timed.
        /// </summary>
        public static void Add(_IAutoTimedSignalFeed Feed)
        {
            _Feeds.Add(Feed);
        }

        /// <summary>
        /// Marks a signal feed as not automatically timed.
        /// </summary>
        public static void Remove(_IAutoTimedSignalFeed Feed)
        {
            _Feeds.Remove(Feed);
        }
    }

    /// <summary>
    /// An interface to a signal feed whose timing is based on the program.
    /// </summary>
    internal interface _IAutoTimedSignalFeed
    {
        /// <summary>
        /// Updates the timing of the signal feed.
        /// </summary>
        void Update(double Time);
    }
}
