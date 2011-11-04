using System;
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
        /// Program main entry point.
        /// </summary>
        [STAThread]
        public static void Main(string[] Args)
        {
            Application.EnableVisualStyles();
            Audio.Initialize();


            Path file = "F:\\Music\\Me\\57.mp3";
            MP3Stream stream = new MP3Stream(new NativeStream(file));
            stream.Initialize();

            Feed<double> pitch = Signal.Time.Map(x => 1.0).Play();
            Feed<long> pos = Audio.Output(
                new SplitStream<Stero<int>, byte, Stero16Compound>(stream),
                OpenTK.Audio.OpenAL.ALFormat.Stereo16,
                44100, pitch);

            PlotForm pf = new PlotForm();
            pf.Show();

            DateTime lasttime = DateTime.Now;
            while (pf.Visible)
            {
                DateTime curtime = DateTime.Now;
                double updatetime = (curtime - lasttime).Milliseconds / 1000.0;
                lasttime = curtime;

                pf.Update(updatetime);
                Console.WriteLine(pos.Current);

                _AutoTimedSignalFeed.Update(updatetime);
                Audio.Update(updatetime);
                Application.DoEvents();
            }
        }
    }
}
