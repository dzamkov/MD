using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using MD.Data;
using MD.Codec;
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
            Path file = "N:\\Music\\Me\\57.mp3";
            Context context = Container.Load(file);
            AudioContent content = (AudioContent)context.Content[0];

            AudioOutput ao = new OpenALOutput();
            this._AudioControl = new ControlEventFeed<AudioOutputControl>();
            this._AudioPitch = new ControlSignalFeed<double>(1.0);
            SignalFeed<long> position;
            ao.Begin(~content.GetStream(context), (int)content.SampleRate, content.Channels, content.Format, this._AudioControl, this._AudioPitch, out position);

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

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'p':
                    this._AudioControl.Fire(AudioOutputControl.Play);
                    break;
                case 's':
                    this._AudioControl.Fire(AudioOutputControl.Pause);
                    break;
                case 'q':
                    this._AudioPitch.Current = this._AudioPitch.Current * 1.2;
                    break;
                case 'w':
                    this._AudioPitch.Current = this._AudioPitch.Current / 1.2;
                    break;
            }
        }

        private ControlEventFeed<AudioOutputControl> _AudioControl;
        private ControlSignalFeed<double> _AudioPitch;
    }
}