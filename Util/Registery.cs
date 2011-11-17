using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A data structure that maintains a singly-linked list of items.
    /// </summary>
    public class Registery<T>
    {
        public Registery()
        {

        }

        /// <summary>
        /// Gets the items in this registery.
        /// </summary>
        public IEnumerable<Item> Items
        {
            get
            {
                Item cur = this._First;
                while (cur != null)
                {
                    yield return cur;
                    cur = cur.Next;
                }
            }
        }

        /// <summary>
        /// Adds an item to the registery.
        /// </summary>
        public Item Add(T Value)
        {
            return this._First = new Item(Value, this._First);
        }

        /// <summary>
        /// Removes the given item from this registery.
        /// </summary>
        public void Remove(Item Item)
        {
            Item prev = null;
            Item cur = this._First;
            while (cur != null)
            {
                if (Item == cur)
                {
                    if (prev == null)
                        this._First = cur.Next;
                    else
                        prev.Next = cur.Next;
                    return;
                }
                prev = cur;
                cur = cur.Next;
            }
        }

        /// <summary>
        /// Removes all items that satisfy the given predicate.
        /// </summary>
        public void Remove(Predicate<Item> Predicate)
        {
            Item prev = null;
            Item cur = this._First;
            while (cur != null)
            {
                if (Predicate(cur))
                {
                    cur = cur.Next;
                    if (prev == null)
                        this._First = cur;
                    else
                        prev.Next = cur;
                }
                else
                {
                    prev = cur;
                    cur = cur.Next;
                }
            }
        }

        /// <summary>
        /// An item within a registery.
        /// </summary>
        public class Item
        {
            public Item(T Value, Item Next)
            {
                this.Value = Value;
                this.Next = Next;
            }

            /// <summary>
            /// The value for this item.
            /// </summary>
            public T Value;

            /// <summary>
            /// The next item in the registery.
            /// </summary>
            public Item Next;
        }

        private Item _First;
    }
}
