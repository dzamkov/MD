using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

using MD.Codec;
using MD.Data;
using MD.UI;
using MD.UI.Audio;

namespace MD
{
    /// <summary>
    /// Program main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main program directory to load resources from.
        /// </summary>
        public static Path Directory;

        /// <summary>
        /// Program main entry point.
        /// </summary>
        [STAThread]
        public static void Main(string[] Args)
        {
            Directory = Path.WorkingDirectory;

            // Load ALL THE PLUGINS
            foreach (Plugin plugin in Plugin.Available)
            {
                plugin.Load();
            }

            new Window().Run(60.0);
        }


        /// <summary>
        /// Registers a callback to be called periodically with the time since the last update.
        /// </summary>
        public static RetractAction RegisterUpdate(Action<double> Callback)
        {
            _Update += Callback;
            return delegate { _Update -= Callback; };
        }
        private static Action<double> _Update = null;

        /// <summary>
        /// Updates the state of the program by the given amount of time in seconds. This will call all registered
        /// update callbacks.
        /// </summary>
        public static void Update(double Time)
        {
            if (_Update != null)
            {
                _Update(Time);
            }
        }
    }
}
