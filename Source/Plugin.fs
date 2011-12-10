namespace MD

open System
open System.Reflection

/// An interface to an application plugin. Contains functions for loading plugins.
[<AbstractClass>]
type Plugin () =

    /// Tries loading a plugin from the given assembly.
    static member Load (assembly : Assembly) =
        assembly.GetModules ()
        |> Seq.tryPick (fun m ->
            match m.GetType("Plugin") with
            | plugin when plugin.BaseType = typeof<Plugin> ->
                match plugin.GetConstructor Type.EmptyTypes with
                | null -> None
                | cons -> Some (cons.Invoke Array.empty :?> Plugin)
            | _ -> None)

    /// Tries loading a plugin from the given file.
    static member Load (file : Path) =
        try
            Assembly.LoadFrom file.Source |> Plugin.Load
        with
        | _ -> None
    
    /// Enumerates the applicable plugins at the given path.
    static member Enumerate (directory : Path)  =
        directory.Subfiles 
        |> Seq.collect (fun file -> 
            if file.DirectoryExists then Plugin.Enumerate file
            elif file.Extension = "dll" then
                match file.Name.LastIndexOf '_' with
                | -1 -> Seq.empty
                | x -> 
                    match file.Name.Substring (0, x) with
                    | "Plugin" -> Plugin.Load file
                    | "Plugin32" when not Environment.Is64BitProcess -> Plugin.Load file
                    | "Plugin64" when Environment.Is64BitProcess -> Plugin.Load file
                    | _ -> None
                    |> Option.toList |> seq
            else Seq.empty)

    /// Gets the user-friendly name of this plugin.
    abstract member Name : string

    /// Gets a description of this plugin.
    abstract member Description : string

    /// Gets the about box information for this plugin, or null if not applicable.
    abstract member About : string
    default this.About = null

    /// Attaches this plugin to the program. Returns a retract to later unload
    /// the plugin.
    abstract member Load : unit -> Retract