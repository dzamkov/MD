namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// Contains functions for constructing and manipulating discrete signals.
module DSignal =

    /// Performs an in-place downsampling of the given signal by an integer factor. Note that this will
    /// cause aliasing if there is still high-frequency content present.
    let inline downsample factor (source : Buffer<'a>) size =
        let mutable source = source
        let mutable sindex = 0
        let mutable dindex = 0
        while sindex < size do
            source.[dindex] <- source.[sindex]
            sindex <- sindex + factor
            dindex <- dindex + 1

    /// Performs an in-place downsampling of the frequency-domain elements of the given time-domain signal 
    /// by an integer factor.
    let inline downsampleFrequency factor (source : Buffer<'a>) size = 
        let mutable source = source

        // Split the signal into chunks and add them all together at the beginning of the buffer. This will 
        // retain frequency information across the entire spectrum, but will decrease the resolution of
        // that information.
        let chunkSize = size / factor
        for chunkIndex = 1 to factor - 1 do
            let chunk = source.Advance (chunkSize * chunkIndex)
            let mutable index = 0
            while index < chunkSize do
                source.[index] <- source.[index] + chunk.[index]
                index <- index + 1

    /// Performs an in-place scaling of the given signal by the given real factor.
    let inline scale (factor : 'b) (source : Buffer<'a>) size =
        let mutable source = source
        let mutable index = 0
        while index < size do
            source.[index] <- source.[index] * factor
            index <- index + 1

    /// Performs an in-place windowing of the given signal by the given window (of the size).
    let inline window (window : Buffer<'b>) (source : Buffer<'a>) size =
        let mutable source = source
        let mutable index = 0
        while index < size do
            source.[index] <- source.[index] * window.[index]
            index <- index + 1

    /// Adds two signal together.
    let inline add (source : Buffer<'a>) (destination : Buffer<'a>) size = 
        let mutable destination = destination
        let mutable index = 0
        while index < size do
            destination.[index] <- source.[index] + destination.[index]
            index <- index + 1

    /// Converts a real signal to a complex signal.
    let convertComplex (source : Buffer<float>) (destination : Buffer<Complex>) size =
        let mutable destination = destination
        let mutable index = 0
        while index < size do
            destination.[index] <- new Complex (source.[index], 0.0)
            index <- index + 1

    /// Gets the absolute value of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let absReal (source : Buffer<Complex>) (destination : Buffer<float>) size =
        let mutable destination = destination
        let mutable index = 0
        while index < size do
            destination.[index] <- source.[index].Abs
            index <- index + 1

    /// Gets the real part of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let real (source : Buffer<Complex>) (destination : Buffer<float>) size =
        let mutable destination = destination
        let mutable index = 0
        while index < size do
            destination.[index] <- source.[index].Real
            index <- index + 1

    /// Gets the imaginary part of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let imag (source : Buffer<Complex>) (destination : Buffer<float>) size =
        let mutable destination = destination
        let mutable index = 0
        while index < size do
            destination.[index] <- source.[index].Imag
            index <- index + 1

    /// Performs an in-place conjugation of a complex signal.
    let conjugate (source : Buffer<Complex>) size =
        let mutable imag = source.Cast<float>().Advance(1).Skip 2
        let mutable index = 0
        while index < size do
            imag.[index] <- imag.[index] * -1.0
            index <- index + 1
        
    /// Performs an in-place downsampling of the given real signal by an integer factor.
    let downsampleReal factor (source : Buffer<float>) size = downsample factor source size

    /// Performs an in-place downsampling of the given complex signal by an integer factor.
    let downsampleComplex factor (source : Buffer<Complex>) size = downsample factor source size

    /// Performs an in-place downsampling of the frequency components in the given real signal by an integer factor.
    let downsampleFrequencyReal factor (source : Buffer<float>) size = downsampleFrequency factor source size

    /// Performs an in-place downsampling of the frequency components in the given complex signal by an integer factor.
    let downsampleFrequencyComplex factor (source : Buffer<Complex>) size = downsampleFrequency factor source size

    /// Performs an in-place scaling of the given real signal by the given real factor.
    let scaleReal (factor : float) (source : Buffer<float>) size = scale factor source size

    /// Performs an in-place scaling of the given complex signal by the given real factor.
    let scaleComplex (factor : float) (source : Buffer<Complex>) size = scale factor source size

    /// Performs an in-place windowing of the given real signal by the given window (of the size).
    let windowReal (win : Buffer<float>) (destination : Buffer<float>)  size = window win destination size

    /// Performs an in-place windowing of the given complex signal by the given window (of the size).
    let windowComplex (win : Buffer<float>) (destination : Buffer<Complex>) size = window win destination size

    /// Adds two real signals together.
    let addReal (source : Buffer<float>) (destination : Buffer<float>) size = add source destination size

     /// Adds two complex signals together.
    let addComplex (source : Buffer<Complex>) (destination : Buffer<Complex>) size = add source destination size