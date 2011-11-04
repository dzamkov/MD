using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

using MAD;
using MD.Data;

namespace MD.Codec
{
    /// <summary>
    /// A stream that returns (24-bit) pcm data for a source stream in the mp3 format.
    /// </summary>
    public class MP3Stream : Stream<Stero<int>>
    {
        public MP3Stream(Stream<byte> Source)
        {
            this._Source = Source;
            this._Decoder = new Decoder();
            this._Buffer = new byte[_BufferSize];
            this._SampleOffset = -1;
        }

        /// <summary>
        /// Initializes the MP3 stream.
        /// </summary>
        public void Initialize()
        {
            this._Decoder.Initialize();
        }

        /// <summary>
        /// Cleans up resources used by the MP3 stream.
        /// </summary>
        public void Terminate()
        {
            this._Decoder.Terminate();
        }

        public override unsafe int Read(int Size, Stero<int>[] Buffer, int Offset)
        {
            fixed (byte* ptr = this._Buffer)
            {
                if (this._SampleOffset == -1)
                    if (!this._AdvanceFrame(ptr))
                        return 0;
                int amountread = 0;
                while (true)
                {
                    int sampsleft = Decoder.FrameSampleCount - this._SampleOffset;
                    int* left = (int*)this._Decoder.Output + this._SampleOffset;
                    int* right = (int*)(left + Decoder.FrameSampleCount);
                    int channels = this._Decoder.Channels;
                    if (Size > sampsleft)
                    {
                        Size -= sampsleft;
                        amountread += sampsleft;
                        while (sampsleft-- > 0)
                        {
                            Buffer[Offset] = new Stero<int>(*left, *right);
                            Offset++;
                            left++;
                            right++;
                        }
                        if (!this._AdvanceFrame(ptr))
                            return amountread;
                        continue;
                    }
                    else
                    {
                        amountread += Size;
                        this._SampleOffset += Size;
                        while (Size-- > 0)
                        {
                            Buffer[Offset] = new Stero<int>(*left, *right);
                            Offset++;
                            left++;
                            right++;
                        }
                        return amountread;
                    }
                }
            }
        }

        /// <summary>
        /// Tries decoding the next frame.
        /// </summary>
        private unsafe bool _AdvanceFrame(byte* BufferPtr)
        {
            while (!this._Decoder.DecodeFrame())
            {
                if (this._Decoder.Error == Error.BufferData)
                {
                    int size = this._AdvanceBuffer(0);
                    if (size == 0)
                        return false;
                    this._Decoder.SetInput(BufferPtr, size);
                    continue;
                }
                if (this._Decoder.Error == Error.BufferLength)
                {
                    int save = _BufferSize - (int)((byte*)this._Decoder.NextFrame - BufferPtr);
                    int size = this._AdvanceBuffer(save);
                    if (size == 0)
                        return false;
                    this._Decoder.SetInput(BufferPtr, save + size);
                    continue;
                }
                if (!this._Decoder.ErrorRecoverable)
                {
                    return false;
                }
            }
            this._Decoder.SynthFrame();
            this._SampleOffset = 0;
            return true;
        }

        /// <summary>
        /// Fills the buffer with the next set of input data. Returns the amount of bytes read.
        /// </summary>
        /// <param name="Save">The amount of data at the end of the buffer to save by moving to the beginning of the buffer.</param>
        private int _AdvanceBuffer(int Save)
        {
            Save = Math.Max(Save, 0);
            int ts = _BufferSize - Save;
            for (int t = 0; t < Save; t++)
            {
                this._Buffer[t] = this._Buffer[t + ts];
            }
            return this._Source.Read(ts, this._Buffer, Save);
        }

        /// <summary>
        /// The size of the input buffer for an mp3 stream.
        /// </summary>
        private const int _BufferSize = 65536;

        private byte[] _Buffer;
        private Stream<byte> _Source;
        private Decoder _Decoder;
        private int _SampleOffset;
    }
}
