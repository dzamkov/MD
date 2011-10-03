using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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
        public static void Main(string[] Args)
        {
            Audio.Initialize();
            Audio.Output(SineSignal.Instance.Dilate(1.0 / 880.0).Play(0.0));

            DateTime lasttime = DateTime.Now;
            while (true)
            {
                DateTime curtime = DateTime.Now;
                double updatetime = (curtime - lasttime).Milliseconds / 1000.0;
                lasttime = curtime;

                Audio.Update(updatetime);
                Application.DoEvents();
            }
        }
    }
}
