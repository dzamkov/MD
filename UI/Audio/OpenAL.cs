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
    public class OpenALOutput : AudioOutput
    {
        public OpenALOutput()
        {
            this._Context = new AudioContext();
            this._Active = new HashSet<OpenALOutputSource>();
        }

        public OpenALOutput(string Device)
        {
            this._Context = new AudioContext(Device);
            this._Active = new HashSet<OpenALOutputSource>();
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

        public override AudioOutputSource Begin(Stream<byte> Stream, int SampleRate, int Channels, AudioFormat Format)
        {
            ALFormat format;
            if (this.GetFormat(Channels, Format, out format))
            {
                return new OpenALOutputSource(this, Stream, SampleRate, format, 4096 * 4, 4);
            }
            return null;
        }

        public override void Update(double Time)
        {
            this._Context.MakeCurrent();
            foreach (OpenALOutputSource source in this._Active)
            {
                source.Update();
            }
        }

        internal AudioContext _Context;
        internal HashSet<OpenALOutputSource> _Active;
    }

    /// <summary>
    /// An audio output source for OpenAL.
    /// </summary>
    public class OpenALOutputSource : AudioOutputSource
    {
        public OpenALOutputSource(OpenALOutput Output, Stream<byte> Stream, int SampleRate, ALFormat Format, int BufferSize, int BufferCount)
        {
            Output._Context.MakeCurrent();
            this._Output = Output;

            this._ID = AL.GenSource();
            this._Stream = Stream;
            this._SampleRate = SampleRate;
            this._Format = Format;
            this._Buffer = new byte[BufferSize];
           
            // Initialize buffers
            for (int t = 0; t < BufferCount; t++)
            {
                this._Write(AL.GenBuffer());
            }
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

        public override int Position
        {
            get
            {
                return this._Position;
            }
            set
            {
                this._StartPosition += value - this._Position;
                this._Position = value;
            }
        }

        public override void Play()
        {
            this._Output._Context.MakeCurrent();
            AL.SourcePlay(this._ID);
            this._Output._Active.Add(this);
        }

        public override void Pause()
        {
            this._Output._Context.MakeCurrent();
            AL.SourcePause(this._ID);
            this._Output._Active.Remove(this);
        }

        public override void Stop()
        {
            this._Output._Context.MakeCurrent();
            AL.SourceStop(this._ID);
            this._Output._Active.Remove(this);

            int bufferamount;
            AL.GetSource(this._ID, ALGetSourcei.BuffersQueued, out bufferamount);

            int[] buffers = new int[bufferamount];
            AL.SourceUnqueueBuffers(this._ID, bufferamount, buffers);
            AL.DeleteBuffers(buffers);
            AL.DeleteSource(this._ID);
        }

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
                AL.SourcePlay(this._ID);
            }

            // Update play position
            int sampleoffset;
            AL.GetSource(this._ID, ALGetSourcei.SampleOffset, out sampleoffset);
            this._Position = this._StartPosition + sampleoffset;
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
        private OpenALOutput _Output;
        private Stream<byte> _Stream;
        private int _SampleRate;
        private int _StartPosition;
        private int _Position;
        private ALFormat _Format;
        private byte[] _Buffer;
    }
}
