﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

using MD.Codec;
using MD.Data;
using MD.UI;

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
            Audio.Initialize();

            // Load ALL THE PLUGINS
            foreach (Plugin plugin in Plugin.Available)
            {
                plugin.Load();
            }

            IEnumerable<Codec.Codec> codecs = Codec.Codec.Codecs;

            PlotForm pf = new PlotForm();
            pf.Show();

            DateTime lasttime = DateTime.Now;
            while (pf.Visible)
            {
                DateTime curtime = DateTime.Now;
                double updatetime = (curtime - lasttime).Milliseconds / 1000.0;
                lasttime = curtime;

                pf.Update(updatetime);

                _AutoTimedSignalFeed.Update(updatetime);
                Audio.Update(updatetime);
                Application.DoEvents();
            }
        }
    }
}
