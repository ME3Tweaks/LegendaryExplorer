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
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using Piccolo.Nodes;

namespace Piccolo.Util {
	/// <summary>
	/// <b>PUtil</b> provides utility methods for the Piccolo framework.
	/// </summary>
	public class PUtil {
		#region Enums
		/// <summary>
		/// Bit fields representing the various orientations with which a point can lie outside
		/// of a rectangle.
		/// </summary>
		[Flags]
			public enum OutCode {
			/// <summary>
			/// The mouse is over the canvas.
			/// </summary>
			None = 0,

			/// <summary>
			/// The mouse is to the left of the canvas.
			/// </summary>
			Left = 1,

			/// <summary>
			/// The mouse is above the canvas.
			/// </summary>
			Top = 2,

			/// <summary>
			/// The mouse is to the right of the canvas.
			/// </summary>
			Right = 4,

			/// <summary>
			/// The mouse is below the canvas.
			/// </summary>
			Bottom = 8
		};
		#endregion

		#region Fields

		/// <summary>
		/// A node filter that only accepts cameras that are associated with canvases.
		/// </summary>
		public readonly static PNodeFilter CAMERA_WITH_CANVAS_FILTER = new CameraWithCanvasFilter();

		/// <summary>
		/// The default step rate for activities.
		/// </summary>
		public const long DEFAULT_ACTIVITY_STEP_RATE = 20;

		/// <summary>
		/// The default timer interval for the activity scheduler.
		/// </summary>
		public const int ACTIVITY_SCHEDULER_FRAME_INTERVAL = 10;

		/// <summary>
		/// The default minimum size for rendering all details of a node.
		/// </summary>
		public const float DEFAULT_GREEK_THRESHOLD = .5f;

		/// <summary>
		/// The default maximum size for rendering fonts.
		/// </summary>
		public const float DEFAULT_MAX_FONT_SIZE = 20000;

        private static Region TEMP_REGION = new();
		#endregion

		#region Filters
		/// <summary>
		/// A node filter that only accepts cameras that are associated with canvases.
		/// </summary>
		private class CameraWithCanvasFilter : PNodeFilter {
			public bool Accept(PNode aNode) {
				return (aNode is PCamera { Canvas: { } } camera);
			}
			public bool AcceptChildrenOf(PNode aNode) {
				return true;
			}
		}
		#endregion

		#region Scene Graph
		/// <summary>
		/// Creates a basic scene graph.
		/// </summary>
		/// <returns>The main camera node in the new scene graph.</returns>
		/// <remarks>
		/// The scene graph will consist of  root node with two children, a layer and a
		/// camera.  Additionally, The camera will be set to view the layer.  Typically,
		/// you will want to add new nodes to the layer.
		/// </remarks>
		public static PCamera CreateBasicScenegraph() {
			PRoot r = new PRoot();
			PLayer l = new PLayer();
			PCamera c = new PCamera();
		
			r.AddChild(c); 
			r.AddChild(l); 
			c.AddLayer(l);
		
			return c;
		}
		#endregion

		#region Geometric
		/// <summary>
		/// Returns the geometric distance between the two given points.
		/// </summary>
		/// <param name="p1">The first point.</param>
		/// <param name="p2">The second point.</param>
		/// <returns>The distance between p1 and p2.</returns>
		public static float DistanceBetweenPoints(PointF p1, PointF p2) {
			return MathF.Sqrt(MathF.Pow(p1.X - p2.X, 2) + MathF.Pow(p1.Y - p2.Y, 2));
		}

		/// <summary>
		/// Returns the center of the given rectangle.
		/// </summary>
		/// <param name="r">The rectangle whose center point is desired.</param>
		/// <returns>The center point of the given rectangle.</returns>
		public static PointF CenterOfRectangle(RectangleF r) {
			float centerX = r.X + (r.Width / 2);
			float centerY = r.Y + (r.Height / 2);
			return new PointF(centerX, centerY);
		}

		/// <summary>
		/// Return the delta required for the rectangle b1 to contain the rectangle b2.
		/// </summary>
		/// <param name="b1">The first rectangle.</param>
		/// <param name="b2">The second rectangle.</param>
		/// <returns>The delta required for b1 to contain b2.</returns>
		public static SizeF DeltaRequiredToContain(RectangleF b1, RectangleF b2) {
			SizeF result = SizeF.Empty;
		
			if (!b1.Contains(b2)) {
				float b1MaxX = Math.Max(b1.X, b1.Right);
				float b1MinX = Math.Min(b1.X, b1.Right);
				float b1MaxY = Math.Max(b1.Y, b1.Bottom);
				float b1MinY = Math.Min(b1.Y, b1.Bottom);

				float b2MaxX = Math.Max(b2.X, b2.Right);
				float b2MinX = Math.Min(b2.X, b2.Right);
				float b2MaxY = Math.Max(b2.Y, b2.Bottom);
				float b2MinY = Math.Min(b2.Y, b2.Bottom);

				if (!(b2MaxX > b1MaxX && b2MinX < b1MinX)) {
					if (b2MaxX > b1MaxX || b2MinX < b1MinX) {
						float difMaxX = b2MaxX - b1MaxX;
						float difMinX = b2MinX - b1MinX;
						if (Math.Abs(difMaxX) < Math.Abs(difMinX)) {
							result.Width = difMaxX;
						} else {
							result.Width = difMinX;
						}
					}				
				}

				if (!(b2MaxY > b1MaxY && b2MinY < b1MinY)) {
					if (b2MaxY > b1MaxY || b2MinY < b1MinY) {
						float difMaxY = b2MaxY - b1MaxY;
						float difMinY = b2MinY - b1MinY;
						if (Math.Abs(difMaxY) < Math.Abs(difMinY)) {
							result.Height = difMaxY;
						} else {
							result.Height = difMinY;
						}
					}
				}
			}
		
			return result;
		}

