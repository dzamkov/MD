module public MD.Program

open MD.Codec
open System

// Ensure System.Core is loaded for debugging
#if DEBUG
System.Linq.Enumerable.Count([]) |> ignore
#endif

[<EntryPoint>]
let main args =
    let wd = Path.WorkingDirectory
    let pd = wd + "Plugins"
    let plugins = Plugin.Enumerate pd
    for plugin in plugins do
        plugin.Load () |> ignore

    let testfile = new Path (@"N:\\Music\\Me\\57.mp3")
    match Container.Load testfile with
    | Some (container, context) -> 
        let contentID = ref 0
        context.NextFrame contentID |> ignore
        Console.ReadKey() |> ignore
    | _ -> ()
    0