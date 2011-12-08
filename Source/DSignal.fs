namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// Contains functions for constructing and manipulating discrete signals.
module DSignal =
    
    /// Copies a real signal of a given size from the given source to the given destination.
    let copyReal (source : nativeptr<double>) (destination : nativeptr<double>) size =
        let mutable index = 0
        while index < size do
            NativePtr.get source index |> NativePtr.set destination index
            index <- index + 1

    /// Performs an in-place downsampling of the given real signal by a factor of two.
    let downsampleReal (source : nativeptr<double>) size =
        let mutable sindex = 0
        let mutable dindex = 0
        while sindex < size do
            let avg = ((NativePtr.get source sindex) + (NativePtr.get source (sindex + 1))) / 2.0
            NativePtr.set source dindex avg
            sindex <- sindex + 2
            dindex <- dindex + 1