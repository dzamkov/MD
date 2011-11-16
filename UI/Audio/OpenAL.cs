using System;
using System.Collections.Generic;
using System.Linq;

using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

using MD.Data;
using MD.Codec;

namespace MD.UI.Audio
{
    /// <summary>
    /// Audio output with OpenAL.
    /// </summary>
    public class OpenALOutput : AudioOutput, IDisposable
    {
        public OpenALOutput()
        {
            this._Context = new AudioContext();
            this._Active = new HashSet<_Source>();
            this._Retract = Program.RegisterUpdate(this._Update);
        }

        public OpenALOutput(string Device)
        {
            this._Context = new AudioContext(Device);
            this._Active = new HashSet<_Source>();
            this._Retract = Program.RegisterUpdate(this._Update);
        }

        public void Dispose()
        {
            this._Context.Dispose();
            this._Retract();
        }

        /// <summary>
        /// Gets the names of the devices currently available for output.
        /// </summary>
        public static IEnumerable<string> AvailableDevices
        {
            get
            {
                return AudioContext.AvailableDevices;
            }
        }

        /// <summary>
        /// Indicates wether the current audio context supports double-precision floating point output.
        /// </summary>
        public bool SupportsDouble
        {
            get
            {
                return this._Context.SupportsExtension("AL_EXT_DOUBLE");
            }
        }

        /// <summary>
        /// Indicates wether the current audio context supports single-precision floating point output.
        /// </summary>
        public bool SupportsFloat
        {
            get
            {
                return this._Context.SupportsExtension("AL_EXT_FLOAT32");
            }
        }

        /// <summary>
        /// Indicates wether the current audio context supports multi-channel output.
        /// </summary>
        public bool SupportsMultiChannel
        {
            get
            {
                return this._Context.SupportsExtension("AL_EXT_MCFORMATS");
            }
        }

        /// <summary>
        /// Indicates wether the current audio context supports MP3 output.
        /// </summary>
        public bool SupportsMP3
        {
            get
            {
                return this._Context.SupportsExtension("AL_EXT_MP3");
            }
        }

        /// <summary>
        /// Indicates wether the current audio context supports Ogg Vorbis output.
        /// </summary>
        public bool SupportsVorbis
        {
            get
            {
                return this._Context.SupportsExtension("EXT_vorbis");
            }
        }

        /// <summary>
        /// Gets the best output format for a source with the given format and amount of channels. If no suitable output format exists,
        /// this function returns false.
        /// </summary>
        public bool GetFormat(int Channels, AudioFormat SourceFormat, out ALFormat OutputFormat)
        {
            ALFormat[] formatbychannels = _Formats[(int)SourceFormat];
            if (Channels > formatbychannels.Length)
            {
                OutputFormat = (ALFormat)0;
                return false;
            }

            OutputFormat = formatbychannels[Channels - 1];

            if (OutputFormat == (ALFormat)0)
                return false;

            if ((int)OutputFormat >= (int)ALFormat.MultiQuad8Ext && (int)OutputFormat <= (int)ALFormat.Multi71Chn32Ext && !this.SupportsMultiChannel)
                return false;

            if ((int)OutputFormat >= (int)ALFormat.MonoFloat32Ext && (int)OutputFormat <= (int)ALFormat.StereoFloat32Ext && !this.SupportsFloat)
                return false;

            if ((int)OutputFormat >= (int)ALFormat.MonoDoubleExt && (int)OutputFormat <= (int)ALFormat.StereoDoubleExt && !this.SupportsDouble)
                return false;

            return true;
        }

        /// <summary>
        /// Table of formats indexed by source format and channels.
        /// </summary>
        private static readonly ALFormat[][] _Formats = new ALFormat[][]
        {
            // PCM 8
            new ALFormat[]
            {
                ALFormat.Mono8,
                ALFormat.Stereo8,
                (ALFormat)0,
                ALFormat.MultiQuad8Ext,
                ALFormat.Multi51Chn8Ext,
                ALFormat.Multi61Chn8Ext,
                ALFormat.Multi71Chn8Ext
            },

            // PCM 16
            new ALFormat[]
            {
                ALFormat.Mono16,
                ALFormat.Stereo16,
                (ALFormat)0,
                ALFormat.MultiQuad16Ext,
                ALFormat.Multi51Chn16Ext,
                ALFormat.Multi61Chn16Ext,
                ALFormat.Multi71Chn16Ext
            },

            // PCM 32
            new ALFormat[]
            {
                (ALFormat)0,
                (ALFormat)0,
                (ALFormat)0,
                ALFormat.MultiQuad32Ext,
                ALFormat.Multi51Chn32Ext,
                ALFormat.Multi61Chn32Ext,
                ALFormat.Multi71Chn32Ext
            },

            // Float
            new ALFormat[]
            {
                ALFormat.MonoFloat32Ext,
                ALFormat.StereoFloat32Ext,
            },

            // Double
            new ALFormat[]
            {
                ALFormat.MonoDoubleExt,
                ALFormat.StereoDoubleExt
            },
        };

        public bool Begin(
            Stream<byte> Stream, 
            int SampleRate, int Channels, AudioFormat Format, 
            EventFeed<AudioOutputControl> Control, 
            SignalFeed<double> Pitch, 
            out SignalFeed<long> Position)
        {
            ALFormat format;
            if (GetFormat(Channels, Format, out format))
            {
                _Source source = new _Source(Stream, SampleRate, format, 4096 * 4, 3, Pitch);
                new _SourceListener(source, this).Link(Control);
                Position = source.Position;
                return true;
            }
            Position = null;
            return false;
        }

