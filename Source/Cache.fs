namespace MD

open System
open System.Collections.Generic

/// A store of data or information that is indexed by a key and used for performance 
/// reasons. Cache items are explicitly added and queried but implicitly removed. Keys should
/// be paired with unique items so that there is only one potential item for each key.
[<AbstractClass>]
type Cache<'a, 'b when 'a : equality> () =
    
    /// Tries getting the datum with the given key, or returns None if it is not found in the cache.
    abstract member Fetch : key : 'a -> 'b option

    /// Submits an item to the cache. Note that this will fail if an item with the given key is
    /// already in the cache.
    abstract member Submit : key : 'a * item : 'b -> unit

/// A cache that automatically removes items when they are no longer being used. All items must be
/// completely managed (in order to be freed with garbage collection).
[<Sealed>]
type AutoCache<'a, 'b when 'a : equality> () =
    inherit Cache<'a, 'b> ()
    let items = new Dictionary<'a, WeakReference> ()

    override this.Fetch key =
        let mutable itemRef = null
        if items.TryGetValue (key, &itemRef) then
            if itemRef.IsAlive then
                Some (itemRef.Target :?> 'b)
            else
                items.Remove key |> ignore
                None
        else None

    override this.Submit (key, item) =
        items.Add (key, new WeakReference (item :> obj))

/// A cache that removes items during explicit collection cycles at regular intervals.
type CyclicCache<'a, 'b when 'a : equality> (remove : 'b -> unit) =
    inherit Cache<'a, 'b> ()
    let items = new Dictionary<'a, CyclicCacheItem<'b>> ()

    override this.Fetch key =
        let mutable item = Unchecked.defaultof<CyclicCacheItem<'b>>
        if items.TryGetValue (key, &item) then
            item.Used <- true
            Some (item.Item)
        else None

    override this.Submit (key, item) =
        items.Add (key, { Item = item; Used = true })

    /// The function called on items as they are removed.
    member this.Remove = remove

    /// Removes all items that were not used (fetched or submitted) since the last call to Collect.
    member this.Collect () =
        let toRemove = new List<'a> ()
        for kvp in items do
            let value = kvp.Value
            if not value.Used then
                remove value.Item
                toRemove.Add kvp.Key
            else
                value.Used <- false
        for key in toRemove do
            items.Remove key |> ignore

/// The internal representation of an item in a Cyclic cache.
and CyclicCacheItem<'b> = {
        Item : 'b
        mutable Used : bool
    }

/// A cache that removes items (in order of age and disuse) when explicitly requested. 
type ManualCache<'a, 'b when 'a : equality> (remove : 'b -> unit) =
    inherit Cache<'a, 'b> ()
    let items = new LinkedList<'a * 'b> ()
    let index = new Dictionary<'a, LinkedListNode<'a * 'b>> ()

    override this.Fetch key =
        let mutable node = null
        if index.TryGetValue (key, &node) then

            // Bring the node to the front of the items list to delay its collection.
            items.Remove node
            items.AddFirst node

            let _, value = node.Value
            Some value
        else None

    override this.Submit (key, item) =
        let node = new LinkedListNode<'a * 'b> ((key, item))
        items.AddFirst node
        index.Add (key, node)

    /// Removes the given amount of items from this cache. Items will be removed in the order
    /// of the last time they fetched.
    member this.Collect count =
        let mutable count = count
        let mutable node = items.Last
        while count > 0 && node <> null do
            let next = node.Previous
            let key, value = node.Value
            index.Remove key |> ignore
            items.Remove node
            remove value
            node <- next