		/// <summary>
		/// Return the delta required to center the rectangle b2 in the rectangle b1.
		/// </summary>
		/// <param name="b1">The first rectangle.</param>
		/// <param name="b2">The second rectangle.</param>
		/// <returns>The delta required to center b2 in b1.</returns>
		public static SizeF DeltaRequiredToCenter(RectangleF b1, RectangleF b2) {
			SizeF result = SizeF.Empty;
			PointF sc = CenterOfRectangle(b1);
			PointF tc = CenterOfRectangle(b2);
			float xDelta = sc.X - tc.X;
			float yDelta = sc.Y - tc.Y;
			result.Width = xDelta;
			result.Height = yDelta;
			return result;
		}

		/// <summary>
		/// Returns the orientation with which the given point lies outside of the given
		/// rectangle.
		/// </summary>
		/// <param name="p">The point to compare against the rectangle.</param>
		/// <param name="r">The rectangle to compare against the point.</param>
		/// <returns></returns>
		public static OutCode RectangleOutCode(PointF p, RectangleF r) {
			OutCode outCode = OutCode.None;
			if (p.X < r.Left) {
				outCode |= OutCode.Left;
			}
			if (p.X > r.Right) {
				outCode |= OutCode.Right;
			}
			if (p.Y < r.Top) {
				outCode |= OutCode.Top;
			}
			if (p.Y > r.Bottom) {
				outCode |= OutCode.Bottom;
			}
			return outCode;
		}

		/// <summary>
		/// Gets a rectangle centered on the given point and inflated in the x and y directions
		/// by the given amounts.
		/// </summary>
		/// <param name="aCenterPoint">The center point for the inflated rectangle.</param>
		/// <param name="inflateX">
		/// The amount to inflate the rectangle in the x direction.
		/// </param>
		/// <param name="inflateY">
		/// The amount to inflate the rectangle in the y direction.
		/// </param>
		/// <returns>The inflated rectangle.</returns>
		public static RectangleF InflatedRectangle(PointF aCenterPoint, float inflateX, float inflateY) {
			RectangleF r = new RectangleF(aCenterPoint.X, aCenterPoint.Y, 0, 0);
			r.Inflate(inflateX, inflateY);
			return r;
		}

		/// <summary>
		/// Returns true if the interior of the given path intersects the given rectangle.
		/// </summary>
		/// <param name="path">The path to check for intersection.</param>
		/// <param name="rect">The rectangle to check for intersection.</param>
		/// <returns>
		/// True if the interior of the given path intersects the given rectangle; otherwise,
		/// false.
		/// </returns>
		public static bool PathIntersectsRect(GraphicsPath path, RectangleF rect) {
			TEMP_REGION.MakeInfinite();
			TEMP_REGION.Intersect(path);
			return TEMP_REGION.IsVisible(rect);
		}

		/// <summary>
		/// Return true if the given rectangle intersects the given line, which must have either
		/// a slope of 0 or 90 degrees.
		/// </summary>
		/// <param name="rect">The rectangle to compare for intersection.</param>
		/// <param name="x1">
		/// The x-coordinate of the first end point of the line to check for intersection.
		/// </param>
		/// <param name="y1">
		/// The y-coordinate of the first end point of the line to check for intersection.
		/// </param>
		/// <param name="x2">
		/// The x-coordinate of the second end point of the line to check for intersection.
		/// </param>
		/// <param name="y2">
		/// The y-coordinate of the second end point of the line to check for intersection.
		/// </param>
		/// <returns>True if the rectangle intersects the line; otherwise, false.</returns>
		/// <remarks>
		/// This is a quick method to check if a vertical or horizontal line intersects a
		/// rectangle.  If the line is not perpendicular or horizontal, the result will be
		/// unpredictable.
		/// </remarks>
		public static bool RectIntersectsPerpLine(RectangleF rect, float x1, float y1, float x2, float y2) {
			float minX = (x1 < x2) ? x1 : x2;
			float minY = (y1 < y2) ? y1 : y2;
			float maxX = (x1 > x2) ? x1 : x2;
			float maxY = (y1 > y2) ? y1 : y2;

			float lx = rect.X;
			float rx = rect.Right;
			float ty = rect.Y;
			float by = rect.Bottom;

			return (minX <= rx &&
				maxX >= lx &&
				minY <= by &&
				maxY >= ty);
		}

