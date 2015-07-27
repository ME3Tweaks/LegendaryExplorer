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
using System.Text;

using System.Runtime.Serialization;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PMatrix</b> is a wrapper around a <see cref="System.Drawing.Drawing2D.Matrix">
	/// System.Drawing.Drawing2D.Matrix</see> that includes several convenience methods.  
	/// </summary>
	[Serializable]
	public class PMatrix : ICloneable {
		#region Fields
		private Matrix matrix;

		private static PointF[] PTS4 = new PointF[4];
		private static PMatrix TEMP_MATRIX = new PMatrix();
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PMatrix initialized to the identity matrix.
		/// </summary>
		public PMatrix() {
			matrix = new Matrix();
		}

		/// <summary>
		/// Constructs a new PMatrix initialized to the geometric transform defined by the
		/// specified rectangle and array of points.
		/// </summary>
		/// <param name="rect">
		/// A <see cref="System.Drawing.Rectangle"/> structure that represents the rectangle
		/// to be transformed.
		/// </param>
		/// <param name="plgpts">
		/// An array of three <see cref="System.Drawing.Point"/> structures that represents
		/// the points of a parallelogram to which the upper-left, upper-right, and lower-left
		/// corners of the rectangle is to be transformed. The lower-right corner of the
		/// parallelogram is implied by the first three corners. 
		/// </param>
		/// <remarks>
		/// This method initializes the new PMatrix such that it represents the geometric
		/// transform that maps the rectangle specified by the rect parameter to the
		/// parallelogram defined by the three points in the plgpts parameter. The upper-left
		/// corner of the rectangle is mapped to the first point in the plgpts array, the
		/// upper-right corner is mapped to the second point, and the lower-left corner is
		/// mapped to the third point. The lower-left point of the parallelogram is implied
		/// by the first three.
		/// </remarks>
		public PMatrix(Rectangle rect, Point[] plgpts) {
			matrix = new Matrix(rect, plgpts);
		}

		/// <summary>
		/// Constructs a new PMatrix initialized to the geometric transform defined by the
		/// specified rectangle and array of points.
		/// </summary>
		/// <param name="rect">
		/// A <see cref="System.Drawing.RectangleF"/> structure that represents the rectangle
		/// to be transformed.
		/// </param>
		/// <param name="plgpts">
		/// An array of three <see cref="System.Drawing.PointF"/> structures that represents
		/// the points of a parallelogram to which the upper-left, upper-right, and lower-left
		/// corners of the rectangle is to be transformed. The lower-right corner of the
		/// parallelogram is implied by the first three corners. 
		/// </param>
		/// <remarks>
		/// This method initializes the new PMatrix such that it represents the geometric
		/// transform that maps the rectangle specified by the rect parameter to the
		/// parallelogram defined by the three points in the plgpts parameter. The upper-left
		/// corner of the rectangle is mapped to the first point in the plgpts array, the
		/// upper-right corner is mapped to the second point, and the lower-left corner is
		/// mapped to the third point. The lower-left point of the parallelogram is implied
		/// by the first three.
		/// </remarks>
		public PMatrix(RectangleF rect, PointF[] plgpts) {
			matrix = new Matrix(rect, plgpts);
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
			matrix = new Matrix(m11, m12, m21, m22, dx, dy);
		}

		/// <summary>
		/// Constructs a new PMatrix initialized to the values in the given
		/// <see cref="System.Drawing.Drawing2D.Matrix">Matrix</see>.
		/// </summary>
		/// <param name="matrix">A <see cref="System.Drawing.Drawing2D.Matrix">Matrix</see>
		/// to use when initializing this <see cref="PMatrix"/>
		/// </param>
		public PMatrix(Matrix matrix) {
			this.matrix = matrix.Clone();
		}
		#endregion

		#region Basic
		/// <summary>
		/// Gets or sets the underlying matrix object.
		/// </summary>
		/// <value>The underlying matrix object.</value>
		public virtual Matrix Matrix {
			get { return matrix.Clone(); }
			set { matrix = value.Clone(); }
		}

		/// <summary>
		/// Gets a reference to the underlying matrix object.
		/// </summary>
		/// <value>A Reference to the underlying matrix object.</value>
		public virtual Matrix MatrixReference {
			get { return matrix; }
		}

		/// <summary>
		/// Creates an exact copy of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <returns>The <see cref="PMatrix"/> object that his method creates.</returns>
		public virtual Object Clone() {
			PMatrix r = new PMatrix();
			r.Multiply(this);
			return r;
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
		public virtual float[] Elements {
			get { return matrix.Elements; }
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="PMatrix"/> object is the identity
		/// matrix.
		/// </summary>
		/// <value>This property is true if this Matrix is identity; otherwise, false.</value>
		public virtual bool IsIdentity {
			get { return matrix.IsIdentity; }
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="PMatrix"/> object is invertible.
		/// </summary>
		/// <value>
		/// This property is true if this <see cref="PMatrix"/> is invertible; otherwise, false.
		/// </value>
		public virtual bool IsInvertible {
			get { return matrix.IsInvertible; }
		}

		/// <summary>
		/// Multiplies this <see cref="PMatrix"/> object by the specified <see cref="PMatrix"/>
		/// object by prepending the specified Matrix.
		/// </summary>
		/// <param name="matrix">
		/// The <see cref="PMatrix"/> object by which this <see cref="PMatrix"/> object is to be
		/// multiplied.
		/// </param>
		public virtual void Multiply(PMatrix matrix) {
			this.matrix.Multiply(matrix.MatrixReference);
		}

		/// <summary>
		/// Inverts this <see cref="PMatrix"/> object, if it is invertible.
		/// </summary>
		public virtual void Invert() {
			matrix.Invert();
		}

		/// <summary>
		/// Resets this <see cref="PMatrix"/> object to have the elements of the identity matrix.
		/// </summary>
		public virtual void Reset() {
			matrix.Reset();
		}
		#endregion

		#region Transform Matrix
		/// <summary>
		/// Gets or sets the x translation value (the dx value, or the element in the third
		/// row and first column) of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <value>The x translation value of this <see cref="PMatrix"/>.</value>
		public virtual float OffsetX {
			get { return matrix.OffsetX; }
			set {
				matrix.Translate(value - matrix.OffsetX, 0, MatrixOrder.Append);
			}
		}

		/// <summary>
		/// Gets or sets the y translation value (the dy value, or the element in the third
		/// row and second column) of this <see cref="PMatrix"/> object.
		/// </summary>
		/// <value>The y translation value of this <see cref="PMatrix"/>.</value>
		public virtual float OffsetY {
			get { return matrix.OffsetY; }
			set	{
				matrix.Translate(0, value - matrix.OffsetY, MatrixOrder.Append);
			}
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
		public virtual void TranslateBy(float dx, float dy) {
			matrix.Translate(dx, dy);
		}

		/// <summary>
		/// Gets or sets the scale value of this <see cref="PMatrix"/>.
		/// </summary>
		/// <value>The scale value of this <see cref="PMatrix"/>.</value>
		public virtual float Scale {
			get	{
				PointF[] pts = {new PointF(0, 0), new PointF(1, 0)};
				matrix.TransformPoints(pts);
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
		public virtual void ScaleBy(float scale) {
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
		public virtual void ScaleBy(float scaleX, float scaleY) {
			matrix.Scale(scaleX, scaleY);
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
		public virtual void ScaleBy(float scale, float x, float y) {
			matrix.Translate(x, y);
			ScaleBy(scale);
			matrix.Translate(-x, -y);
		}

		/// <summary>
		/// Gets or sets the rotation value of this <see cref="PMatrix"/>, in degrees.
		/// </summary>
		/// <value>
		/// The rotation value of this <see cref="PMatrix"/>, in degrees.  The value will be
		/// between 0 and 360.
		/// </value>
		public virtual float Rotation {
			get	{
				PointF p1 = new PointF(0, 0);
				PointF p2 = new PointF(1, 0);
				PointF tp1 = this.Transform(p1);
				PointF tp2 = this.Transform(p2);

				double dy = Math.Abs(tp2.Y - tp1.Y);
				float l = PUtil.DistanceBetweenPoints(tp1, tp2);
				double rotation = Math.Asin(dy / l);

				// correct for quadrant
				if (tp2.Y - tp1.Y > 0) {
					if (tp2.X - tp1.X < 0) {
						rotation = Math.PI - rotation;
					}
				} else {
					if (tp2.X - tp1.X > 0) {
						rotation = 2 * Math.PI - rotation;
					} else {
						rotation = rotation + Math.PI;
					}
				}

				// convert to degrees
				return (float)(rotation * (180 / Math.PI));
			}
			set {
				RotateBy(value - Rotation);
			}
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
		public virtual void RotateBy(float theta) {
			matrix.Rotate(theta);
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
		public virtual void RotateBy(float theta, float x, float y) {
			matrix.RotateAt(theta, new PointF(x, y));
		}

		/// <summary>
		/// Applies the specified shear vector to this <see cref="PMatrix"/> object by prepending
		/// the shear transformation.
		/// </summary>
		/// <param name="shearX">The horizontal shear factor.</param>
		/// <param name="shearY">The vertical shear factor.</param>
		/// <remarks>
		/// The transformation applied in this method is a pure shear only if one of the parameters
		/// is 0. Applied to a rectangle at the origin, when the shearY factor is 0, the
		/// transformation moves the bottom edge horizontally by shearX times the height of the
		/// rectangle. When the shearX factor is 0, it moves the right edge vertically by shearY times
		/// the width of the rectangle. Caution is in order when both parameters are nonzero, because
		/// the results are hard to predict. For example, if both factors are 1, the transformation is
		/// singular (hence noninvertible), squeezing the entire plane to a single line.
		/// </remarks>
		public virtual void ShearBy(float shearX, float shearY) {
			matrix.Shear(shearX, shearY);
		}
		#endregion

		#region Transform Objects
		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given point.
		/// </summary>
		/// <param name="point">The point to transform.</param>
		/// <returns>The transformed point.</returns>
		public virtual PointF Transform(PointF point) {
			PointF[] pts = {point};
			TransformPoints(pts);
			return pts[0];
		}

		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given size.
		/// </summary>
		/// <param name="size">The size to be transformed.</param>
		/// <returns>The transformed size.</returns>
		public virtual SizeF Transform(SizeF size) {
			PointF[] pts = {new PointF(size.Width, size.Height)};
			TransformVectors(pts);
			return new SizeF(pts[0].X, pts[0].Y);
		}

		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to all
		/// of the points in the given array.
		/// <see cref="PMatrix"/>.
		/// </summary>
		/// <param name="pts">The array of points to transform.</param>
		public virtual void TransformPoints(PointF[] pts) {
			float[] elements = matrix.Elements;
			
			float x, y;
			int count = pts.Length;
			for (int i = 0; i < count; i++) {
				x = elements[0] * pts[i].X + elements[2] * pts[i].Y + elements[4];
				y = elements[1] * pts[i].X + elements[3] * pts[i].Y + elements[5];

				pts[i].X = x;
				pts[i].Y = y;
			}
		}

		/// <summary>
		/// Applies only the scale and rotate components of this <see cref="PMatrix"/> object to all
		/// of the points in the given array.
		/// </summary>
		/// <param name="pts">The array of points to transform.</param>
		public virtual void TransformVectors(PointF[] pts) {
			float[] elements = matrix.Elements;

			float x, y;
			int count = pts.Length;
			for (int i = 0; i < count; i++) {
				x = elements[0] * pts[i].X + elements[2] * pts[i].Y;
				y = elements[1] * pts[i].X + elements[3] * pts[i].Y;

				pts[i].X = x;
				pts[i].Y = y;
			}
		}

		/// <summary>
		/// Applies the geometric transform represented by this <see cref="PMatrix"/> object to the
		/// given rectangle.
		/// </summary>
		/// <param name="rect">The rectangle to transform.</param>
		/// <returns>The transformed rectangle.</returns>
		public virtual RectangleF Transform(RectangleF rect) {
			float x = rect.X;
			float y = rect.Y;
			float width = rect.Width;
			float height = rect.Height;

			PTS4[0].X = x;
			PTS4[0].Y = y;
			PTS4[1].X = x + width;
			PTS4[1].Y = y;
			PTS4[2].X = x + width;
			PTS4[2].Y = y + height;
			PTS4[3].X = x;
			PTS4[3].Y = y + height;

			TransformPoints(PTS4);

			float minX = PTS4[0].X;
			float minY = PTS4[0].Y;
			float maxX = PTS4[0].X;
			float maxY = PTS4[0].Y;

			for (int i = 1; i < 4; i++) {
				x = PTS4[i].X;
				y = PTS4[i].Y;

				if (x < minX) {
					minX = x;
				}
				if (y < minY) {
					minY = y;
				}
				if (x > maxX) {
					maxX = x;
				}
				if (y > maxY) {
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
		public virtual PointF InverseTransform(PointF point) {
			if (IsInvertible) {
				TEMP_MATRIX.Reset();
				TEMP_MATRIX.Multiply(this);
				TEMP_MATRIX.Invert();
				point = TEMP_MATRIX.Transform(point);

			}
			return point;
		}

		/// <summary>
		/// Applies the inverse of the geometric transform represented by this
		/// <see cref="PMatrix"/> object to the given size.
		/// </summary>
		/// <param name="size">The size to be transformed.</param>
		/// <returns>The transformed size.</returns>
		public virtual SizeF InverseTransform(SizeF size) {
			if (IsInvertible) {
				TEMP_MATRIX.Reset();
				TEMP_MATRIX.Multiply(this);
				TEMP_MATRIX.Invert();
				size = TEMP_MATRIX.Transform(size);
			}
			return size;
		}

		/// <summary>
		/// Applies the inverse of the geometric transform represented by this
		/// <see cref="PMatrix"/> object to the given rectangle.
		/// </summary>
		/// <param name="rect">The rectangle to transform.</param>
		/// <returns>The transformed rectangle.</returns>
		public virtual RectangleF InverseTransform(RectangleF rect) {
			if (IsInvertible) {
				TEMP_MATRIX.Reset();
				TEMP_MATRIX.Multiply(this);
				TEMP_MATRIX.Invert();
				rect = TEMP_MATRIX.Transform(rect);
			}
			return rect;
		}
		#endregion

		#region Serialization
		/// <summary>
		/// Read this this PMatrix from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">
		/// The StreamingContext of this serialization operation.
		/// </param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PMatrix(SerializationInfo info, StreamingContext context) {
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
		protected virtual String ElementString {
			get {
				StringBuilder result = new StringBuilder();

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
