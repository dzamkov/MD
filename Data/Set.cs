using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.Data
{
    /// <summary>
    /// A set of items compared with the given equality defintion. Unless stated otherwise, a set can be
    /// assumed to be mutable.
    /// </summary>
    public interface Set<T, TEquality>
        where TEquality : Equality<T>
    {
        /// <summary>
        /// Tests wether the given item is in this set.
        /// </summary>
        bool In(T Item);

        /// <summary>
        /// Gets the items in this set, or null if the set can not be enumerated.
        /// </summary>
        IEnumerable<T> Items { get; }
    }

    /// <summary>
    /// A set containing no items.
    /// </summary>
    public class NullSet<T, TEquality> : Set<T, TEquality>
        where TEquality : Equality<T>
    {
        private NullSet()
        {

        }

        /// <summary>
        /// The only instance of this class.
        /// </summary>
        public static readonly NullSet<T, TEquality> Instance = new NullSet<T, TEquality>();

        /// <summary>
        /// The items for the instances of this class (stored as a global to reduce allocation costs for the Items property).
        /// </summary>
        private static readonly T[] _Items = new T[0];

        public bool In(T Item)
        {
            return false;
        }

        public IEnumerable<T> Items
        {
            get
            {
                return _Items;
            }
        }
    }

    /// <summary>
    /// A set containing a single item.
    /// </summary>
    public class SingletonSet<T, TEquality> : Set<T, TEquality>
        where TEquality : Equality<T>
    {
        public SingletonSet(T Item)
        {
            this.Item = Item;
        }

        /// <summary>
        /// The only item in this set.
        /// </summary>
        public readonly T Item;

        public bool In(T Item)
        {
            return default(TEquality).Equal(this.Item, Item);
        }

        public IEnumerable<T> Items
        {
            get
            {
                return new T[] { this.Item };
            }
        }
    }
}
