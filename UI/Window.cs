using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using MD.Data;
using MD.Codec;
using MD.UI.Audio;
using MD.UI.Input;

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

            // Create interface
            this._Probes = new ControlCollectionFeed<Probe>();
            this._Interface = new Interface(this._Probes);

            var mouse = this.Mouse;

            this._MousePosition = new ControlSignalFeed<Point>(new Point(mouse.X, mouse.Y));
            this._MouseLeft = new ControlSignalFeed<bool>(mouse[MouseButton.Left]);
            this._MouseRight = new ControlSignalFeed<bool>(mouse[MouseButton.Right]);
            this._MouseScroll = new ControlEventFeed<double>();
            this._MouseProbe = new Probe(this._MousePosition, this._MouseLeft, this._MouseRight, this._MouseScroll);

            mouse.Move += delegate(object sender, MouseMoveEventArgs args)
            {
                this._MousePosition.Current = new Point(args.X, args.Y);
            };

            mouse.ButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                if (args.Button == MouseButton.Left)
                    this._MouseLeft.Current = true;
                if (args.Button == MouseButton.Right)
                    this._MouseRight.Current = true;
            };

            mouse.ButtonUp += delegate(object sender, MouseButtonEventArgs args)
            {
                if (args.Button == MouseButton.Left)
                    this._MouseLeft.Current = false;
                if (args.Button == MouseButton.Right)
                    this._MouseRight.Current = false;
            };

            mouse.WheelChanged += delegate(object sender, MouseWheelEventArgs args)
            {
                this._MouseScroll.Fire(args.DeltaPrecise);
            };

            this.MouseEnter += delegate
            {
                this._RetractMouseProbe = this._Probes.Add(this._MouseProbe);
            };

            this.MouseLeave += delegate
            {
                if (this._RetractMouseProbe != null)
                {
                    this._RetractMouseProbe();
                    this._RetractMouseProbe = null;
                }
            };
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

        private Interface _Interface;
        private ControlCollectionFeed<Probe> _Probes;
        private RetractAction _RetractMouseProbe;

        private Probe _MouseProbe;
        private ControlSignalFeed<Point> _MousePosition;
        private ControlSignalFeed<bool> _MouseLeft;
        private ControlSignalFeed<bool> _MouseRight;
        private ControlEventFeed<double> _MouseScroll;
        

        private ControlEventFeed<AudioOutputControl> _AudioControl;
        private ControlSignalFeed<double> _AudioPitch;
    }
}