        /// <summary>
        /// Updates the state of the audio output and all sources by the given amount of time in seconds.
        /// </summary>
        private void _Update(double Time)
        {
            this._Context.MakeCurrent();
            foreach (_Source source in this._Active)
            {
                source.Update();
            }
        }

        /// <summary>
        /// A listener to a control event feed for a source.
        /// </summary>
        private class _SourceListener
        {
            public _SourceListener(_Source Source, OpenALOutput Output)
            {
                this.Source = Source;
                this.Output = Output;
            }

            /// <summary>
            /// The callback for this listener.
            /// </summary>
            public void Callback(AudioOutputControl Control)
            {
                this.Output._Context.MakeCurrent();
                switch (Control)
                {
                    case AudioOutputControl.Play:
                        this.Source.Play();
                        this.Output._Active.Add(this.Source);
                        break;
                    case AudioOutputControl.Pause:
                        this.Source.Pause();
                        this.Output._Active.Remove(this.Source);
                        break;
                    case AudioOutputControl.Stop:
                        this.Source.Stop();
                        this.Source.Dispose();
                        this.Output._Active.Remove(this.Source);
                        this.Retract();
                        this.Output._Retract -= this.Retract;
                        break;
                }
            }

            /// <summary>
            /// Links this listener to the given event feed.
            /// </summary>
            public void Link(EventFeed<AudioOutputControl> Feed)
            {
                this.Retract = Feed.Register(this.Callback);
                this.Output._Retract += this.Retract;
            }

            /// <summary>
            /// The source this listener is for.
            /// </summary>
            public readonly _Source Source;

            /// <summary>
            /// The output this listener is for.
            /// </summary>
            public readonly OpenALOutput Output;

            /// <summary>
            /// The retract action for the callback of this listener.
            /// </summary>
            public RetractAction Retract;
        }

        /// <summary>
        /// An audio output source for OpenAL.
        /// </summary>
        private class _Source : IDisposable
        {
            public _Source(Stream<byte> Stream, int SampleRate, ALFormat Format, int BufferSize, int BufferCount, SignalFeed<double> Pitch)
            {
                this.Position = new ControlSignalFeed<long>(0);

                this._ID = AL.GenSource();
                this._Stream = Stream;
                this._SampleRate = SampleRate;
                this._Format = Format;
                this._Pitch = Pitch;
                this._Buffer = new byte[BufferSize];

                // Initialize buffers
                for (int t = 0; t < BufferCount; t++)
                {
                    this._Write(AL.GenBuffer());
                }
            }

            public void Dispose()
            {
                int bufferamount;
                AL.GetSource(this._ID, ALGetSourcei.BuffersQueued, out bufferamount);

                int[] buffers = new int[bufferamount];
                AL.SourceUnqueueBuffers(this._ID, bufferamount, buffers);
                AL.DeleteBuffers(buffers);
                AL.DeleteSource(this._ID);
            }

            /// <summary>
            /// Gets the size of the buffer (in bytes) for this source.
            /// </summary>
            public int BufferSize
            {
                get
                {
                    return this._Buffer.Length;
                }
            }

            /// <summary>
            /// Plays this source.
            /// </summary>
            public void Play()
            {
                AL.SourcePlay(this._ID);
            }

            /// <summary>
            /// Pauses this source.
            /// </summary>
            public void Pause()
            {
                AL.SourcePause(this._ID);
            }

            /// <summary>
            /// Stops this source.
            /// </summary>
            public void Stop()
            {
                AL.SourceStop(this._ID);
            }

            /// <summary>
            /// The position of the source.
            /// </summary>
            public readonly ControlSignalFeed<long> Position;

            /// <summary>
            /// Updates the audio source.
            /// </summary>
            public void Update()
            {
                int bufferprocessed;
                AL.GetSource(this._ID, ALGetSourcei.BuffersProcessed, out bufferprocessed);

                if (bufferprocessed > 0)
                {
                    this._StartPosition += this.BufferSize * bufferprocessed;

                    int[] buffers = new int[bufferprocessed];
                    AL.SourceUnqueueBuffers(this._ID, bufferprocessed, buffers);
                    for (int t = 0; t < buffers.Length; t++)
                    {
                        this._Write(buffers[t]);
                    }

                    if (AL.GetSourceState(this._ID) != ALSourceState.Playing)
                    {
                        AL.SourcePlay(this._ID);
                    }
                }

                // Update play position
                int sampleoffset;
                AL.GetSource(this._ID, ALGetSourcei.SampleOffset, out sampleoffset);
                this.Position.Current = this._StartPosition + sampleoffset;

                // Update pitch (if needed).
                if (this._Pitch != null)
                {
                    double nrate = this._Pitch.Current;
                    AL.Source(this._ID, ALSourcef.Pitch, (float)nrate);
                }
            }

            /// <summary>
            /// Writes the next set of data to the buffer with the given ID.
            /// </summary>
            private void _Write(int ID)
            {
                this._Stream.Read(this._Buffer, this._Buffer.Length, 0);
                AL.BufferData<byte>(ID, this._Format, this._Buffer, this._Buffer.Length, this._SampleRate);
                AL.SourceQueueBuffer(this._ID, ID);
            }

            private int _ID;
            private Stream<byte> _Stream;
            private int _SampleRate;
            private int _StartPosition;
            private ALFormat _Format;
            private byte[] _Buffer;
            private SignalFeed<double> _Pitch;
        }

        private AudioContext _Context;
        private HashSet<_Source> _Active;
        private RetractAction _Retract;
    }
}
