using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace MD.UI
{
    /// <summary>
    /// A control that displays a dynamic image.
    /// </summary>
    public abstract class DisplayControl : Control
    {
        public DisplayControl()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// <summary>
        /// Draws the contents of the display control (in 32 bit argb format) to the given buffer.
        /// </summary>
        public abstract unsafe void Draw(int* Ptr, int Width, int Height);

        protected override unsafe void OnPaint(PaintEventArgs e)
        {
            Size size = this.ClientSize;

            // Make sure the buffer exists and is of the right size.
            if (this._Buffer == null)
            {
                this._Buffer = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            }

            // Lock buffer for writing
            BitmapData bd = this._Buffer.LockBits(new System.Drawing.Rectangle(0, 0, size.Width, size.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int* ptr = (int*)bd.Scan0.ToPointer();
            this.Draw(ptr, size.Width, size.Height);

            // Unlock buffer and draw
            this._Buffer.UnlockBits(bd);
            e.Graphics.DrawImageUnscaled(this._Buffer, 0, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this._Buffer != null)
            {
                this._Buffer.Dispose();
                this._Buffer = null;
            }
            this.Refresh();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this._Buffer.Dispose();
            }
        }

        private Bitmap _Buffer;
    }
}
