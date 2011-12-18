namespace MD.UI

/// Represents a color (with no transparency information).
type Color (r : float, g : float, b : float) =
    struct

        /// Gets a completely white color.
        static member White = new Color (1.0, 1.0, 1.0)

        /// Gets a completely black color.
        static member Black = new Color (0.0, 0.0, 0.0)

        /// Creates a color based on its RGB representation.
        static member RGB (r : float, g : float, b : float) =
            new Color (r, g, b)

        /// Blends two colors using the given amount (between 0.0 and 1.0) to determine
        /// what portion of the final color is the second color.
        static member Blend (a : Color, b : Color, amount : float) =
            let am = 1.0 - amount
            let bm = amount
            new Color (a.R * am + b.R * bm, a.G * am + b.G * bm, a.B * am + b.B * bm)

        /// Scales all components in a color by a certain amount.
        static member (*) (a : Color, b : float) =
            new Color (a.R * b, a.G * b, a.B * b)

        /// Modulates a color with another color.
        static member (*) (a : Color, b : Color) =
            new Color (a.R * b.R, a.G * b.G, a.B * b.B)

        /// Gets the red component of this color.
        member this.R = r

        /// Gets the green component of this color.
        member this.G = g

        /// Gets the blue component of this color.
        member this.B = b

        /// Gets the byte representation of the red component of this color.
        member this.RByte = byte (r * 255.99)

        /// Gets the byte representation of the green component of this color.
        member this.GByte = byte (g * 255.99)

        /// Gets the byte representation of the blue component of this color.
        member this.BByte = byte (b * 255.99)

        /// Gets the relative lightness of this color between 0.0 and 1.0.
        member this.Lightness = (r + g + b) / 3.0
    end

/// A stop, or control point, in a gradient.
type GradientStop = {

    /// The value this stop occurs at.
    Value : float

    /// The color of this stop.
    Color : Color

    }

/// A continuous mapping of real values to colors.
type Gradient (stops : GradientStop[]) =
    
    /// Gets the stops for this gradient. Note that there must be at least one stop and the stop(s)
    /// are given in order of ascending value.
    member this.Stops = stops

    /// Gets the minimum value for which this gradient has a unique color.
    member this.Minimum = stops.[0].Value

    /// Gets the maximum value for which this gradient has a unique color.
    member this.Maximum = stops.[stops.Length - 1].Value

    /// Gets the color of the gradient for the given value.
    member this.GetColor value = 
        let rec search low lowStop high highStop value =
            if high = low + 1 then 
                Color.Blend (lowStop.Color, highStop.Color, (value - lowStop.Value) / (highStop.Value - lowStop.Value))
            else
                let mid = (low + high) / 2
                let midStop = stops.[mid]
                if value < midStop.Value then search low lowStop mid midStop value
                else search mid midStop high highStop value
        let low = 0
        let high = stops.Length - 1
        let lowStop = stops.[low]
        let highStop = stops.[high]
        match value with
        | value when value <= lowStop.Value -> lowStop.Color
        | value when value >= highStop.Value -> highStop.Color
        | _ -> search low lowStop high highStop value
                    

/// Represents a color with transparency information.
type Paint (alpha : float, pre : Color) =
    struct

        /// Gets a completely white paint.
        static member White = new Paint (1.0, Color.White)

        /// Gets a completely black paint.
        static member Black = new Paint (1.0, Color.Black)

        /// Gets a completely transparent paint.
        static member Transparent = new Paint (0.0, Color.White)

        /// Creates a paint based on its post-multiplied argb representation.
        static member ARGB (a : float, source : Color) =
            new Paint (a, source * a)

        /// Creates a paint based on its post-multiplied argb representation.
        static member ARGB (a : float, r : float, g : float, b : float) =
            new Paint (a, new Color (a * r, a * g, a * b))

        /// Creates an opaque paint based on its rgb representation.
        static member RGB (source : Color) =
            new Paint (1.0, source)

        /// Creates an opaque paint based on its rgb representation.
        static member RGB (r : float, g : float, b : float) =
            new Paint (1.0, new Color (r, g, b))

        /// Modulates a paint with a color.
        static member (*) (a : Paint, b : Color) =
            new Paint (a.Alpha, a.AdditiveColor * b)

        // Modulates a paint with another paint.
        static member (*) (a : Paint, b : Paint) =
            new Paint (a.Alpha * b.Alpha, a.AdditiveColor * b.AdditiveColor)

        /// Blends two paints using the given amount (between 0.0 and 1.0) to determine
        /// what portion of the final paint is the second paint.
        static member Blend (a : Paint, b : Paint, amount : float) =
            let am = 1.0 - amount
            let bm = amount
            let ac : Color = a.AdditiveColor
            let bc : Color = b.AdditiveColor
            new Paint (a.Alpha * am + b.Alpha * bm, new Color (ac.R * am + bc.R * bm, ac.G * am + bc.G * bm, ac.B * am + bc.B * bm))

        /// Gets the color added by this paint when composited, that is, the actual color multiplied by alpha.
        member this.AdditiveColor = pre

        /// Gets the color this paint appears as, or white if this paint is completely transparent.
        member this.Color = 
            if alpha = 0.0 
            then new Color (1.0, 1.0, 1.0)
            else pre * (1.0 / alpha)

        /// Gets the transparency of this paint, with 1.0 indicating fully opaque and 0.0 indicating fully transparent.
        member this.Alpha =alpha

        /// Gets the byte representation of the alpha component of this paint.
        member this.AlphaByte = byte (alpha * 255.99)

        /// Gets wether this paint is fully opaque.
        member this.IsOpaque = (alpha = 1.0)

        /// Gets wether this paint is fully transparent.
        member this.IsTransparent = (alpha = 0.0)
    end