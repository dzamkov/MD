using System;
using System.Collections.Generic;
using System.Linq;

namespace MD.UI
{
    /// <summary>
    /// A two-dimensional axis-aligned rectangle.
    /// </summary>
    public struct Rectangle
    {
        public Rectangle(Point TopLeft, Point BottomRight)
        {
            this.TopLeft = TopLeft;
            this.BottomRight = BottomRight;
        }

        public Rectangle(double Left, double Top, double Right, double Bottom)
        {
            this.TopLeft = new Point(Left, Top);
            this.BottomRight = new Point(Right, Bottom);
        }

        /// <summary>
        /// The position of the top-left corner of this rectangle.
        /// </summary>
        public Point TopLeft;

        /// <summary>
        /// The position of the bottom-right corner of this rectangle.
        /// </summary>
        public Point BottomRight;

        /// <summary>
        /// Gets the position of the top-right corner of this rectangle.
        /// </summary>
        public Point TopRight
        {
            get
            {
                return new Point(this.BottomRight.X, this.TopLeft.Y);
            }
        }

        /// <summary>
        /// Gets the position of the bottom-left corner of this rectangle.
        /// </summary>
        public Point BottomLeft
        {
            get
            {
                return new Point(this.TopLeft.X, this.BottomRight.Y);
            }
        }

        /// <summary>
        /// Gets the horizontal position of the left edge of this rectangle.
        /// </summary>
        public double Left
        {
            get
            {
                return this.TopLeft.X;
            }
        }

        /// <summary>
        /// Gets the vertical position of the top edge of this rectangle.
        /// </summary>
        public double Top
        {
            get
            {
                return this.TopLeft.Y;
            }
        }

        /// <summary>
        /// Gets the horizontal position of the right edge of this rectangle.
        /// </summary>
        public double Right
        {
            get
            {
                return this.BottomRight.X;
            }
        }

        /// <summary>
        /// Gets the vertical position of the bottom edge of this rectangle.
        /// </summary>
        public double Bottom
        {
            get
            {
                return this.BottomRight.Y;
            }
        }
    }
}
