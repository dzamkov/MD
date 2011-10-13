using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MD
{
    /// <summary>
    /// Contains functions for performing fourier transforms.
    /// </summary>
    public static class FFT
    {
        /// <summary>
        /// Performs a fourier transform on a real input array and outputs the result to a complex output array.
        /// </summary>
        public static unsafe void Real(double* Input, double* Output, double* Twiddle, int Samples, int Step)
        {
            if (Samples == 1)
            {
                Output[0] = Input[0];
                Output[1] = 0.0;
            }
            else
            {
                int hsamps = Samples / 2;
                int dstep = Step * 2;
                double* a = Output;
                double* b = Output + Samples;
                Real(Input, a, Twiddle, hsamps, dstep);
                Real(Input + dstep, b, Twiddle, hsamps, dstep);
                double* twid = Twiddle;
                for (int t = 0; t < hsamps; t++)
                {
                    double areal = a[0];
                    double aimag = a[1];
                    double breal = b[0];
                    double bimag = b[1];
                    double treal = twid[0];
                    double timag = twid[1];
                    double ereal = breal * treal - bimag * timag;
                    double eimag = breal * timag + bimag * treal;
                    a[0] = areal + ereal;
                    a[1] = aimag + eimag;
                    b[0] = breal - ereal;
                    b[1] = breal - eimag;

                    a += 2;
                    b += 2;
                    twid += 2 * Step;
                }
            }
        }

        /// <summary>
        /// Precomputes an array of twiddle factors for the given amount of samples. Each twiddle factor is a complex number, thus the size
        /// of the output array should be twice the amount of samples.
        /// </summary>
        public static unsafe void ComputeTwiddleFactors(bool Inverse, int Samples, double* Output)
        {
            double c = (Inverse ? 2.0 : -2.0) * Math.PI;
            for (int t = 0; t < Samples; t++)
            {
                Complex e = new Complex(c * (double)t / (double)Samples).TimesI.Exp;
                Output[0] = e.Real;
                Output[1] = e.Imag;
                Output += 2;
            }
        }
    }
}
