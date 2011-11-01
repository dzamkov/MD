using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

using Mp3Sharp;

using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// An mp3 signal source.
    /// </summary>
    public class MP3Source
    {
        public MP3Source(Stream Stream)
        {
            this._Stream = new Mp3Stream(Stream);
        }

        public MP3Source(string File)
        {
            this._Stream = new Mp3Stream(File);
        }

        /// <summary>
        /// Gets the bytes per sample in a mp3 sound format.
        /// </summary>
        public static int FormatBytesPerSample(SoundFormat Format)
        {
            if (Format == SoundFormat.Pcm16BitMono)
                return 2;
            else
                return 4;
        }

        /// <summary>
        /// Gets the sound format of the mp3 source.
        /// </summary>
        public SoundFormat Format
        {
            get
            {
                if (this._Raw == null)
                    this._Raw = new _RawArray(this._Stream);
                this._Raw.WaitInitialized();
                return this._Stream.Format;
            }
        }

        /// <summary>
        /// Gets the sample rate of the mp3 source.
        /// </summary>
        public int SampleRate
        {
            get
            {
                if (this._Raw == null)
                    this._Raw = new _RawArray(this._Stream);
                this._Raw.WaitInitialized();
                return this._Stream.Frequency;
            }
        }

        /// <summary>
        /// Gets the size (in samples) of the mp3 source.
        /// </summary>
        public int Size
        {
            get
            {
                if (this._Raw == null)
                    this._Raw = new _RawArray(this._Stream);
                this._Raw.WaitInitialized();
                return (int)(this._Stream.Length / FormatBytesPerSample(this._Stream.Format));
            }
        }

        /// <summary>
        /// Gets the raw sample data for this mp3 source.
        /// </summary>
        public Array<byte> Raw
        {
            get
            {
                if (this._Raw == null)
                    this._Raw = new _RawArray(this._Stream);
                return this._Raw;
            }
        }

        /// <summary>
        /// Gets a 16bit stero signal for this mp3 source.
        /// </summary>
        public Signal<Stero<short>> Stero16Signal
        {
            get
            {
                return new DiscreteSignal<Stero<short>>(
                    this.Raw.Combine<Stero<short>, Stero16Compound>(),
                    this.SampleRate);
            }
        }

        /// <summary>
        /// The data array for an mp3 file (written by a seperate thread).
        /// </summary>
        private class _RawArray : Array<byte>
        {
            public _RawArray(Mp3Stream Stream)
            {
                this._Stream = Stream;
                this._Memory = new MemoryArray<byte>(65536);

                Thread tr = new Thread(this._BeginWrite);
                tr.IsBackground = true;
                tr.Start();
            }

            /// <summary>
            /// Begins writing the data of this array into memory.
            /// </summary>
            private void _BeginWrite()
            {
                while (true)
                {
                    byte[] buf = new byte[this._Memory.BufferSize];
                    int r = this._Stream.Read(buf, 0, buf.Length);
                    Thread.Sleep(100);
                    this._Memory.Append(buf);
                    this._Initialized = true;
                    if (r != buf.Length)
                    {
                        break;
                    }
                }
            }

            /// <summary>
            /// Blocks the current thread until the mp3 stream is initialized.
            /// </summary>
            public void WaitInitialized()
            {
                while (!this._Initialized)
                {
                    Thread.Sleep(10);
                }
            }

            public override int Size
            {
                get
                {
                    return (int)this._Stream.Length;
                }
            }

            public override byte Read(int Index)
            {
                while (this._Memory.Size < Index)
                {
                    Thread.Sleep(10);
                }
                return this._Memory.Read(Index);
            }

            public override void Read(int Start, int Size, byte[] Buffer, int Offset)
            {
                while (this._Memory.Size < Start + Size)
                {
                    Thread.Sleep(10);
                }
                this._Memory.Read(Start, Size, Buffer, Offset);
            }

            private volatile bool _Initialized;
            private Mp3Stream _Stream;
            private MemoryArray<byte> _Memory;
        }

        private Mp3Stream _Stream;
        private _RawArray _Raw;
    }
}
