using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A stero sample of a certain type.
    /// </summary>
    public struct Stero<T>
    {
        public Stero(T Left, T Right)
        {
            this.Left = Left;
            this.Right = Right;
        }

        /// <summary>
        /// The sample for the left channel.
        /// </summary>
        public T Left;

        /// <summary>
        /// The sample for the right channel.
        /// </summary>
        public T Right;
    }

    /// <summary>
    /// A compound for stero samples.
    /// </summary>
    public struct SteroCompound<T> : ICompound<Stero<T>, T>
    {
        public int Size
        {
            get
            {
                return 2;
            }
        }

        public Stero<T> Combine(T[] Buffer, int Offset)
        {
            T left = Buffer[Offset++];
            T right = Buffer[Offset];
            return new Stero<T>(left, right);
        }

        public void Split(Stero<T> Compound, T[] Buffer, int Offset)
        {
            Buffer[Offset++] = Compound.Left;
            Buffer[Offset] = Compound.Right;
        }
    }

    /// <summary>
    /// A compound for 16bit stero samples.
    /// </summary>
    public struct Stero16Compound : ICompound<Stero<short>, byte>, ICompound<Stero<double>, byte>
    {
        public int Size
        {
            get
            {
                return 4;
            }
        }

        Stero<short> ICompound<Stero<short>, byte>.Combine(byte[] Buffer, int Offset)
        {
            throw new NotImplementedException();
        }

        public void Split(Stero<short> Compound, byte[] Buffer, int Offset)
        {
            Buffer[Offset++] = (byte)Compound.Left;
            Buffer[Offset++] = (byte)(Compound.Left >> 8);
            Buffer[Offset++] = (byte)Compound.Right;
            Buffer[Offset] = (byte)(Compound.Right >> 8);
        }

        Stero<double> ICompound<Stero<double>, byte>.Combine(byte[] Buffer, int Offset)
        {
            throw new NotImplementedException();
        }

        public void Split(Stero<double> Compound, byte[] Buffer, int Offset)
        {
            short samp;
            samp = (short)(Compound.Left * 32767.0);
            Buffer[Offset++] = (byte)samp;
            Buffer[Offset++] = (byte)(samp >> 8);
            samp = (short)(Compound.Right * 32767.0);
            Buffer[Offset++] = (byte)samp;
            Buffer[Offset] = (byte)(samp >> 8);
        }
    }
}
