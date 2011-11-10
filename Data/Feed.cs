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
    public sealed class SignalFeed<T> : Feed<T>
    {
        internal SignalFeed(Signal<T> Source, Feed<double> Rate, double Time)
        {
            this.Source = Source;
            this.Rate = Rate;
            this._Time = Time;
            this._RetractUpdate = Program.RegisterUpdate(delegate(double time) { this._Time += time; });
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

        internal RetractHandler _RetractUpdate;
        internal double _Time;
    }
}
