namespace MD

open System
open System.Runtime.InteropServices

/// Represents a complex number.
[<StructLayout (LayoutKind.Sequential)>]
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

        /// Gets the square of the absolute value of this number.
        member this.SquareAbs = real * real + imag * imag

        /// Gets the phase of this complex number in radians
        member this.Phase = atan2 imag real

        /// Multiplies this number by i.
        member this.TimesI = new Complex (-imag, real)

        /// Gets the complex conjugate of this number.
        member this.Conjugate = new Complex (real, -imag)

        override this.ToString() = 
            if imag >= 0.0 then String.Format("{0} + {1}i", real, imag)
            else String.Format("{0} - {1}i", real, -imag)
    end