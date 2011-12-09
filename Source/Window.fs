﻿namespace MD

open Util
open System
open Microsoft.FSharp.NativeInterop

/// A windowing function on the interval (-1/2, 1/2) for use in signal processing.
type Window = float -> float

/// Constains functions for constructing and manipulating windowing functions.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Window = 

    /// The rectangular window
    let rect : Window = fun x -> 1.0

    /// The Hann window
    let hann : Window = fun x -> 1.0 + cos (2.0 * Math.PI  * x)

    /// The Hamming window
    let hamming : Window = fun x -> 0.54 + 0.46 * cos (2.0 * Math.PI  * x)

    /// Constructs a normalized instance of the given window.
    let construct (window : Window) (destination : nativeptr<float>) size =
        
        // Create unscaled values
        let delta = 1.0 / float size
        let mutable parameter = -0.5 + delta * 0.5
        let mutable index = 0
        let mutable total = 0.0
        while index < size do
            let value = window parameter
            NativePtr.set destination index value
            total <- total + value
            parameter <- parameter + delta
            index <- index + 1

        // Normalize
        DSignal.scaleReal (1.0 / total) destination size