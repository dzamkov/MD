using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace MD.UI
{
    /// <summary>
    /// A two-dimensional position or offset (vector).
    /// </summary>
    public struct Point
    {
        public Point(double X, double Y)
        {
            this.X = X;
            this.Y = Y;
        }

        /// <summary>
        /// The horizontal component of the point.
        /// </summary>
        public double X;

        /// <summary>
        /// The vertical component of the point.
        /// </summary>
        public double Y;

        /// <summary>
        /// Gets the square of the length of this point offset (vector). This function is quicker to compute than the actual length
        /// because it avoids a square root, which may be costly.
        /// </summary>
        public double SquareLength
        {
            get
            {
                return this.X * this.X + this.Y * this.Y;
            }
        }

        /// <summary>
        /// Gets the length of the vector.
        /// </summary>
        public double Length
        {
            get
            {
                return Math.Sqrt(this.SquareLength);
            }
        }

        /// <summary>
        /// Gets the ratio between the horizontal and vertical components of this point.
        /// </summary>
        public double AspectRatio
        {
            get
            {
                return this.X / this.Y;
            }
        }

        /// <summary>
        /// Creates a unit vector (point offset) for the specified angle in radians.
        /// </summary>
        public static Point Unit(double Angle)
        {
            return new Point(Math.Sin(Angle), Math.Cos(Angle));
        }

        /// <summary>
        /// Scales the point by the given point.
        /// </summary>
        public Point Scale(Point Scale)
        {
            return new Point(this.X * Scale.X, this.Y * Scale.Y);
        }

        /// <summary>
        /// Gets the angle of this point (representing an offset) in radians.
        /// </summary>
        public double Angle
        {
            get
            {
                return Math.Atan2(this.Y, this.X);
            }
        }

        /// <summary>
        /// Gets the dot product of two points (representing offsets).
        /// </summary>
        public static double Dot(Point A, Point B)
        {
            return A.X * B.X + A.Y * B.Y;
        }

        public static implicit operator PointF(Point Vector)
        {
            return new PointF((float)Vector.X, (float)Vector.Y);
        }

        public static Point operator -(Point A, Point B)
        {
            return new Point(A.X - B.X, A.Y - B.Y);
        }

        public static Point operator -(Point A)
        {
            return new Point(-A.X, -A.Y);
        }

        public static Point operator +(Point A, Point B)
        {
            return new Point(A.X + B.X, A.Y + B.Y);
        }

        public static Point operator *(Point A, double B)
        {
            return new Point(A.X * B, A.Y * B);
        }
    }
}
