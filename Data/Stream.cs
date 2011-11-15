using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MD.Data
{
    /// <summary>
    /// A reader for data from a certain source.
    /// </summary>
    public interface Stream<T>
    {
        /// <summary>
        /// Tries reading an item from the stream. If the end of the stream was reached, false is returned and Data
        /// will be left unchanged.
        /// </summary>
        bool Read(ref T Data);

        /// <summary>
        /// Reads the next items from the stream into the given buffer and returns the amount of items read.
        /// </summary>
        /// <param name="Size">The maximum amount of items to read.</param>
        /// <param name="Offset">The offset in the buffer to begin writing.</param>
        int Read(T[] Buffer, int Size, int Offset);

        /// <summary>
        /// Reads the next items from the stream into the given memory location. This should only be used when the stream contains
        /// value types.
        /// </summary>
        unsafe int Read(byte* Destination, int Size);
    }

    /// <summary>
    /// A stream that reads from a native .net stream.
    /// </summary>
    public sealed class NativeStream : Stream<byte>, IDisposable
    {
        public NativeStream(Disposable<Stream> Source)
        {
            this.Source = Source;
        }

        /// <summary>
        /// The source stream for this native stream.
        /// </summary>
        public readonly Disposable<Stream> Source;

        public bool Read(ref byte Data)
        {
            throw new NotImplementedException();
        }

        public int Read(byte[] Buffer, int Size, int Offset)
        {
            return (~this.Source).Read(Buffer, Offset, Size);
        }

        public unsafe int Read(byte* Destination, int Size)
        {
            fixed (byte* ptr = _ReadBuffer)
            {
                int ar = 0;
                while (Size > 0)
                {
                    if (Size > _ReadBuffer.Length)
                    {
                        int r = this.Read(_ReadBuffer, _ReadBuffer.Length, 0);
                        Size -= r;
                        ar += r;
                        Unsafe.Copy(ptr, Destination, r);
                        Destination += r;
                        if (r != _ReadBuffer.Length)
                            break;
                    }
                    else
                    {
                        int r = this.Read(_ReadBuffer, Size, 0);
                        ar += r;
                        Unsafe.Copy(ptr, Destination, r);
                        break;
                    }
                }
                return ar;
            }
        }

        public void Dispose()
        {
            this.Source.Dispose();
        }

        private static byte[] _ReadBuffer = new byte[4096];
    }

    /// <summary>
    /// A stream that reads from a buffer (native array).
    /// </summary>
    public sealed class BufferStream<T> : Stream<T>
    {
        public BufferStream(T[] Source, int Offset)
        {
            this.Source = Source;
            this.Offset = Offset;
        }

        public BufferStream(T[] Source)
            : this(Source, 0)
        {

        }

        /// <summary>
        /// The source buffer for this stream.
        /// </summary>
        public readonly T[] Source;

        /// <summary>
        /// The current offset of the stream in the buffer.
        /// </summary>
        public int Offset;

        public bool Read(ref T Data)
        {
            if (this.Offset < this.Source.Length)
            {
                Data = this.Source[this.Offset];
                this.Offset++;
                return true;
            }
            return false;
        }

        public int Read(T[] Buffer, int Size, int Offset)
        {
            Size = Math.Min(this.Source.Length - this.Offset, Size);
            int of = this.Offset;
            T[] s = this.Source;
            this.Offset += Size;
            while (Size-- > 0)
            {
                Buffer[Offset++] = s[of++];
            }
            return Size;
        }

        public unsafe int Read(byte* Destination, int Size)
        {
            int size = Unsafe.SizeOf<T>();
            var handle = Unsafe.Pin<T>(Source);
            byte* source = (byte*)handle.AddrOfPinnedObject().ToPointer();
            source += this.Offset;
            int r = Math.Min(Size, this.Source.Length - this.Offset);
            Unsafe.Copy(source, Destination, size * r);
            this.Offset += r;
            Unsafe.Unpin(handle);
            return r;
        }
    }

    /// <summary>
    /// A stream that maps item from a source stream based on a mapping function.
    /// </summary>
    public sealed class MapStream<TSource, T> : Stream<T>
    {
        public MapStream(Stream<TSource> Source, Func<TSource, T> Map)
        {
            this.Source = Source;
            this.Map = Map;
        }

        /// <summary>
        /// The source stream for this stream.
        /// </summary>
        public readonly Stream<TSource> Source;

        /// <summary>
        /// The mapping function for this stream.
        /// </summary>
        public readonly Func<TSource, T> Map;

        public bool Read(ref T Data)
        {
            TSource source = default(TSource);
            if (this.Source.Read(ref source))
            {
                Data = this.Map(source);
                return true;
            }
            return false;
        }

        public int Read(T[] Buffer, int Size, int Offset)
        {
            throw new NotImplementedException();
        }

        public unsafe int Read(byte* Destination, int Size)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A stream that reads raw data from a pointer location.
    /// </summary>
    public sealed class UnsafeStream : Stream<byte>
    {
        public unsafe UnsafeStream(byte* Current, byte* End)
        {
            this.Current = Current;
            this.End = End;
        }

        public UnsafeStream()
        {
            
        }

        /// <summary>
        /// A pointer to the current position of the stream in memory.
        /// </summary>
        public unsafe byte* Current;

        /// <summary>
        /// A pointer to the end of the array in memory.
        /// </summary>
        public unsafe byte* End;

        public unsafe bool Read(ref byte Data)
        {
            if (this.Current < this.End)
            {
                Data = *this.Current;
                this.Current++;
                return true;
            }
            return false;
        }

        public unsafe int Read(byte* Destination, int Size)
        {
            int ar = Math.Min((int)(this.End - this.Current), Size);
            Unsafe.Copy(this.Current, Destination, ar);
            this.Current += ar;
            return ar;
        }

        public unsafe int Read(byte[] Buffer, int Size, int Offset)
        {
            fixed (byte* ptr = Buffer)
            {
                return this.Read(ptr + Offset, Size);
            }
        }
    }
}
