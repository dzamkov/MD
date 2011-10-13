using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Program main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Program main entry point.
        /// </summary>
        [STAThread]
        public static void Main(string[] Args)
        {
            Application.EnableVisualStyles();
            Audio.Initialize();

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "(*.mp3)|*.mp3|All Files (*.*)|*.*";
                ofd.InitialDirectory = "F:\\Music";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    MP3Source source = new MP3Source(ofd.FileName);

                    Spectrogram spec = new Spectrogram(source.Stero16Signal.Map(x => (x.Left * 16384.0) + (x.Right * 16384.0)));
                    spec.Show();
                }
            }

            DateTime lasttime = DateTime.Now;
            while (true)
            {
                DateTime curtime = DateTime.Now;
                double updatetime = (curtime - lasttime).Milliseconds / 1000.0;
                lasttime = curtime;

                _AutoTimedSignalFeed.Update(updatetime);
                Audio.Update(updatetime);
                Application.DoEvents();
            }
        }
    }
}
