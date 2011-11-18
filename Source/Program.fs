module public MD.Program

open System

// Ensure System.Core is loaded for debugging
#if DEBUG
System.Linq.Enumerable.Count([]) |> ignore
#endif

[<EntryPoint>]
let main args =
    Console.ReadKey() |> ignore
    0