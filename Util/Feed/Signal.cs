using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A source for a dynamic, time-dependant value.
    /// </summary>
    public interface SignalFeed<T>
    {
        /// <summary>
        /// Gets the current value of the feed.
        /// </summary>
        T Current { get; }

        /// <summary>
        /// Gets an event feed that gives an event for each change in this feed, or null if this feed is not discrete (changes continously).
        /// </summary>
        /// <remarks>During an event in this feed, the signal feed will have its new value.</remarks>
        EventFeed<Change<T>> Delta { get; }
    }

    /// <summary>
    /// Contains information about a change in a signal feed.
    /// </summary>
    public struct Change<T>
    {
        public Change(T Old, T New)
        {
            this.Old = Old;
            this.New = New;
        }

        /// <summary>
        /// The old value of the feed.
        /// </summary>
        public T Old;

        /// <summary>
        /// The new value of the feed.
        /// </summary>
        public T New;
    }

    /// <summary>
    /// A signal feed that maintains a manually-set value.
    /// </summary>
    public sealed class ControlSignalFeed<T> : SignalFeed<T>
    {
        public ControlSignalFeed(T Initial)
        {
            this._Current = Initial;
        }

        /// <summary>
        /// Gets or sets the current value of this controlled signal feed.
        /// </summary>
        public T Current
        {
            get
            {
                lock (this)
                {
                    return this._Current;
                }
            }
            set
            {
                ControlEventFeed<Change<T>> delta;
                Change<T> change;
                lock (this)
                {
                    change = new Change<T>(this._Current, value);
                    delta = this._Delta;
                    this._Current = value;
                }
                if (delta != null)
                {
                    delta.Fire(change);
                }
            }
        }

        public EventFeed<Change<T>> Delta
        {
            get
            {
                // Do not create a delta feed until requested.
                if (this._Delta == null)
                {
                    // Use a locked section to ensure only one delta feed is created with multithreaded code.
                    lock (this)
                    {
                        if (this._Delta == null)
                        {
                            this._Delta = new ControlEventFeed<Change<T>>();
                        }
                    }
                }
                return this._Delta;
            }
        }

        private T _Current;
        private volatile ControlEventFeed<Change<T>> _Delta;
    }
}
