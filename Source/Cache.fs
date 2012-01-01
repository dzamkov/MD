namespace MD

open System
open System.Collections.Generic

/// A cached mapping that creates and stores items that are accessed frequently and automatically
/// removes items that are not used for a while.
[<AbstractClass>]
type Cache<'a, 'b when 'a : equality> (create : 'a -> 'b, delete : 'b -> unit) =
    inherit Map<'a, 'b> ()

    /// Gets the default function used to create an item (for a certain parameter) for the cache.
    member this.Create = create

    /// Gets the function used to delete an item when no longer used.
    member this.Delete = delete

    /// Manually submits an item to the cache. The item should not already exist.
    abstract member Submit : 'a * 'b -> unit

    /// Looks up an item in the cache, returning None if it has not yet been submitted.
    abstract member Fetch : 'a -> 'b option

    override this.Get param =
        match this.Fetch param with
        | Some item -> item
        | None ->
            let item = create param
            this.Submit (param, item)
            item

/// A cache that automatically removes items when they are no longer being used. All items must be
/// completely managed (in order to be freed with garbage collection) and can not have a custom delete function.
[<Sealed>]
type AutoCache<'a, 'b when 'a : equality> (create : 'a -> 'b) =
    inherit Cache<'a, 'b> (create, ignore)
    let items = new Dictionary<'a, WeakReference> ()

    override this.Submit (param, item) =
        items.Add (param, new WeakReference (item))

    override this.Fetch param =
        let mutable itemRef = null
        if items.TryGetValue (param, &itemRef) && itemRef.IsAlive 
        then Some (itemRef.Target :?> 'b)
        else None

    override this.Get param =
        let create = this.Create
        let mutable itemRef = null
        if items.TryGetValue (param, &itemRef) then
            if itemRef.IsAlive 
            then itemRef.Target :?> 'b
            else
                let item = create param
                itemRef.Target <- item
                item
        else 
            let item = create param
            items.Add (param, new WeakReference (item))
            item

/// A cache that removes items during explicit collection cycles at regular intervals.
type CyclicCache<'a, 'b when 'a : equality> (create : 'a -> 'b, delete : 'b -> unit) =
    inherit Cache<'a, 'b> (create, delete)
    let items = new Dictionary<'a, CyclicCacheItem<'b>> ()

    /// Removes all items that were not used (fetched or submitted) since the last call to Collect.
    member this.Collect () =
        let delete = this.Delete
        let toRemove = new List<'a> ()
        for kvp in items do
            let value = kvp.Value
            if not value.Used then
                delete value.Item
                toRemove.Add kvp.Key
            else
                value.Used <- false
        for key in toRemove do
            items.Remove key |> ignore

    override this.Submit (param, item) =
        items.Add (param, { Item = item; Used = true })

    override this.Fetch param =
        let mutable item = Unchecked.defaultof<CyclicCacheItem<'b>>
        if items.TryGetValue (param, &item) then
            item.Used <- true
            Some item.Item
        else None

/// The internal representation of an item in a Cyclic cache.
and CyclicCacheItem<'b> = {
        Item : 'b
        mutable Used : bool
    }

/// A cache that removes items (in order of age and disuse) when explicitly requested. 
type ManualCache<'a, 'b when 'a : equality> (create : 'a -> 'b, delete : 'b -> unit) =
    inherit Cache<'a, 'b> (create, delete)
    let items = new LinkedList<'a * 'b> ()
    let index = new Dictionary<'a, LinkedListNode<'a * 'b>> ()

    /// Gets the amount of items in this cache.
    member this.Size = items.Count

    /// Removes the given amount of items from this cache. Items will be removed in the order
    /// of the last time they were fetched.
    member this.Collect count =
        let delete = this.Delete
        let mutable count = count
        let mutable node = items.Last
        while count > 0 && node <> null do
            let next = node.Previous
            let key, value = node.Value
            index.Remove key |> ignore
            items.Remove node
            delete value
            node <- next

    override this.Submit (param, item) =
        let node = new LinkedListNode<'a * 'b> ((param, item))
        items.AddFirst node
        index.Add (param, node)

    override this.Fetch param =
        let mutable node = null
        if index.TryGetValue (param, &node) then

            // Bring the node to the front of the items list to delay its collection.
            items.Remove node
            items.AddFirst node

            let _, item = node.Value
            Some item
        else None