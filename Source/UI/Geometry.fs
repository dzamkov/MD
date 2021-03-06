﻿namespace MD.UI

open MD
open System

/// Identifies an axis in two-dimensional space.
type Axis =
    | Horizontal
    | Vertical

/// A point or offset in two-dimensional space.
type Point (x : float, y : float) =
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

        /// Computes the dot product of two offsets.
        static member (*) (a : Point, b : Point) = a.X * b.X + a.Y * b.Y

        /// Computes the wedge product of two offsets.
        static member (^^) (a : Point, b : Point) = a.X * b.Y - a.Y * b.X

        /// Doesn't do anything.
        static member (~+) (a : Point) = a

        /// Negates an offset.
        static member (~-) (a : Point) = new Point (-a.X, -a.Y)

        /// Scales a point by the multipliers given by another point.
        static member Scale (a : Point, b : Point) =
            new Point (a.X * b.X, a.Y * b.Y)

        /// Gets the given axis component of this point.
        member this.Item
            with get (axis : Axis) =
                match axis with
                | Horizontal -> x
                | Vertical -> y

        /// Gets the horizontal component of this point.
        member this.X = x

        /// Gets the vertical component of this point.
        member this.Y = y

        /// Gets the length of this offset.
        member this.Length = sqrt (x * x + y * y)

        /// Gets the normalized form of this offset.
        member this.Normal = this / this.Length

        /// Gets the angle of this offset.
        member this.Angle = atan2 y x

        /// Gets an offset perpendicular to this offset, with the same length.
        member this.Cross = new Point (-y, x)

        override this.ToString () = String.Format ("{0}, {1}", x, y)
    end

/// An axis-aligned rectangle in two-dimensional space.
type Rectangle (left : float, right : float, bottom : float, top : float) =
    struct

        /// Gets a rectangle that contains all points.
        static member Unbound = new Rectangle (-infinity, infinity, -infinity, infinity)

        /// Gets the unit square in the first quadrant.
        static member Unit = new Rectangle (0.0, 1.0, 0.0, 1.0)

        /// A square with edge length 2.0 centered on the origin.
        static member View = new Rectangle (-1.0, 1.0, -1.0, 1.0)

        /// Gets a rectangle that contains no points.
        static member Null = new Rectangle (infinity, -infinity, infinity, -infinity)

        /// Adds an offset to this rectangle.
        static member (+) (a : Rectangle, b : Point) =
            new Rectangle (a.Left + b.X, a.Right + b.X, a.Bottom + b.Y, a.Top + b.Y)

        /// Adds an offset from this rectangle.
        static member (-) (a : Rectangle, b : Point) =
            new Rectangle (a.Left - b.X, a.Right - b.X, a.Bottom - b.Y, a.Top - b.Y)

        /// Gets the intersection of two rectangles.
        static member (&&&) (a : Rectangle, b : Rectangle) =
            new Rectangle (max a.Left b.Left, min a.Right b.Right, max a.Bottom b.Bottom, min a.Top b.Top)

        /// Gets the union of two rectangles.
        static member (|||) (a : Rectangle, b : Rectangle) =
            new Rectangle (min a.Left b.Left, max a.Right b.Right, min a.Bottom b.Bottom, max a.Top b.Top)
        
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

        /// Gets the position of the center of this rectangle.
        member this.Center = new Point ((left + right) / 2.0, (bottom + top) / 2.0)

        /// Gets an offset for the size of this rectangle.
        member this.Size = new Point (right - left, top - bottom)

        /// Gets the width of this rectangle.
        member this.Width = right - left

        /// Gets the height of this rectangle.
        member this.Height = top - bottom

        /// Gets the area of this rectangle.
        member this.Area = (right - left) * (top - bottom)
    end

/// An affline transform in two-dimensional space.
type Transform (offset : Point, x : Point, y : Point) =
    struct
        /// Gets the identity transform.
        static member Identity = new Transform (new Point (0.0, 0.0), new Point (1.0, 0.0), new Point (0.0, 1.0))

        /// Composes two transforms to be applied in the order they are given.
        static member (*) (a : Transform, b : Transform) =
            new Transform (b.Apply a.Offset, b.ApplyDirection a.X, b.ApplyDirection a.Y)

        /// Applies a transform to a point.
        static member (*) (a : Transform, b : Point) = a.Apply b

        /// Applies a transform to a bounding rectangle.
        static member (*) (a : Transform, b : Rectangle) = a.ApplyBounds b

        /// Creates a rotation transform for a certain angle in radians.
        static member Rotate (angle : double) =
            new Transform (new Point (0.0, 0.0), new Point (cos angle, sin angle), new Point (-(sin angle), cos angle))

        /// Creates a scale transform with independant scale factors for each axis.
        static member Scale (horizontal : double, vertical : double) =
            new Transform (new Point (0.0, 0.0), new Point (horizontal, 0.0), new Point (0.0, vertical))

        /// Creates a scale transform with the given scale factor.
        static member Scale (amount : double) =
            new Transform (new Point (0.0, 0.0), new Point (amount, 0.0), new Point (0.0, amount))

        /// Creates a tranlation transform with the given offset.
        static member Translate (offset : Point) =
            new Transform (offset, new Point (1.0, 0.0), new Point (0.0, 1.0))

        /// Creates a transform from the unit square to the given rectangle.
        static member Place (rect : Rectangle) =
            new Transform (new Point (rect.Left, rect.Bottom), new Point (rect.Right - rect.Left, 0.0), new Point (0.0, rect.Top - rect.Bottom))

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

        /// Applies this transform to a bounding rectangle such that the result will be the smallest rectangle that contains
        /// all of the area in the transformed form of the input rectangle.
        member this.ApplyBounds (bounds : Rectangle) =
            let a, b, c, d = this * bounds.BottomLeft, this * bounds.BottomRight, this * bounds.TopRight, this * bounds.TopLeft
            let minX = a.X |> min b.X |> min c.X |> min d.X
            let minY = a.Y |> min b.Y |> min c.Y |> min d.Y
            let maxX = a.X |> max b.X |> max c.X |> max d.X
            let maxY = a.Y |> max b.Y |> max c.Y |> max d.Y
            new Rectangle (minX, maxX, minY, maxY)

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