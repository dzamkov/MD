using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A source for discrete events.
    /// </summary>
    public interface EventFeed<T>
    {
        /// <summary>
        /// Registers a callback to be called whenever an event occurs in this feed. Unless otherwise noted, the callback may be called by any thread.
        /// </summary>
        /// <returns>An action that will unregister the callback, or null if the callback will never be called. Note that this is not guranteed to
        /// stop the callback from being called; it only serves to inform the feed that it no longer needs to call the callback.</returns>
        RetractAction Register(Action<T> Callback);
    }

    /// <summary>
    /// An event feed that never fires.
    /// </summary>
    public sealed class NullEventFeed<T> : EventFeed<T>
    {
        private NullEventFeed()
        {

        }

        /// <summary>
        /// The only instance of this class.
        /// </summary>
        public static readonly NullEventFeed<T> Instance = new NullEventFeed<T>();

        public RetractAction Register(Action<T> Callback)
        {
            return null;
        }
    }

    /// <summary>
    /// A signal feed that gives manually-fired events.
    /// </summary>
    public sealed class ControlEventFeed<T> : EventFeed<T>
    {
        public ControlEventFeed()
        {

        }

        /// <summary>
        /// Causes this event feed to fire with the given value.
        /// </summary>
        public void Fire(T Value)
        {
            Action<T> callback = this._Callback; // Copying the callback ensures it is not changed by another thread.
            if (callback != null)
            {
                callback(Value);
            }
        }

        public RetractAction Register(Action<T> Callback)
        {
            lock (this)
            {
                this._Callback += Callback;
            }
            return delegate
            {
                lock (this)
                {
                    this._Callback -= Callback;
                }
            };
        }

        private volatile Action<T> _Callback;
    }

    /// <summary>
    /// An event feed that produces tags events from a source feed.
    /// </summary>
    public sealed class TaggedEventFeed<TTag, T> : EventFeed<Tagged<TTag, T>>
    {
        public TaggedEventFeed(EventFeed<T> Source, TTag Tag)
        {
            this.Source = Source;
            this.Tag = Tag;
        }

        /// <summary>
        /// The source event feed for this event feed.
        /// </summary>
        public readonly EventFeed<T> Source;

        /// <summary>
        /// The tag applied by this event feed.
        /// </summary>
        public readonly TTag Tag;

        public RetractAction Register(Action<Tagged<TTag, T>> Callback)
        {
            return this.Source.Register(delegate(T Source)
            {
                Callback(new Tagged<TTag, T>(this.Tag, Source));
            });
        }
    }

    /// <summary>
    /// A tagged event in an event feed.
    /// </summary>
    public struct Tagged<TTag, T>
    {
        public Tagged(TTag Tag, T Event)
        {
            this.Tag = Tag;
            this.Event = Event;
        }

        /// <summary>
        /// TThe tag for the event.
        /// </summary>
        public TTag Tag;

        /// <summary>
        /// The value of the event.
        /// </summary>
        public T Event;
    }
}
