namespace MD

/// Represents a color (with no transparency information).
type Color (r : double, g : double, b : double) =
    struct

        /// Gets a completely white color.
        static member White = new Color (1.0, 1.0, 1.0)

        /// Gets a completely black color.
        static member Black = new Color (0.0, 0.0, 0.0)

        /// Creates a color based on its RGB representation.
        static member RGB (r : double, g : double, b : double) =
            new Color (r, g, b)

        /// Blends two colors using the given amount (between 0.0 and 1.0) to determine
        /// what portion of the final color is the second color.
        static member Blend (a : Color, b : Color, amount : double) =
            let am = 1.0 - amount
            let bm = amount
            new Color (a.R * am + b.R * bm, a.G * am + b.G * bm, a.B * am + b.B * bm)

        /// Scales all components in a color by a certain amount.
        static member (*) (a : Color, b : double) =
            new Color (a.R * b, a.G * b, a.B * b)

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

/// Represents a color with transparency information.
type Paint (a : double, pre : Color) =
    struct

        /// Gets a completely white paint.
        static member White = new Paint (1.0, Color.White)

        /// Gets a completely black paint.
        static member Black = new Paint (1.0, Color.Black)

        /// Gets a completely transparent paint.
        static member Transparent = new Paint (0.0, Color.White)

        /// Creates a paint based on its post-multiplied argb representation.
        static member ARGB (a : double, source : Color) =
            new Paint (a, source * a)

        /// Creates a paint based on its post-multiplied argb representation.
        static member ARGB (a : double, r : double, g : double, b : double) =
            new Paint (a, new Color (a * r, a * g, a * b))

        /// Blends two paints using the given amount (between 0.0 and 1.0) to determine
        /// what portion of the final paint is the second paint.
        static member Blend (a : Paint, b : Paint, amount : double) =
            let am = 1.0 - amount
            let bm = amount
            let ac : Color = a.AdditiveColor
            let bc : Color = b.AdditiveColor
            new Paint (a.Alpha * am + b.Alpha * bm, new Color (ac.R * am + bc.R * bm, ac.G * am + bc.G * bm, ac.B * am + bc.B * bm))

        /// Gets the color added by this paint when composited, that is, the visible color multiplied by alpha.
        member this.AdditiveColor = pre

        /// Gets the color this paint appears as, or white if this paint is completely transparent.
        member this.Color = 
            if a = 0.0 
            then new Color (1.0, 1.0, 1.0)
            else pre * (1.0 / a)

        /// Gets the transparency of this paint, with 1.0 indicating fully opaque and 0.0 indicating fully transparent.
        member this.Alpha = a

        /// Gets the byte representation of the alpha component of this paint.
        member this.AlphaByte = byte (a * 255.99)

        /// Gets wether this paint is fully opaque.
        member this.IsOpaque = (a = 1.0)

        /// Gets wether this paint is fully transparent.
        member this.IsTransparent = (a = 0.0)
    end