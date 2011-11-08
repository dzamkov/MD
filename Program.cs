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
            Application.EnableVisualStyles();
            AudioOutput audiout = new OpenALOutput();

            // Load ALL THE PLUGINS
            foreach (Plugin plugin in Plugin.Available)
            {
                plugin.Load();
            }

            DateTime lasttime = DateTime.Now;
            while (true)
            {
                DateTime curtime = DateTime.Now;
                double updatetime = (curtime - lasttime).Milliseconds / 1000.0;
                lasttime = curtime;


                _AutoTimedSignalFeed.Update(updatetime);
                audiout.Update(updatetime);
                Application.DoEvents();
            }
        }
    }
}
