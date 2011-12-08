namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// Contains functions for constructing and manipulating discrete signals.
module DSignal =
    
    /// Copies a signal of a given size from the given source to the given destination.
    let inline copy (source : nativeptr<'a>) (destination : nativeptr<'a>) size = 
        let mutable index = 0
        while index < size do
            NativePtr.get source index |> NativePtr.set destination index
            index <- index + 1

    /// Performs an in-place downsampling of the given signal by a factor of two.
    let inline downsample (source : nativeptr<'a>) (half : 'b) size =
        let mutable sindex = 0
        let mutable dindex = 0
        while sindex < size do
            let avg = ((NativePtr.get source sindex) + (NativePtr.get source (sindex + 1))) * half
            NativePtr.set source dindex avg
            sindex <- sindex + 2
            dindex <- dindex + 1

    /// Performs an in-place scaling of the given signal by the given real factor.
    let inline scale (factor : 'b) (source : nativeptr<'a>) size =
        let mutable index = 0
        while index < size do
            (NativePtr.get source index) * factor |> NativePtr.set source index
            index <- index + 1

    /// Converts a real signal to a complex signal.
    let convertComplex (source : nativeptr<float>) (destination : nativeptr<Complex>) size =
        let mutable index = 0
        while index < size do
            new Complex (NativePtr.get source index, 0.0) |> NativePtr.set destination index
            index <- index + 1

    /// Gets the absolute value of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let absReal (source : nativeptr<Complex>) (destination : nativeptr<float>) size =
        let mutable index = 0
        while index < size do
            (NativePtr.get source index).Abs |> NativePtr.set destination index
            index <- index + 1

    /// Gets the real part of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let real (source : nativeptr<Complex>) (destination : nativeptr<float>) size =
        let mutable index = 0
        while index < size do
            (NativePtr.get source index).Real |> NativePtr.set destination index
            index <- index + 1

    /// Gets the imaginary part of the given complex signal. Note that source and destination may be the same
    /// for an in-place operation.
    let imag (source : nativeptr<Complex>) (destination : nativeptr<float>) size =
        let mutable index = 0
        while index < size do
            (NativePtr.get source index).Imag |> NativePtr.set destination index
            index <- index + 1

    /// Performs an in-place conjugation of a complex signal.
    let conjugate (source : nativeptr<Complex>) size =
        let fptr = NativePtr.toNativeInt source |> NativePtr.ofNativeInt<float>
        let fptr = NativePtr.add fptr 1
        let mutable index = 0
        while index < size * 2 do
            (NativePtr.get fptr index) * -1.0 |> NativePtr.set fptr index
            index <- index + 2

    /// Copies a real signal of a given size from the given source to the given destination.
    let copyReal (source : nativeptr<float>) (destination : nativeptr<float>) size = copy source destination size

    /// Copies a complex signal of a given size from the given source to the given destination.
    let copyComplex (source : nativeptr<Complex>) (destination : nativeptr<Complex>) size = copy source destination size
        
    /// Performs an in-place downsampling of the given real signal by a factor of two.
    let downsampleReal (source : nativeptr<float>) size = downsample source 0.5 size

    /// Performs an in-place downsampling of the given complex signal by a factor of two.
    let downsampleComplex (source : nativeptr<Complex>) size = downsample source 0.5 size

    /// Performs an in-place scaling of the given real signal by the given real factor.
    let scaleReal (factor : float) (source : nativeptr<float>) size = scale factor source size

    /// Performs an in-place scaling of the given complex signal by the given real factor.
    let scaleComplex (factor : float) (source : nativeptr<Complex>) size = scale factor source size