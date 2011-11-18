namespace MD

open System
open System.IO

/// A path on the filesystem.
type public Path =
    struct
        
        /// The source string that defines this path.
        val public Source : string

        new (source : string) = { Source = source }

        /// Gets or sets the current working directory for the application.
        static member WorkingDirectory
            with get () : Path = new Path (Environment.CurrentDirectory)
            and set (x : Path) = (Environment.CurrentDirectory <- x.Source)

        /// Gets the path for a sub-file within this path.
        static member (+) (x : Path, file : string) = Path.Combine (x.Source, file)

        /// Gets the name of the file or directory at this path.
        member this.Name = Path.GetFileName this.Source

        /// Gets the path for the parent directory of this path, or returns this path if it is a root path.
        member this.Parent = 
            match Path.GetDirectoryName this.Source with
            | null -> this
            | x -> new Path (x)

        /// Gets the extension of this path, or null if there is none.
        member this.Extension =
            match this.Source.LastIndexOf '.' with
            | -1 -> null
            | x -> this.Source.Substring x

        /// Gets wether this path exists on the file system as a directory.
        member this.DirectoryExists = Directory.Exists this.Source

        /// Gets wether this path exists on the file system as a file.
        member this.FileExists = File.Exists this.Source

        /// Gets wether this path exists on the file system.
        member this.Exists = this.DirectoryExists || this.FileExists

        /// Gets the paths for the files and folders in the directory at this path.
        member this.Subfiles = Directory.EnumerateFileSystemEntries this.Source |> Seq.map (fun x -> new Path (x))

        /// Ensures a directory exists at this path by deleting files and creating directories as needed. Returns wether any
        /// file system modifications were made.
        member this.MakeDirectory () =
            if this.DirectoryExists then false
            else
                if this.FileExists then File.Delete this.Source
                this.Parent.MakeDirectory () |> ignore
                Directory.CreateDirectory this.Source |> ignore
                true

        override this.ToString () = this.Source
    end