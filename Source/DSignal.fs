﻿namespace MD

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
        
    /// Performs an in-place downsampling of the given real signal by a factor of two.
    let downsampleReal factor (source : Buffer<float>) size = downsample factor source size

    /// Performs an in-place downsampling of the given complex signal by a factor of two.
    let downsampleComplex factor (source : Buffer<Complex>) size = downsample factor source size

    /// Performs an in-place scaling of the given real signal by the given real factor.
    let scaleReal (factor : float) (source : Buffer<float>) size = scale factor source size

    /// Performs an in-place scaling of the given complex signal by the given real factor.
    let scaleComplex (factor : float) (source : Buffer<Complex>) size = scale factor source size

    /// Performs an in-place windowing of the given real signal by the given window (of the size).
    let windowReal (win : Buffer<float>) (destination : Buffer<float>)  size = window win destination size

    /// Performs an in-place windowing of the given complex signal by the given window (of the size).
    let windowComplex (win : Buffer<float>) (destination : Buffer<Complex>) size = window win destination size