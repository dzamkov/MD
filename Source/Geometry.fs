namespace MD

/// Represents a point or offset in two-dimensional space.
type Point (x : double, y : double) =
    struct
        /// Adds two offsets.
        static member (+) (a : Point, b : Point) =
            new Point (a.X + b.X, a.Y + b.Y)

        /// Subtracts two offsets.
        static member (-) (a : Point, b : Point) =
            new Point (a.X - b.X, a.Y - b.Y)

        /// Scales an offset by a certain amount.
        static member (*) (a : Point, b : double) =
            new Point (a.X * b, a.Y * b)

        /// Scales an offset by a certain amount.
        static member (/) (a : Point, b : double) =
            new Point (a.X / b, a.Y / b)

        /// Doesn't do anything.
        static member (~+) (a : Point) = a

        /// Negates an offset.
        static member (~-) (a : Point) = new Point (-a.X, -a.Y)

        /// Gets the horizontal component of this point.
        member this.X = x

        /// Gets the vertical component of this point.
        member this.Y = y

        /// Gets the length of this offset.
        member this.Length = sqrt (x * x + y * y)

        /// Gets the angle of this offset.
        member this.Angle = atan2 y x

        /// Gets an offset perpendicular to this offset, with the same length.
        member this.Cross = new Point (-y, x)
    end