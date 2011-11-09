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
            if (this.Read(buf, 1, 0) > 0)
            {
                Data = buf[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the next items from the stream into the given buffer and returns the amount of items read.
        /// </summary>
        /// <param name="Size">The maximum amount of items to read.</param>
        /// <param name="Offset">The offset in the buffer to begin writing.</param>
        public virtual int Read(T[] Buffer, int Size, int Offset)
        {
            int ar = 0;
            while (Size > 0)
            {
                if (!this.Read(ref Buffer[Offset]))
                    break;
                ar++;
                Offset++;
                Size--;
            }
            return ar;
        }

        /// <summary>
        /// Reads the next items from the stream into the given memory location. This should only be used when the stream contains
        /// value types.
        /// </summary>
        public virtual unsafe int Read(byte* Destination, int Size)
        {
            T[] buf = new T[1];
            int size = Unsafe.SizeOf<T>();
            var handle = Unsafe.Pin<T>(buf);
            byte* source = (byte*)handle.AddrOfPinnedObject().ToPointer();
            int ar = 0;
            while (Size > 0)
            {
                if (!this.Read(ref buf[0]))
                    break;
                Unsafe.Copy(source, Destination, size);
                Destination += size;
                ar++;
                Size--;
            }
            Unsafe.Unpin(handle);
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

        public override int Read(byte[] Buffer, int Size, int Offset)
        {
            return this.Source.Read(Buffer, Offset, Size);
        }

        public override unsafe int Read(byte* Destination, int Size)
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

        public override bool Read(ref T Data)
        {
            if (this.Offset < this.Source.Length)
            {
                Data = this.Source[this.Offset];
                this.Offset++;
                return true;
            }
            return false;
        }

        public override int Read(T[] Buffer, int Size, int Offset)
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

        public override unsafe int Read(byte* Destination, int Size)
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

        public override bool Read(ref T Data)
        {
            TSource source = default(TSource);
            if (this.Source.Read(ref source))
            {
                Data = this.Map(source);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A stream that splits compounds from a source stream into items.
    /// </summary>
    public sealed class SplitStream<TSource, T, TCompound> : Stream<T>
        where TCompound : ICompound<TSource, T>
    {
        public SplitStream(Stream<TSource> Source)
        {
            this.Source = Source;
            this.Buffer = new T[default(TCompound).Size];
            this.Offset = this.Buffer.Length;
        }

        public SplitStream(Stream<TSource> Source, T[] Buffer, int Offset)
        {
            this.Source = Source;
            this.Buffer = Buffer;
            this.Offset = Offset;
        }

        /// <summary>
        /// The source stream for this stream.
        /// </summary>
        public readonly Stream<TSource> Source;

        /// <summary>
        /// A buffer for the next items to be read (with a size of the compound being split).
        /// </summary>
        public readonly T[] Buffer;

        /// <summary>
        /// The current offset of the stream in the item buffer.
        /// </summary>
        public int Offset;

        public override bool Read(ref T Data)
        {
            if (this.Offset < this.Buffer.Length)
            {
                Data = this.Buffer[this.Offset];
                this.Offset++;
                return true;
            }
            if (this.AdvanceBuffer())
            {
                Data = this.Buffer[0];
                this.Offset++;
                return true;
            }
            return false;
        }

        public override int Read(T[] Buffer, int Size, int Offset)
        {
            return base.Read(Buffer, Size, Offset);
        }

        /// <summary>
        /// Fills the buffer with the next items to be read. Returns false if there is no more source data.
        /// </summary>
        public bool AdvanceBuffer()
        {
            TSource src = default(TSource);
            if (this.Source.Read(ref src))
            {
                default(TCompound).Split(src, this.Buffer, 0);
                this.Offset = 0;
                return true;
            }
            return false;
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

        public override unsafe bool Read(ref byte Data)
        {
            if (this.Current < this.End)
            {
                Data = *this.Current;
                this.Current++;
                return true;
            }
            return false;
        }

        public override unsafe int Read(byte* Destination, int Size)
        {
            int ar = Math.Min((int)(this.End - this.Current), Size);
            Unsafe.Copy(this.Current, Destination, ar);
            this.Current += ar;
            return ar;
        }

        public override unsafe int Read(byte[] Buffer, int Size, int Offset)
        {
            fixed (byte* ptr = Buffer)
            {
                return this.Read(ptr + Offset, Size);
            }
        }
    }
}
