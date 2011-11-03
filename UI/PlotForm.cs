using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

namespace MD.UI
{
    /// <summary>
    /// A form that displays a plot.
    /// </summary>
    public class PlotForm : Form
    {
        public PlotForm()
        {
            this._PlotControl = new PlotControl(null);
            this._PlotControl.Dock = DockStyle.Fill;
            this.Controls.Add(this._PlotControl);
        }

        /// <summary>
        /// Updates the state of this form by the given amount of time in seconds.
        /// </summary>
        public void Update(double Time)
        {
            this._PlotControl.Refresh();
        }

        private PlotControl _PlotControl;
    }
}
