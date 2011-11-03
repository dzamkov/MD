using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using SysPath = System.IO.Path;
using SysFile = System.IO.File;

namespace MD
{

    /// <summary>
    /// A file path on the file system.
    /// </summary>
    public struct Path
    {
        public Path(string Path)
        {
            this._Path = Path;
        }

        /// <summary>
        /// Gets the path for a file or folder in the directory for this path.
        /// </summary>
        public Path this[string File]
        {
            get
            {
                return this._Path + SysPath.DirectorySeparatorChar + File;
            }
        }

        /// <summary>
        /// Gets the parent directory of this path. If this is a root directory, the same path is returned.
        /// </summary>
        public Path Parent
        {
            get
            {
                return SysPath.GetDirectoryName(this) ?? this;
            }
        }

        /// <summary>
        /// Gets the name of the file or folder at this path.
        /// </summary>
        public string Name
        {
            get
            {
                return SysPath.GetFileName(this);
            }
        }

        /// <summary>
        /// Reads the contents of the file at this path, or returns null if not possible.
        /// </summary>
        public string Contents
        {
            get
            {
                using (TextReader tr = new StreamReader(this))
                {
                    return tr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Gets the files (and folders) in the directory at this path.
        /// </summary>
        public IEnumerable<Path> Subfiles
        {
            get
            {
                IEnumerable<string> files = Directory.GetFiles(this);
                IEnumerable<string> folders = Directory.GetDirectories(this);
                return
                    from file in files.Concat(folders)
                    select (Path)file;
            }
        }

        /// <summary>
        /// Gets wether there is a file or directory at this path.
        /// </summary>
        public bool Exists
        {
            get
            {
                return Directory.Exists(this) || SysFile.Exists(this);
            }
        }

        /// <summary>
        /// Gets wether there is a directory at this path.
        /// </summary>
        public bool DirectoryExists
        {
            get
            {
                return Directory.Exists(this);
            }
        }

        /// <summary>
        /// Gets wether there is a file at this path.
        /// </summary>
        public bool FileExists
        {
            get
            {
                return SysFile.Exists(this);
            }
        }

        /// <summary>
        /// Insures a directory exists at this path. Returns true if a new directory was created.
        /// </summary>
        public bool MakeDirectory()
        {
            if (this.DirectoryExists)
            {
                return false;
            }
            if (this.FileExists)
            {
                this.Delete();
            }
            this.Parent.MakeDirectory();
            Directory.CreateDirectory(this);
            return true;
        }

        /// <summary>
        /// Deletes whatever is at this path if anything. Returns false if nothing exists at this path.
        /// </summary>
        public bool Delete()
        {
            if (this.FileExists)
            {
                SysFile.Delete(this);
                return true;
            }
            if (this.DirectoryExists)
            {
                Directory.Delete(this);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets or sets the path for the working directory.
        /// </summary>
        public static Path WorkingDirectory
        {
            get
            {
                return Environment.CurrentDirectory;
            }
            set
            {
                Environment.CurrentDirectory = value;
            }
        }

        public static implicit operator string(Path Path)
        {
            return Path._Path;
        }

        public static implicit operator Path(string Path)
        {
            return new Path(Path);
        }

        private string _Path;
    }
}