		/// <summary>
		/// Expands the given rectangle to include the given point.
		/// </summary>
		/// <param name="rect">The rectangle to expand.</param>
		/// <param name="p">The point to include.</param>
		/// <returns>The expanded rectangle.</returns>
		public static RectangleF AddPointToRect(RectangleF rect, PointF p) {
			float px = p.X;
			float py = p.Y;
			float rx = rect.X;
			float ry = rect.Y;
			float rw = rect.Width;
			float rh = rect.Height;

			if (rect == Rectangle.Empty) {
				rect.X = px;
				rect.Y = py;
				rect.Width = 0;
				rect.Height = 0;
			} else {
				float x1 = (rx <= px) ? rx : px;
				float y1 = (ry <= py) ? ry : py;
				float x2 = ((rx + rw) >= px) ? (rx + rw) : px;			
				float y2 = ((ry + rh) >= py) ? (ry + rh) : py;
				rect.X = x1;
				rect.Y = y1;
				rect.Width = x2 - x1;
				rect.Height = y2 - y1;
			}

			return rect;
		}

		/// <summary>
		/// Returns the union of the two rectangles.  If one rectangle is empty and one is not,
		/// the non-empty rectangle will be returned.
		/// </summary>
		/// <param name="rect1">The first rectangle.</param>
		/// <param name="rect2">The second rectangle.</param>
		/// <returns>The union of the two rectangles.</returns>
		public static RectangleF AddRectToRect(RectangleF rect1, RectangleF rect2) {
			RectangleF result = rect1;
		
			if (!result.IsEmpty) {
				if (!rect2.IsEmpty) {
					result = RectangleF.Union(result, rect2);
				}
			} 
			else {
				result = rect2;
			}

			return result;
		}
		#endregion

		#region Time
		/// <summary>
		/// Gets the current time in milliseconds.
		/// </summary>
		/// <value>The current time in milliseconds.</value>
		public static long CurrentTimeMillis => Environment.TickCount;

        #endregion

		#region Drawing
		/// <summary>
		/// Darkens the given color by an amount specified by the scaleFactor.
		/// </summary>
		/// <param name="color">The color to darken.</param>
		/// <param name="scaleFactor">
		/// The factor used to determine how much to darken the color.
		/// </param>
		/// <returns>The new darker color.</returns>
		public static Color Darker(Color color, float scaleFactor) {
			return Color.FromArgb(
				color.A,
				(int)(Math.Max(0, color.R - scaleFactor * color.R)),
				(int)(Math.Max(0, color.G - scaleFactor * color.G)),
				(int)(Math.Max(0, color.B - scaleFactor * color.B)));
		}

		/// <summary>
		/// Lightens the given color by an amount specified by the scaleFactor.
		/// </summary>
		/// <param name="color">The color to lighten.</param>
		/// <param name="scaleFactor">
		/// The factor used to determine how much to lighten the color.
		/// </param>
		/// <returns>The new lighter color.</returns>
		public static Color Brighter(Color color, float scaleFactor) {
			return Color.FromArgb(
				color.A,
				(int)(Math.Min(255, color.R + scaleFactor * color.R)),
				(int)(Math.Min(255, color.G + scaleFactor * color.G)),
				(int)(Math.Min(255, color.B + scaleFactor * color.B)));
		}

		/// <summary>
		/// Gets or sets a system wide value that specifies the smallest size at which nodes
		/// should render themselves completely.
		/// </summary>
		/// <value>
		/// The smallest size at which nodes should render themselves completely.
		/// </value>
		/// <remarks>
		/// Once a node becomes very small, it is no longer necessary to render it completely
		/// since it is impossible to see the details anyway.  When this happens, you might
		/// choose to simply render a filled rectangle or nothing at all.  This property
		/// specifies when nodes should be "greeked".  Note, however, there is nothing in the
		/// piccolo framework that enforces the "greeking" of nodes.  Nodes can choose to ignore
		/// this property.  For an example of "greeking," see
		/// <see cref="PText">PText</see>.
		/// </remarks>
		public static float GreekThreshold { get; set; } = DEFAULT_GREEK_THRESHOLD;

        /// <summary>
		/// Gets or sets a system wide value that specifies the maximum size at which text
		/// should be rendered.
		/// </summary>
		/// <value>The maximum size at which text should be rendered.</value>
		/// <remarks>
		/// This value specifies the largest font size at which text should be rendered.  Nodes
		/// that have text should pay attention to this value.  Note, however, there is nothing
		/// in the piccolo framework that enforces nodes to stop rendering text at this size.
		/// For an example of a node that uses this property, see
		/// <see cref="PText">PText</see>.
		/// </remarks>
		public static float MaxFontSize { get; set; } = DEFAULT_MAX_FONT_SIZE;

        #endregion
	}
}
