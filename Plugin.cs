using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Gives information about a plugin (loaded or unloaded).
    /// </summary>
    public class Plugin
    {
        public Plugin(Type Type, string Name, Func<RetractHandler> Load)
        {
            this.Type = Type;
            this.Name = Name;
            this._Load = Load;
        }

        /// <summary>
        /// The name of the plugin.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The plugin's defining class.
        /// </summary>
        public readonly Type Type;

        /// <summary>
        /// Gets or sets wether this plugin is currently loaded.
        /// </summary>
        public bool Loaded
        {
            get
            {
                return this._Retract != null;
            }
            set
            {
                if (this._Retract == null && value)
                {
                    this._Retract = this._Load();
                }
                if (this._Retract != null && !value)
                {
                    this._Retract();
                    this._Retract = null;
                }
            }
        }

        /// <summary>
        /// Loads this plugin, if it hisn't loaded already.
        /// </summary>
        public RetractHandler Load()
        {
            if (this._Retract != null)
            {
                return null;
            }
            this._Retract = this._Load();
            return delegate
            {
                this._Retract();
                this._Retract = null;
            };
        }

        /// <summary>
        /// Tries loading a plugin from a file. Returns null if a problem occured.
        /// </summary>
        public static Plugin Load(Path File)
        {
            try
            {
                return Load(Assembly.LoadFrom(File));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries loading a plugin from an assembly. Returns null if a problem occured.
        /// </summary>
        public static Plugin Load(Assembly Assembly)
        {
            Module[] modules = Assembly.GetModules();
            foreach (Module mod in modules)
            {
                Type plugin = mod.GetType("Plugin");
                if (plugin != null)
                {
                    string name = null;
                    Func<RetractHandler> load = null;
                    if (Reflection.Get<string>(plugin, "Name", ref name) &&
                        Reflection.Get<Func<RetractHandler>>(plugin, "Load", ref load))
                    {
                        return new Plugin(plugin, name, load);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all the available plugins.
        /// </summary>
        public static IEnumerable<Plugin> Available
        {
            get
            {
                if (_Available == null)
                {
                    _Available = new List<Plugin>();
                    _EnumeratePlugins(Program.Directory["Plugins"], _Available);
                }
                return _Available;
            }
        }
        private static List<Plugin> _Available;

        /// <summary>
        /// Enumerates all plugins in the given directory and outputs them to the given list.
        /// </summary>
        private static void _EnumeratePlugins(Path Directory, List<Plugin> Out)
        {
            foreach (Path f in Directory.Subfiles)
            {
                if (f.FileExists && f.Extension == "dll")
                {
                    string name = f.Name;
                    string[] parts = name.Split('_');

                    if (parts.Length > 0 && parts[0] == "plugin")
                    {
                        string platform = null;
                        if (parts.Length > 1)
                            platform = parts[1];

                        bool shouldload = false;
                        switch (platform)
                        {
                            case "32":
                                shouldload = !Environment.Is64BitProcess;
                                break;
                            case "64":
                                shouldload = Environment.Is64BitProcess;
                                break;
                            default:
                                shouldload = true;
                                break;
                        }

                        if (shouldload)
                        {
                            Plugin pg = Load(f);
                            if (pg != null)
                            {
                                _Available.Add(pg);
                            }
                        }
                    }
                    continue;
                }

                if (f.DirectoryExists)
                {
                    _EnumeratePlugins(f, Out);
                }
            }
        }

        private RetractHandler _Retract;
        private Func<RetractHandler> _Load;
    }
}
