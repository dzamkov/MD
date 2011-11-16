using System;
using System.Collections.Generic;
using System.Linq;

using MD.Data;

namespace MD.UI
{
    /// <summary>
    /// Contains functions related to feeds.
    /// </summary>
    public static class Feed
    {
        /// <summary>
        /// Gets an event feed that never fires.
        /// </summary>
        public static EventFeed<T> Null<T>()
        {
            return NullEventFeed<T>.Instance;
        }

        /// <summary>
        /// Maps events in this event feed based on the given mapping function.
        /// </summary>
        public static EventFeed<T> Map<TSource, T>(this EventFeed<TSource> Source, Func<TSource, T> Map)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Maps values in a signal feed based on the given mapping function.
        /// </summary>
        public static SignalFeed<T> Map<TSource, T>(this SignalFeed<TSource> Source, Func<TSource, T> Map)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Filters events in this event feed that don't satisfy the given filter function.
        /// </summary>
        public static EventFeed<T> Filter<T>(this EventFeed<T> Source, Func<T, bool> Filter)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Strips data from this event feed so that it only gives information about when events occur.
        /// </summary>
        public static EventFeed<Void> Strip<T>(this EventFeed<T> Source)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an event feed that fires an event whenever this feed rises (changes from false to true), or returns null if the feed varies continuously.
        /// </summary>
        public static EventFeed<Void> Rising(this SignalFeed<bool> Feed)
        {
            EventFeed<Change<bool>> delta = Feed.Delta;
            if (delta != null)
            {
                return delta.Filter(x => x.New == true).Strip();
            }
            return null;
        }

        /// <summary>
        /// Gets an event feed that fires an event whenever this feed falls (changes from true to false), or returns null if the feed varies continuously.
        /// </summary>
        public static EventFeed<Void> Falling(this SignalFeed<bool> Feed)
        {
            EventFeed<Change<bool>> delta = Feed.Delta;
            if (delta != null)
            {
                return delta.Filter(x => x.New == false).Strip();
            }
            return null;
        }
    }

    /// <summary>
    /// A source of dynamic, time-dependant information of a certain type.
    /// </summary>
    public interface Feed<T>
    {

    }

    /// <summary>
    /// A dynamic, time-dependant value.
    /// </summary>
    public interface SignalFeed<T> : Feed<T>
    {
        /// <summary>
        /// Gets the current value of the feed. The value returned by this property should not be affected by future changes in the feed.
        /// </summary>
        T Current { get; }

        /// <summary>
        /// Gets an event feed that gives an event for each discrete change in this feed. This will return null for signal feeds that
        /// change continously, with no discernible moment of change.
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
    /// A source of discrete events.
    /// </summary>
    public interface EventFeed<T> : Feed<T>
    {
        /// <summary>
        /// Registers a callback to be called whenever an event occurs in this feed.
        /// </summary>
        /// <returns>An action that will unregister the callback, or null if the callback will never be called.</returns>
        RetractAction Register(Action<T> Callback);
    }

    /// <summary>
    /// A finite, dynamic, time-dependant set of items of the given type, using the given equality definition.
    /// </summary>
    public interface SetFeed<T, TEquality> : SignalFeed<Set<T, TEquality>>
        where TEquality : Equality<T>
    {

        /// <summary>
        /// Registers a callback to be called for every item currently in the set, and all future items that enter the set. When an item is removed from
        /// a set, the unregister action returned from the callback will be called.
        /// </summary>
        /// <returns>An action that will unregister every item, and will prevent the callback from being 
        /// called for future items that enter the set.</returns>
        RetractAction RegisterMaintain(RegisterItemAction<T> Callback);
    }

    /// <summary>
    /// A function that registers an item in a set using a user-defined method.
    /// </summary>
    /// <returns>An action that will unregister the item, or null if not required.</returns>
    public delegate RetractAction RegisterItemAction<T>(T Item);

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
                return this._Current;
            }
            set
            {
                T old = this._Current;
                if (this._Delta != null)
                {
                    this._Delta.Fire(new Change<T>(old, value));
                }
                this._Current = value;
            }
        }

        public EventFeed<Change<T>> Delta
        {
            get
            {
                // Do not create a delta feed until requested.
                if (this._Delta == null)
                    this._Delta = new ControlEventFeed<Change<T>>();
                return this._Delta;
            }
        }

        private T _Current;
        private ControlEventFeed<Change<T>> _Delta;
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
        /// Causes this event feed to fire an event with the given value.
        /// </summary>
        public void Fire(T Value)
        {
            if (this._Callback != null)
            {
                this._Callback(Value);
            }
        }

        public RetractAction Register(Action<T> Callback)
        {
            this._Callback += Callback;
            return delegate { this._Callback -= Callback; };
        }

        private Action<T> _Callback;
    }
}
