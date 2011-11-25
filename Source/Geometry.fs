namespace MD

/// A point or offset in two-dimensional space.
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

        override this.ToString () = x.ToString () + ", " + y.ToString ()
    end

/// An axis-aligned rectangle in two-dimensional space.
type Rectangle (left : double, top : double, right : double, bottom : double) =
    struct

        new (topLeft : Point, bottomRight : Point) = new Rectangle (topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y)

        /// Adds an offset to this rectangle.
        static member (+) (a : Rectangle, b : Point) =
            new Rectangle (a.Left + b.X, a.Top + b.Y, a.Right + b.X, a.Bottom + b.Y)

        /// Adds an offset from this rectangle.
        static member (-) (a : Rectangle, b : Point) =
            new Rectangle (a.Left - b.X, a.Top - b.Y, a.Right - b.X, a.Bottom - b.Y)
        
        /// Gets the horizontal component of the left edge of this rectangle.
        member this.Left = left

        /// Gets the vertical component of the top edge of this rectangle.
        member this.Top = top

        /// Gets the horizontal component of the right edge of this rectangle.
        member this.Right = right

        /// Gets the vertical component of the bottom edge of this rectangle.
        member this.Bottom = bottom

        /// Gets the position of the top-left corner of this rectangle.
        member this.TopLeft = new Point (left, top)

        /// Gets the position of the top-right corner of this rectangle.
        member this.TopRight = new Point (right, top)

        /// Gets the position of the bottom-left corner of this rectangle.
        member this.BottomLeft = new Point (left, bottom)

        /// Gets the position of the bottom-right corner of this rectangle.
        member this.BottomRight = new Point (right, bottom)

        /// Gets the area of this rectangle.
        member this.Area = (right - left) * (bottom - top)
    end

/// An affline transform in two-dimensional space.
type Transform (offset : Point, x : Point, y : Point) =
    struct
        /// Gets the identity transform.
        static member Identity = new Transform (new Point (0.0, 0.0), new Point (1.0, 0.0), new Point (0.0, 1.0))

        /// Composes two transforms to be applied in the order they are given.
        static member (*) (a : Transform, b : Transform) =
            new Transform (b.Apply a.Offset, b.ApplyDirection a.X, b.ApplyDirection a.Y)

        /// Gets the offset of this transform (the position of the origin when transformed).
        member this.Offset = offset

        /// Gets the x component of this transform (the amount that the horizontal component is multiplied by when transformed).
        member this.X = x

        /// Gets the y component of this transform (the amount that the vertical component is multiplied by when transformed).
        member this.Y = y

        /// Applies this transform to a point.
        member this.Apply (point : Point) = offset + (x * point.X) + (y * point.Y)

        /// Applies this transform to a directional offset.
        member this.ApplyDirection (offset : Point) = (x * offset.X) + (y * offset.Y)

        /// Gets the determinant of this transform.
        member this.Determinant = (x.X * y.Y) - (x.Y * y.X)

        /// Gets the inverse of this transform.
        member this.Inverse =
            let idet = 1.0 / this.Determinant
            new Transform (
                new Point ((y.Y * offset.X - y.X * offset.Y) * -idet, (x.Y * offset.X - x.X * offset.Y) * idet),
                new Point (y.Y * idet, x.Y * -idet),
                new Point (y.X * -idet, x.X * idet))
    end