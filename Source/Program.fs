module public MD.Program

open MD.OpenTK
open System

// Ensure System.Core is loaded for debugging
#if DEBUG
System.Linq.Enumerable.Count([]) |> ignore
#endif

type TestA () =
    class
    end

type TestB<'a> () =
    inherit TestA ()

[<EntryPoint>]
let main args =
    let wd = Path.WorkingDirectory
    let pd = wd + "Plugins"
    let plugins = Plugin.Enumerate pd
    for plugin in plugins do
        plugin.Load () |> ignore

    let win = new Window ()
    win.Run ()

    0