using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A source for a dynamic collection of items.
    /// </summary>
    public interface CollectionFeed<T>
    {
        /// <summary>
        /// Registers a callback to be called for every item currently in the feed and every item that later enters it. The callback will return an action
        /// to be called when the corresponding item leaves the collection, or when the callback is unregistered.
        /// </summary>
        /// <returns>An action that will unregister the callback, or null if the callback will never be called. Note that this is not guranteed to
        /// stop the callback from being called; it only serves to inform the feed that it no longer needs to call the callback.</returns>
        RetractAction Register(Func<T, RetractAction> Callback);
    }

    /// <summary>
    /// A collection feed that allows items to be manually added or removed.
    /// </summary>
    public sealed class ControlCollectionFeed<T> : CollectionFeed<T>
    {
        public ControlCollectionFeed()
        {
            this._Items = new Registery<T>();
            this._Callbacks = new Registery<Func<T, RetractAction>>();
            this._Relations = new Registery<_Relation>();
        }

        /// <summary>
        /// Adds an item to this collection and returns an action to later remove it.
        /// </summary>
        public RetractAction Add(T Item)
        {
            lock (this)
            {
                var item = this._Items.Add(Item);
                foreach (var callback in this._Callbacks.Items)
                {
                    this._MakeRelation(item, callback);
                }
                return delegate { this._Remove(item); };
            }
        }

        public RetractAction Register(Func<T, RetractAction> Callback)
        {
            lock (this)
            {
                var callback = this._Callbacks.Add(Callback);
                foreach (var item in this._Items.Items)
                {
                    this._MakeRelation(item, callback);
                }
                return delegate { this._Unregister(callback); };
            }
        }

        /// <summary>
        /// Makes a relation between the given item and callback.
        /// </summary>
        private void _MakeRelation(Registery<T>.Item Item, Registery<Func<T, RetractAction>>.Item Callback)
        {
            RetractAction retract = Callback.Value(Item.Value);
            if (retract != null)
            {
                this._Relations.Add(new _Relation(Item, Callback, retract));
            }
        }

        /// <summary>
        /// Removes the given item.
        /// </summary>
        private void _Remove(Registery<T>.Item Item)
        {
            lock (this)
            {
                this._Items.Remove(Item);
                this._Relations.Remove(delegate(Registery<_Relation>.Item item)
                {
                    if (item.Value.Item == Item)
                    {
                        item.Value.Retract();
                        return true;
                    }
                    return false;
                });
            }
        }

        /// <summary>
        /// Unregisters the given callback.
        /// </summary>
        private void _Unregister(Registery<Func<T, RetractAction>>.Item Callback)
        {
            lock (this)
            {
                this._Callbacks.Remove(Callback);
                this._Relations.Remove(delegate(Registery<_Relation>.Item item)
                {
                    if (item.Value.Callback == Callback)
                    {
                        item.Value.Retract();
                        return true;
                    }
                    return false;
                });
            }
        }

        /// <summary>
        /// A relation between an item and a callback.
        /// </summary>
        private class _Relation
        {
            public _Relation(Registery<T>.Item Item, Registery<Func<T, RetractAction>>.Item Callback, RetractAction Retract)
            {
                this.Item = Item;
                this.Callback = Callback;
                this.Retract = Retract;
            }

            /// <summary>
            /// The item for this relation.
            /// </summary>
            public Registery<T>.Item Item;

            /// <summary>
            /// The callback for this relation.
            /// </summary>
            public Registery<Func<T, RetractAction>>.Item Callback;

            /// <summary>
            /// The retract function for the relationship.
            /// </summary>
            public RetractAction Retract;
        }

        private Registery<T> _Items;
        private Registery<Func<T, RetractAction>> _Callbacks;
        private Registery<_Relation> _Relations;
    }
}
