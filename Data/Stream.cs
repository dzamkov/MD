using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MD.Data
{
    /// <summary>
    /// A reader for data from a certain source.
    /// </summary>
    public abstract class Stream<T>
    {
        /// <summary>
        /// Tries reading an item from the stream. If the end of the stream was reached, false is returned and Data
        /// will be left unchanged.
        /// </summary>
        public virtual bool Read(ref T Data)
        {
            T[] buf = new T[1];
            if (this.Read(1, buf, 0) > 0)
            {
                Data = buf[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the next few items from the stream into the given buffer and returns the amount of items read.
        /// </summary>
        /// <param name="Size">The maximum amount of items to read.</param>
        /// <param name="Offset">The offset in the buffer to begin writing.</param>
        public virtual int Read(int Size, T[] Buffer, int Offset)
        {
            int ar = 0;
            while (Size-- > 0)
            {
                if (!this.Read(ref Buffer[Offset]))
                    return ar;
                ar++;
                Offset++;
            }
            return ar;
        }
    }

    /// <summary>
    /// A stream that reads from a native .net stream.
    /// </summary>
    public sealed class NativeStream : Stream<byte>, IDisposable
    {
        public NativeStream(Stream Source)
        {
            this.Source = Source;
        }

        public NativeStream(Path File)
        {
            this.Source = new FileStream(File, FileMode.Open);
        }

        /// <summary>
        /// The source stream for this native stream.
        /// </summary>
        public readonly Stream Source;

        public override int Read(int Size, byte[] Buffer, int Offset)
        {
            return this.Source.Read(Buffer, Offset, Size);
        }

        public void Dispose()
        {
            this.Source.Dispose();
        }
    }
}
