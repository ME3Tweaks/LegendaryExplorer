/* 
 * Copyright (c) 2003-2006, University of Maryland
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided
 * that the following conditions are met:
 * 
 *		Redistributions of source code must retain the above copyright notice, this list of conditions
 *		and the following disclaimer.
 * 
 *		Redistributions in binary form must reproduce the above copyright notice, this list of conditions
 *		and the following disclaimer in the documentation and/or other materials provided with the
 *		distribution.
 * 
 *		Neither the name of the University of Maryland nor the names of its contributors may be used to
 *		endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
 * TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * Piccolo was written at the Human-Computer Interaction Laboratory www.cs.umd.edu/hcil by Jesse Grosjean
 * and ported to C# by Aaron Clamage under the supervision of Ben Bederson.  The Piccolo website is
 * www.cs.umd.edu/hcil/piccolo.
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Piccolo.Util {
    /// <summary>
    /// <b>PMatrix</b> is a wrapper around a <see cref="Matrix3x2">
    /// System.Numerics.Matrix3x2</see> that includes several convenience methods.  
    /// </summary>
    public sealed class PMatrix : ICloneable {
		#region Fields
		private Matrix3x2 matrix;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PMatrix initialized to the identity matrix.
		/// </summary>
		public PMatrix() {
			matrix = Matrix3x2.Identity;
		}

		/// <summary>
		/// Constructs a new PMatrix initialized with the specified elements.
		/// </summary>
		/// <param name="m11">
		/// The value in the first row and first column of the new <see cref="PMatrix"/>.
		/// </param>
		/// <param name="m12">
		/// The value in the first row and second column of the new <see cref="PMatrix"/>.
		/// </param>
		/// <param name="m21">
		/// The value in the second row and first column of the new <see cref="PMatrix"/>. 
		/// </param>
		/// <param name="m22">
		/// The value in the second row and second column of the new <see cref="PMatrix"/>.
		/// </param>
		/// <param name="dx">
		/// The value in the third row and first column of the new <see cref="PMatrix"/>. 
		/// </param>
		/// <param name="dy">
		/// The value in the third row and second column of the new <see cref="PMatrix"/>. 
		/// </param>
		public PMatrix(float m11, float m12, float m21, float m22, float dx, float dy) {
			matrix = new Matrix3x2(m11, m12, m21, m22, dx, dy);
		}

		/// <summary>
		/// Constructs a new PMatrix initialized to the values in the given
		/// <see cref="Matrix3x2">Matrix</see>.
		/// </summary>
		/// <param name="matrix">A <see cref="Matrix3x2">Matrix</see>
		/// to use when initializing this <see cref="PMatrix"/>
		/// </param>
		public PMatrix(Matrix3x2 matrix) {
			this.matrix = matrix;
		}
		#endregion

		#region Basic
		/// <summary>
		/// Gets or sets the underlying matrix object.
		/// </summary>
		/// <value>The underlying matrix object.</value>
		public Matrix3x2 Matrix {
			get => matrix;
            set => matrix = value;
        }

        /// <summary>
		/// Creates an exact copy of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <returns>The <see cref="PMatrix"/> object that his method creates.</returns>
		public object Clone()
        {
            return new PMatrix(matrix);
        }

        public Matrix GetGdiMatrix()
        {
            return new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
        }

		/// <summary>
		/// Gets an array of floating-point values that represents the elements of this
		/// <see cref="PMatrix"/> object.
		/// </summary>
		/// <value>
		/// An array of floating-point values that represents the elements of this
		/// <see cref="PMatrix"/> object.
		/// </value>
		/// <remarks>
		/// The elements m11, m12, m21, m22, dx, dy of the <see cref="PMatrix"/> object are
		/// represented by the values in the array in that order.
		/// </remarks>
		public float[] Elements => new []{matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32};

        /// <summary>
		/// Gets a value indicating whether this <see cref="PMatrix"/> object is the identity
		/// matrix.
		/// </summary>
		/// <value>This property is true if this Matrix is identity; otherwise, false.</value>
		public bool IsIdentity => matrix.IsIdentity;

		/// <summary>
		/// Gets a value indicating whether this <see cref="PMatrix"/> object is invertible.
		/// </summary>
		/// <value>
		/// This property is true if this <see cref="PMatrix"/> is invertible; otherwise, false.
		/// </value>
		public bool IsInvertible => MathF.Abs(matrix.GetDeterminant()) >= float.Epsilon;

        /// <summary>
		/// Multiplies this <see cref="PMatrix"/> object by the specified <see cref="PMatrix"/>
		/// object by prepending the specified Matrix.
		/// </summary>
		/// <param name="otherMatrix">
		/// The <see cref="PMatrix"/> object by which this <see cref="PMatrix"/> object is to be
		/// multiplied.
		/// </param>
		public void Multiply(PMatrix otherMatrix) {
			matrix = otherMatrix.matrix * matrix;
		}

		/// <summary>
		/// Multiplies this <see cref="PMatrix"/> object by the specified <see cref="Matrix3x2"/>
		/// by prepending the specified Matrix.
		/// </summary>
		/// <param name="otherMatrix">
		/// The <see cref="Matrix3x2"/> object by which this <see cref="PMatrix"/> object is to be
		/// multiplied.
		/// </param>
		public void Multiply(Matrix3x2 otherMatrix)
        {
            matrix = otherMatrix * matrix;
        }

		/// <summary>
		/// Inverts this <see cref="PMatrix"/> object, if it is invertible.
		/// </summary>
		public void Invert() {
            Matrix3x2.Invert(matrix, out matrix);
		}

		/// <summary>
		/// Resets this <see cref="PMatrix"/> object to have the elements of the identity matrix.
		/// </summary>
		public void Reset() {
			matrix = Matrix3x2.Identity;
		}
		#endregion

		#region Transform Matrix
		/// <summary>
		/// Gets or sets the x translation value (the dx value, or the element in the third
		/// row and first column) of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <value>The x translation value of this <see cref="PMatrix"/>.</value>
		public float OffsetX {
			get => matrix.M31;
            set => matrix.M31 = value;
        }

		/// <summary>
		/// Gets or sets the y translation value (the dy value, or the element in the third
		/// row and second column) of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <value>The y translation value of this <see cref="PMatrix"/>.</value>
		public float OffsetY {
			get => matrix.M32;
            set => matrix.M32 = value;
        }

		/// <summary>
		/// Applies the specified translation vector (dx and dy) to this <see cref="PMatrix"/>
		/// object by prepending the translation vector.
		/// </summary>
		/// <param name="dx">
		/// The x value by which to translate this <see cref="PMatrix"/>.
		/// </param>
		/// <param name="dy">
		/// The y value by which to translate this <see cref="PMatrix"/>.
		/// </param>
		public void TranslateBy(float dx, float dy) {
			matrix = Matrix3x2.CreateTranslation(dx, dy) * matrix;
		}

		/// <summary>
		/// Gets or sets the scale value of this <see cref="PMatrix"/>.
		/// </summary>
		/// <value>The scale value of this <see cref="PMatrix"/>.</value>
		public float Scale {
			get	{
				PointF[] pts = {new(0, 0), new(1, 0)};
				TransformPoints(pts);
				return PUtil.DistanceBetweenPoints(pts[0], pts[1]);
			}
			set {
				if (Scale != 0) {
					ScaleBy(value / Scale);
				} else {
					throw new InvalidOperationException("Can't set Scale when Scale is equal to 0");
				}				
			}
		}

		/// <summary>
		/// Applies the specified scale vector to this <see cref="PMatrix"/> object by
		/// prepending the scale vector.
		/// </summary>
		/// <param name="scale">
		/// The value by which to scale this <see cref="PMatrix"/> along both axes.
		/// </param>
		/// <remarks>
		/// This value will be applied to the current scale value of the matrix.  This is not
		/// the same as setting the <see cref="PMatrix.Scale"/> directly.
		/// </remarks>
		public void ScaleBy(float scale) {
			ScaleBy(scale, scale);
		}

		/// <summary>
		/// Applies the specified scale vector to this <see cref="PMatrix"/> object by
		/// prepending the scale vector.
		/// </summary>
		/// <param name="scaleX">
		/// The value by which to scale this <see cref="PMatrix"/> in the x-axis direction. 
		/// </param>
		/// <param name="scaleY">
		/// The value by which to scale this <see cref="PMatrix"/> in the y-axis direction. 
		/// </param>
		/// <remarks>
		/// This value will be applied to the current scale value of the matrix.  This is not
		/// the same as setting the <see cref="PMatrix.Scale"/> directly.
		/// </remarks>
		public void ScaleBy(float scaleX, float scaleY) {
			matrix = Matrix3x2.CreateScale(scaleX, scaleY) * matrix;
		}

		/// <summary>
		/// Scale about the specified point.  Applies the specified scale vector to this
		/// <see cref="PMatrix"/> object by translating to the given point before prepending the
		/// scale vector.
		/// </summary>
		/// <param name="scale">
		/// The value by which to scale this <see cref="PMatrix"/> along both axes, around the point
		/// (x, y).
		/// </param>
		/// <param name="x">
		/// The x-coordinate of the point to scale about.
		/// </param>
		/// <param name="y">
		/// The y-coordinate of the point to scale about.
		/// </param>
		/// <remarks>
		/// This value will be applied to the current scale value of the matrix.  This is not
		/// the same as setting the <see cref="PMatrix.Scale"/> directly.
		/// </remarks>
		public void ScaleBy(float scale, float x, float y) {
			TranslateBy(x, y);
			ScaleBy(scale);
            TranslateBy(-x, -y);
		}

		/// <summary>
		/// Gets or sets the rotation value of this <see cref="PMatrix"/>, in degrees.
		/// </summary>
		/// <value>
		/// The rotation value of this <see cref="PMatrix"/>, in degrees.  The value will be
		/// between 0 and 360.
		/// </value>
		public float Rotation {
			get	{
				var p1 = new PointF(0, 0);
				var p2 = new PointF(1, 0);
				PointF tp1 = Transform(p1);
				PointF tp2 = Transform(p2);

				float dy = MathF.Abs(tp2.Y - tp1.Y);
				float l = PUtil.DistanceBetweenPoints(tp1, tp2);
				float rotation = MathF.Asin(dy / l);

				// correct for quadrant
				if (tp2.Y - tp1.Y > 0) {
					if (tp2.X - tp1.X < 0) {
						rotation = MathF.PI - rotation;
					}
				} else {
					if (tp2.X - tp1.X > 0) {
						rotation = 2 * MathF.PI - rotation;
					} else {
						rotation += MathF.PI;
					}
				}

				// convert to degrees
				return rotation * (180 / MathF.PI);
			}
			set => RotateBy(value - Rotation);
        }

		/// <summary>
		/// Prepend to this <see cref="PMatrix"/> object a clockwise rotation, around the origin
		/// and by the specified angle.
		/// <see cref="PMatrix"/> object.
		/// </summary>
		/// <param name="theta">The angle of the rotation, in degrees.</param>
		/// <remarks>
		/// This value will be applied to the current rotation value of the matrix.  This is not
		/// the same as setting the <see cref="PMatrix.Rotation"/> directly.
		/// </remarks>
		public void RotateBy(float theta) {
			matrix = Matrix3x2.CreateRotation(theta * (MathF.PI / 180)) * matrix;
		}

		/// <summary>
		/// Applies a clockwise rotation about the specified point to this <see cref="PMatrix"/>
		/// object by prepending the rotation.
		/// </summary>
		/// <param name="theta">The angle of the rotation, in degrees.</param>
		/// <param name="x">The x-coordinate of the point to rotate about.</param>
		/// <param name="y">The y-coordinate of the point to rotate about.</param>
		/// <remarks>
		/// This value will be applied to the current rotation value of the matrix.  This is not
		/// the same as setting the <see cref="PMatrix.Rotation"/> directly.
		/// </remarks>
		public void RotateBy(float theta, float x, float y) {
			matrix = Matrix3x2.CreateRotation(theta * (MathF.PI / 180), new Vector2(x, y)) * matrix;
		}

		/// <summary>
		/// Applies the specified skew to this <see cref="PMatrix"/> object by prepending
		/// the skew transformation.
		/// </summary>
		/// <param name="radiansX">The skew angle in the x dimension in degrees</param>
		/// <param name="radiansY">The skew angle in the y dimension in degrees</param>
		public void SkewBy(float radiansX, float radiansY) {
			matrix = Matrix3x2.CreateSkew(radiansX, radiansY) * matrix;
		}
		#endregion

		#region Transform Objects
		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given point.
		/// </summary>
		/// <param name="point">The point to transform.</param>
		/// <returns>The transformed point.</returns>
		public PointF Transform(PointF point) {
			Span<PointF> pts = stackalloc PointF[1];
            pts[0] = point;
			TransformPoints(pts);
			return pts[0];
		}

		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given size.
		/// </summary>
		/// <param name="size">The size to be transformed.</param>
		/// <returns>The transformed size.</returns>
		public SizeF Transform(SizeF size) {
			PointF[] pts = {new(size.Width, size.Height)};
			TransformVectors(pts);
			return new SizeF(pts[0].X, pts[0].Y);
		}

        /// <summary>
        /// Applies the geometric transform represented by this <see cref="PMatrix"/> object to all
        /// of the points in the given array.
        /// <see cref="PMatrix"/>.
        /// </summary>
        /// <param name="pts">The array of points to transform.</param>
        public void TransformPoints(Span<PointF> pts) => TransformPoints(matrix, pts);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TransformPoints(Matrix3x2 m, Span<PointF> pts)
        {
            int count = pts.Length;
            for (int i = 0; i < count; i++)
            {
                float x = m.M11 * pts[i].X + m.M21 * pts[i].Y + m.M31;
                float y = m.M12 * pts[i].X + m.M22 * pts[i].Y + m.M32;

                pts[i] = new PointF(x, y);
            }
        }

        /// <summary>
        /// Applies only the scale and rotate components of this <see cref="PMatrix"/> object to all
        /// of the points in the given array.
        /// </summary>
        /// <param name="pts">The array of points to transform.</param>
        public void TransformVectors(Span<PointF> pts) => TransformVectors(matrix, pts);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TransformVectors(Matrix3x2 m, Span<PointF> pts)
        {
            int count = pts.Length;
            for (int i = 0; i < count; i++)
            {
                float x = m.M11 * pts[i].X + m.M21 * pts[i].Y;
                float y = m.M12 * pts[i].X + m.M22 * pts[i].Y;

                pts[i] = new PointF(x, y);
            }
        }

		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given rectangle.
		/// </summary>
		/// <param name="rect">The rectangle to transform.</param>
		/// <returns>The transformed rectangle.</returns>
		public RectangleF Transform(RectangleF rect) => Transform(matrix, rect);

        private static RectangleF Transform(Matrix3x2 m, RectangleF rect)
        {
            float x = rect.X;
            float y = rect.Y;
            float width = rect.Width;
            float height = rect.Height;

            Span<PointF> pts4 = stackalloc PointF[4];
            pts4[0].X = x;
            pts4[0].Y = y;
            pts4[1].X = x + width;
            pts4[1].Y = y;
            pts4[2].X = x + width;
            pts4[2].Y = y + height;
            pts4[3].X = x;
            pts4[3].Y = y + height;

            TransformPoints(m, pts4);

            float minX = pts4[0].X;
            float minY = pts4[0].Y;
            float maxX = pts4[0].X;
            float maxY = pts4[0].Y;

            for (int i = 1; i < 4; i++)
            {
                x = pts4[i].X;
                y = pts4[i].Y;

                if (x < minX)
                {
                    minX = x;
                }
                if (y < minY)
                {
                    minY = y;
                }
                if (x > maxX)
                {
                    maxX = x;
                }
                if (y > maxY)
                {
                    maxY = y;
                }
            }

            rect.X = minX;
            rect.Y = minY;
            rect.Width = maxX - minX;
            rect.Height = maxY - minY;
            return rect;
        }

		/// <summary>
		/// Applies the inverse of the geometric transform represented by this
		/// <see cref="PMatrix"/> object to the given point.
		/// </summary>
		/// <param name="point">The point to transform.</param>
		/// <returns>The transformed point.</returns>
		public PointF InverseTransform(PointF point) {
			if (IsInvertible)
			{
				Matrix3x2.Invert(matrix, out Matrix3x2 temp);
				TransformPoints(temp, MemoryMarshal.CreateSpan(ref point, 1));
            }
			return point;
		}

		/// <summary>
		/// Applies the inverse of the geometric transform represented by this
		/// <see cref="PMatrix"/> object to the given size.
		/// </summary>
		/// <param name="size">The size to be transformed.</param>
		/// <returns>The transformed size.</returns>
		public SizeF InverseTransform(SizeF size) {
			if (IsInvertible)
			{
                Matrix3x2.Invert(matrix, out Matrix3x2 tempMatrix);
                var p = size.ToPointF();
                TransformVectors(tempMatrix, MemoryMarshal.CreateSpan(ref p, 1));
                return new SizeF(p);
            }
			return size;
		}

		/// <summary>
		/// Applies the inverse of the geometric transform represented by this
		/// <see cref="PMatrix"/> object to the given rectangle.
		/// </summary>
		/// <param name="rect">The rectangle to transform.</param>
		/// <returns>The transformed rectangle.</returns>
		public RectangleF InverseTransform(RectangleF rect) {
			if (IsInvertible)
			{
                Matrix3x2.Invert(matrix, out Matrix3x2 temp);
				return Transform(temp, rect);
			}
			return rect;
		}
		#endregion

		#region Debugging
		/// <summary>
		/// Overridden.  Returns a string representation of this object for debugging
		/// purposes.
		/// </summary>
		/// <returns>A string representation of this object.</returns>
		public override string ToString() {
			return base.ToString () + "[" + ElementString + "]";
		}

		/// <summary>
		/// Gets a string representation of the elements in this <see cref="PMatrix"/>.
		/// </summary>
        private string ElementString {
			get {
				var result = new StringBuilder();

				float[] elements = Elements;
				int length = elements.Length;
				for (int i = 0; i < elements.Length; i++) {
					result.Append(elements[i].ToString());
					if (i < length-1) result.Append(", ");
				}

				return result.ToString();
			}
		}
		#endregion
	}
}
