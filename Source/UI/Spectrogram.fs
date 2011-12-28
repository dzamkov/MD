namespace MD.UI

open MD
open MD.Util
open MD.DSP
open System
open System.Collections.Generic

/// Defines a coloring for components in a spectrogram, based on the relative frequency (where 0.0 is 0 hz and 
/// 1.0 is the Nyquist frequency) and component value.
type SpectrogramColoring = Map<float * Complex, Color>

/// Contains parameters for a spectrogram.
type SpectrogramParameters = {
    
    /// The monochannel sample data for the spectrogram
    Samples : Data<float>

    /// The window to use for the spectrogram.
    Window : Window

    /// The size of the window, in samples.
    WindowSize : float

    /// The coloring method for the spectrogram.
    Coloring : SpectrogramColoring

    }
    
/// A tile for a spectrogram tile image.
type SpectrogramTile = {

    /// The first time sample in the range of this spectrogram tile.
    MinTime : uint64

    /// The amount of time samples this spectrogram tile covers.
    TimeRange : uint64

    /// The first frequency sample in the range of this spectrogram tile.
    MinFrequency : int

    /// The amount of frequency samples this spectrogram tile covers. This must be a power
    /// of two less than or equal to the window size.
    FrequencyRange : int

    /// The image size of the tile. Height should be a power of two.
    Size : ImageSize

    }