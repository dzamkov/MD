using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

namespace Spectrogram
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

                    Signal<double> rate = Signal.Time.Map(x => 1.0 + x * 0.05);
                    Audio.Output<Stero<short>, Stero16Compound>(source.Stero16Signal.Play(0.0, rate.Play()), OpenTK.Audio.OpenAL.ALFormat.Stereo16);
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
