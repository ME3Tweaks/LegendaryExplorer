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
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// This is a simple node that draws a "3D" rectangle within it's bounds.
	/// </summary>
	/// <remarks>
	/// Drawing a 3D rectangle in a zooming environment is a little tricky because you
	/// generally do not want the 3D borders to get scaled.  This version always draws
	/// the 3D border at a fixed 2 pixel width.
	/// </remarks>
	[Serializable]
	public class P3DRectangle : PNode, ISerializable {
		#region Fields
		private Color topLeftOuterColor;
		private Color topLeftInnerColor;
		private Color bottomRightInnerColor;
		private Color bottomRightOuterColor;
		private GraphicsPath path;
		[NonSerialized] private Pen pen;
		private bool raised;
		private const float BRIGHTNESS_SCALE_FACTOR = .4f;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new P3DRectangle with empty bounds.
		/// </summary>
		public P3DRectangle() {
			raised = true;
			pen = new Pen(Brushes.Black, 0);
			path = new GraphicsPath();
		}

		/// <summary>
		/// Constructs a new P3DRectangle with the given bounds.
		/// </summary>
		/// <param name="bounds">The bounds of the new P3DRectangle</param>
		public P3DRectangle(RectangleF bounds) :
			this(bounds.X, bounds.Y, bounds.Width, bounds.Height) {
		}
	
		/// <summary>
		/// Constructs a new P3DRectangle with the given x, y, width and height values.
		/// </summary>
		/// <param name="x">The x coordinate of the new P3DRectangle.</param>
		/// <param name="y">The y coordinate of the new P3DRectangle.</param>
		/// <param name="width">The width of the new P3DRectangle.</param>
		/// <param name="height">The width of the new P3DRectangle.</param>
		public P3DRectangle(float x, float y, float width, float height) :
			this() {
			SetBounds(x, y, width, height);
		}
		#endregion

		#region Painting
		/// <summary>
		/// Gets or sets a value that indicates whether the 3D rectangle is rendered so that
		/// it appears raised or depressed.
		/// </summary>
		/// <value>True if the rectangle is raised; otherwise, false.</value>
		public virtual bool Raised {
			get {
				return raised;
			}
			set {
				raised = value;
				Brush = Brush;
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.Brush">PNode.Brush</see>.
		/// </summary>
		public override Brush Brush {
			set {
				base.Brush = value;

				if (value is SolidBrush) {
					Color color = ((SolidBrush)value).Color;
			
					if (raised) {
						topLeftOuterColor = PUtil.Brighter(color, BRIGHTNESS_SCALE_FACTOR);
						topLeftInnerColor = PUtil.Brighter(topLeftOuterColor, BRIGHTNESS_SCALE_FACTOR);
						bottomRightInnerColor = PUtil.Darker(color, BRIGHTNESS_SCALE_FACTOR);
						bottomRightOuterColor = PUtil.Darker(bottomRightInnerColor, BRIGHTNESS_SCALE_FACTOR);
					} else {
						topLeftOuterColor = PUtil.Darker(color, BRIGHTNESS_SCALE_FACTOR);
						topLeftInnerColor = PUtil.Darker(topLeftOuterColor, BRIGHTNESS_SCALE_FACTOR);
						bottomRightInnerColor = PUtil.Brighter(color, BRIGHTNESS_SCALE_FACTOR);
						bottomRightOuterColor = PUtil.Brighter(bottomRightInnerColor, BRIGHTNESS_SCALE_FACTOR);			
					}
				} else {
					topLeftOuterColor = Color.Empty;
					topLeftInnerColor = Color.Empty;
					bottomRightInnerColor = Color.Empty;
					bottomRightOuterColor = Color.Empty;
				}
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.Paint">PNode.Paint</see>.
		/// </summary>
		protected override void Paint(PPaintContext paintContext) {
			Graphics g = paintContext.Graphics;

			float x = X;
			float y = Y;
			float width = Width;
			float height = Height;
			float[] elements = g.Transform.Elements;
			float magX = elements[0];
			float magY = elements[3];
			float dx = (float)(1.0 / magX);
			float dy = (float)(1.0 / magY);

			g.FillRectangle(Brush, Bounds);

			path.Reset();
			path.AddLine((float)(x+width), (float)y, (float)x, (float)y);
			path.AddLine((float)x, (float)y, (float)x, (float)(y+height));
			pen.Color = topLeftOuterColor;
			g.DrawPath(pen, path);

			path.Reset();
			path.AddLine((float)(x+width), (float)(y+dy), (float)(x+dx), (float)(y+dy));
			path.AddLine((float)(x+dx), (float)(y+dy), (float)(x+dx), (float)(y+height));
			pen.Color = topLeftInnerColor;
			g.DrawPath(pen, path);

			path.Reset();
			path.AddLine((float)(x+width), (float)(y), (float)(x+width), (float)(y+height));
			path.AddLine((float)(x+width), (float)(y+height), (float)(x), (float)(y+height));
			pen.Color = bottomRightOuterColor;
			g.DrawPath(pen, path);

			path.Reset();
			path.AddLine((float)(x+width-dx), (float)(y+dy), (float)(x+width-dx), (float)(y+height-dy));
			path.AddLine((float)(x+width-dx), (float)(y+height-dy), (float)(x), (float)(y+height-dy));
			pen.Color = bottomRightInnerColor;
			g.DrawPath(pen, path);
		}
		#endregion

		#region Serialization
		//****************************************************************
		// Serialization - Nodes conditionally serialize their parent.
		// This means that only the parents that were unconditionally
		// (using GetObjectData) serialized by someone else will be restored
		// when the node is deserialized.
		//****************************************************************

		/// <summary>
		/// Read this this P3DRectangle and all its children from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">
		/// The StreamingContext of this serialization operation.
		/// </param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected P3DRectangle(SerializationInfo info, StreamingContext context)
			: base(info, context) {

			pen = PUtil.ReadPen(info);
		}

		/// <summary>
		/// Write this P3DRectangle and all of its descendent nodes to the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to write to.</param>
		/// <param name="context">The streaming context of this serialization operation.</param>
		/// <remarks>
		/// This node's parent is written out conditionally, that is it will only be written out
		/// if someone else writes it out unconditionally.
		/// </remarks>
		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData (info, context);

			PUtil.WritePen(pen, info);
		}
		#endregion
	}
}