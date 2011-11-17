using System;
using System.Collections.Generic;
using System.Linq;

using MD.Data;

namespace MD
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
        /// Tags all events from an event feed with a certain value.
        /// </summary>
        public static EventFeed<Tagged<TTag, T>> Tag<TTag, T>(this EventFeed<T> Source, TTag Tag)
        {
            return new TaggedEventFeed<TTag, T>(Source, Tag);
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
}
