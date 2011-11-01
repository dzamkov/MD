using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.Data
{
    /// <summary>
    /// Represents a double-precision complex number.
    /// </summary>
    public struct Complex
    {
        public Complex(double Real, double Imag)
        {
            this.Real = Real;
            this.Imag = Imag;
        }

        public Complex(double Real)
        {
            this.Real = Real;
            this.Imag = 0.0;
        }

        public override string ToString()
        {
            return this.Real.ToString() + " + " + this.Imag.ToString() + "i";
        }

        /// <summary>
        /// Gets the value of e to the power of this complex number.
        /// </summary>
        public Complex Exp
        {
            get
            {
                double rexp = Math.Exp(this.Real);
                double icos = Math.Cos(this.Imag);
                double isin = Math.Sin(this.Imag);
                return new Complex(rexp * icos, rexp * isin);
            }
        }

        /// <summary>
        /// Gets the argument of the complex number.
        /// </summary>
        public double Arg
        {
            get
            {
                return Math.Atan2(this.Imag, this.Real);
            }
        }

        /// <summary>
        /// Get the square of the absolute value of this complex number.
        /// </summary>
        public double SquareAbs
        {
            get
            {
                return this.Real * this.Real + this.Imag * this.Imag;
            }
        }

        /// <summary>
        /// Gets the absolute value of this complex number.
        /// </summary>
        public double Abs
        {
            get
            {
                return Math.Sqrt(this.SquareAbs);
            }
        }

        /// <summary>
        /// Gets the value of this complex number when multiplied by I.
        /// </summary>
        public Complex TimesI
        {
            get
            {
                return new Complex(-this.Imag, this.Real);
            }
        }

        public static Complex operator +(double A, Complex B)
        {
            return new Complex(A + B.Real, B.Imag);
        }

        public static Complex operator +(Complex A, double B)
        {
            return new Complex(A.Real + B, A.Imag);
        }

        public static Complex operator +(Complex A, Complex B)
        {
            return new Complex(A.Real + B.Real, A.Imag + B.Imag);
        }

        public static Complex operator -(double A, Complex B)
        {
            return new Complex(A - B.Real, -B.Imag);
        }

        public static Complex operator -(Complex A, double B)
        {
            return new Complex(A.Real - B, A.Imag);
        }

        public static Complex operator -(Complex A, Complex B)
        {
            return new Complex(A.Real - B.Real, A.Imag - B.Imag);
        }

        public static Complex operator *(double A, Complex B)
        {
            return new Complex(A * B.Real, A * B.Imag);
        }

        public static Complex operator *(Complex A, double B)
        {
            return new Complex(A.Real * B, A.Imag * B);
        }

        public static Complex operator *(Complex A, Complex B)
        {
            return new Complex(A.Real * B.Real - A.Imag * B.Imag, A.Imag * B.Real + A.Real * B.Imag);
        }

        public static Complex operator /(double A, Complex B)
        {
            return new Complex(A * B.Real, -A * B.Imag) / B.SquareAbs;
        }

        public static Complex operator /(Complex A, double B)
        {
            return new Complex(A.Real / B, A.Imag / B);
        }

        public static Complex operator /(Complex A, Complex B)
        {
            return new Complex(A.Real * B.Real + A.Imag * B.Imag, A.Imag * B.Real - A.Real * B.Imag) / B.SquareAbs;
        }

        public static implicit operator Complex(double A)
        {
            return new Complex(A);
        }

        /// <summary>
        /// The real part of the complex number.
        /// </summary>
        public double Real;

        /// <summary>
        /// The imaginary part of the complex number.
        /// </summary>
        public double Imag;
    }

}
