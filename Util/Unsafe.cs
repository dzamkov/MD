using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MD
{
    /// <summary>
    /// Contains helper functions for unsafe code.
    /// </summary>
    public static class Unsafe
    {
        /// <summary>
        /// Copies the given amount of bytes from the source to the destination.
        /// </summary>
        public static unsafe void Copy(byte* Source, byte* Destination, int Size)
        {
            int isize = Size / 4;
            Copy((int*)Source, (int*)Destination, isize);
            Size -= isize * 4;
            while (Size > 0)
            {
                *Destination = *Source;
                Source++;
                Destination++;
                Size--;
            }
        }

        /// <summary>
        /// Copies the given amount of shorts from the source to the destination.
        /// </summary>
        public static unsafe void Copy(short* Source, short* Destination, int Size)
        {
            int isize = Size / 2;
            Copy((int*)Source, (int*)Destination, isize);
            Size -= isize * 2;
            while (Size > 0)
            {
                *Destination = *Source;
                Source++;
                Destination++;
                Size--;
            }
        }

        /// <summary>
        /// Copies the given amount of ints from the source to the destination.
        /// </summary>
        public static unsafe void Copy(int* Source, int* Destination, int Size)
        {
            while (Size > 0)
            {
                *Destination = *Source;
                Source++;
                Destination++;
                Size--;
            }
        }

        /// <summary>
        /// Pins the given array.
        /// </summary>
        public static GCHandle Pin<T>(T[] Array)
        {
            return GCHandle.Alloc(Array, GCHandleType.Pinned);
        }

        /// <summary>
        /// Unpins the given handle.
        /// </summary>
        public static void Unpin(GCHandle Handle)
        {
            Handle.Free();
        }

        /// <summary>
        /// Gets the size in bytes of an instance of the given type.
        /// </summary>
        public static int SizeOf<T>()
        {
            return _Info<T>.Size;
        }

        /// <summary>
        /// Information about a type.
        /// </summary>
        private struct _Info<T>
        {
            static _Info()
            {
                Type type = typeof(T);
                if (type.IsValueType)
                {
                    Size = Marshal.SizeOf(type);
                }
                else
                {
                    Size = IntPtr.Size;
                }
            }

            /// <summary>
            /// The size in bytes of the type.
            /// </summary>
            public static int Size;
        }
    }
}
