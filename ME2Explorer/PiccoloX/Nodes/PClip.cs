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
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// <b>PClip</b> is a simple node that applies a clip before rendering or picking its
	/// children.
	/// </summary>
	/// <remarks>
	/// PClip is a subclass of PPath.  The clip applied is the GraphicsPath wrapped by the
	/// base class.  See <c>UMD.HCIL.PiccoloExample.ClipExample</c>.
	/// </remarks>>
	[Serializable]
	public class PClip : PPath {
		#region Fields
		private static Region TEMP_REGION = new Region();
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PClip.
		/// </summary>
		public PClip() {
		}
		#endregion

		#region Full Bounds Geometry
		/// <summary>
		/// Overridden.  Returns the bounds of the clip in parent coordinates.
		/// </summary>
		/// <returns>
		/// The bounds of the clip in the parent coordinate system of this node.
		/// </returns>
		public override RectangleF ComputeFullBounds() {
			return LocalToParent(Bounds);
		}
		#endregion

		#region Paint Damage Management		
		/// <summary>
		/// Overridden.  If the repaint request comes from a child, then
		/// repaint the intersection of the clip's bounds and the requested
		/// repaint bounds.
		/// </summary>
		/// <param name="bounds">
		/// The bounds to repaint, specified in the local coordinate system.
		/// </param>
		/// <param name="childOrThis">
		/// If childOrThis does not equal this then this node's matrix will
		/// be applied to the bounds paramater.
		/// </param>
		public override void RepaintFrom(RectangleF bounds, PNode childOrThis) {
			if (childOrThis != this) {
				bounds = RectangleF.Intersect(Bounds, bounds);
				base.RepaintFrom(bounds, childOrThis);
			} else {
				base.RepaintFrom(bounds, childOrThis);
			}
		}
		#endregion

		#region Painting
		/// <summary>
		/// Overridden.  Renders the fill for this node and then pushes the clip onto the
		/// paint context, so that when this node's children are rendered they will be
		/// clipped accordingly.
		/// </summary>
		/// <param name="paintContext">
		/// The paint context to use for painting this node.
		/// </param>
		protected override void Paint(PPaintContext paintContext) {
			Brush b = Brush;
			if (b != null) {
				Graphics g = paintContext.Graphics;
				g.FillPath(b, this.PathReference);
			}
			TEMP_REGION.MakeInfinite();
			TEMP_REGION.Intersect(PathReference);
			paintContext.PushClip(TEMP_REGION);
		}

		/// <summary>
		/// Overridden.  Pops the clip from the paint context and then renders the outline
		/// of this node.
		/// </summary>
		/// <param name="paintContext">
		/// The paint context to use for painting this node.
		/// </param>
		protected override void PaintAfterChildren(PPaintContext paintContext) {
			paintContext.PopClip();
			if (Pen != null) {
				Graphics g = paintContext.Graphics;
				g.DrawPath(Pen, PathReference);
			}
		}
		#endregion

		#region Picking
		/// <summary>
		/// Overridden.  Only picks this node's children if the pick bounds intersects the
		/// clip.
		/// </summary>
		/// <param name="pickPath">The pick path to add the node to if its picked.</param>
		/// <returns>
		/// True if this node or one of its descendents was picked; else false.
		/// </returns>
		public override bool FullPick(PPickPath pickPath) {
			if (Pickable && FullIntersects(pickPath.PickBounds)) {
				pickPath.PushNode(this);
				pickPath.PushMatrix(MatrixReference);
			
				if (Pick(pickPath)) {
					return true;
				}

				if (ChildrenPickable && PUtil.PathIntersectsRect(PathReference, pickPath.PickBounds)) { 		
					int count = ChildrenCount;
					for (int i = count - 1; i >= 0; i--) {
						PNode each = this.GetChild(i);
						if (each.FullPick(pickPath))
							return true;
					}				
				}

				if (PickAfterChildren(pickPath)) {
					return true;
				}

				pickPath.PopMatrix(MatrixReference);
				pickPath.PopNode(this);
			}

			return false;
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
		/// Read this PClip and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">
		/// The StreamingContext of this serialization operation.
		/// </param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PClip(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
		#endregion
	}
}
