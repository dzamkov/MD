﻿namespace MD

open System

/// Represents a complex number.
type Complex (real : float, imag : float) =
    struct

        /// Gets zero as a complex number.
        static member Zero = new Complex (0.0, 0.0)

        /// Adds two complex numbers.
        static member (+) (a : Complex, b : Complex) = new Complex (a.Real + b.Real, a.Imag + b.Imag)
        
        /// Adds a complex number to a real.
        static member (+) (a : Complex, b : float) = new Complex (a.Real + b, a.Imag)

        /// Subtracts two complex numbers.
        static member (-) (a : Complex, b : Complex) = new Complex (a.Real - b.Real, a.Imag - b.Imag)

        /// Subtracts a real from a complex number.
        static member (-) (a : Complex, b : float) = new Complex (a.Real - b, a.Imag)

        /// Multiples two complex numbers.
        static member (*) (a : Complex, b : Complex) = 
            new Complex (a.Real * b.Real - a.Imag * b.Imag, a.Imag * b.Real + a.Real * b.Imag)

        /// Multiples a complex number by a real.
        static member (*) (a : Complex, b : float) = 
            new Complex (a.Real * b, a.Imag * b)

        /// Divides two complex numbers.
        static member (/) (a : Complex, b : Complex) = 
            let iden = 1.0 / (b.Real * b.Real + b.Imag * b.Imag)
            let real = (a.Real * b.Real + a.Imag * b.Imag) * iden
            let imag = (a.Imag * b.Real - a.Real * b.Imag) * iden
            new Complex (real, imag)

        /// Gets e to the power of a number multiplied by i.
        static member ExpImag (imag : float) =
            new Complex (cos imag, -(sin imag))

        /// Gets the real component of this number.
        member this.Real = real

        /// Gets the imaginary component of this number.
        member this.Imag = imag

        /// Gets the absolute value, or magnitude of this number.
        member this.Abs = sqrt (real * real + imag * imag)

        /// Multiplies this number by i.
        member this.TimesI = new Complex (-this.Imag, this.Real)

        override this.ToString() = String.Format("{0} + {1}i", real, imag)
    end