using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using MD.Data;
using MD.UI.Audio;

namespace MD.UI
{
    /// <summary>
    /// Main program window.
    /// </summary>
    public class Window : GameWindow
    {
        public Window()
            : base(640, 480, GraphicsMode.Default, "MD")
        {

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            this.MakeCurrent();
            GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            this.SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            double updatetime = e.Time;
            Program.Update(updatetime);

        }

        protected override void OnResize(EventArgs e)
        {
            this.MakeCurrent();
            GL.Viewport(0, 0, this.Width, this.Height);
        }
    }
}