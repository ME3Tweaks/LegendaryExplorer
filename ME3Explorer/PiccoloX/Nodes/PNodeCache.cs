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
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// PNodeCache caches a visual representation of it's children 
	/// into an image and uses this cached image for painting instead of
	/// painting it's children directly.
	/// </summary>
	/// <remarks>
	/// This class is intended to be used in two ways.
	/// <P>
	/// First it can be used as a simple optimization technique. If a node 
	/// has many descendents it may be faster to paint the cached image 
	/// representation instead of painting each node.
	/// </P>
	/// <P>
	/// Second PNodeCache provides a place where "image" effects such as
	/// blurring and drop shadows can be added to the Piccolo scene graph.
	/// This can be done by overriding the method createImageCache and
	/// returing an image with the desired effect applied.
	/// </P>
	/// </remarks>
	[Serializable]
	public class PNodeCache : PNode {
		#region Fields
		private Image imageCache;
		private bool validatingCache;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PNodeCache.
		/// </summary>
		public PNodeCache() {
			// The default constructor is required here since this class has a
			// deserialization constructor.  Otherwise, a PNodeCache object could
			// not  be constructed with "new PNodeCache()".
		}
		#endregion

		#region Image Cache
		/// <summary>
		/// Override this method to customize the image cache creation process. For
		/// example if you want to create a shadow effect you would do that here. Fill
		/// in the cacheOffsetRef if needed to make your image cache line up with the
		/// nodes children.
		/// </summary>
		/// <param name="cacheOffsetRef">
		/// Set this value to apply an offset to the image cache.
		/// </param>
		/// <returns>The newly created image cache.</returns>
		public virtual Image CreateImageCache(ref SizeF cacheOffsetRef) {
			return ToImage();
		}

		/// <summary>
		/// Gets a cached image representation of this node's children.
		/// </summary>
		/// <remarks>A cached image representation of this node's children.</remarks>
		public virtual Image ImageCache {
			get {
				if (imageCache == null) {			
					SizeF cacheOffsetRef = SizeF.Empty;
					validatingCache = true;
					ResetBounds();
					imageCache = CreateImageCache(ref cacheOffsetRef);
					RectangleF b = FullBounds;
					SetBounds(b.X + cacheOffsetRef.Width,
						b.Y + cacheOffsetRef.Height,
						imageCache.Width, 
						imageCache.Height);
					validatingCache = false;
				}
				return imageCache;
			}
		}

		/// <summary>
		/// Discards the current image cache.
		/// </summary>
		public void InvalidateCache() {
			imageCache = null;
		}
		#endregion

		#region Paint Damage Management
		/// <summary>
		/// Overridden.  Invalidate this node's paint if the image cache is not currently
		/// being created.
		/// </summary>
		public override void InvalidatePaint() {
			if (!validatingCache) {
				base.InvalidatePaint();
			}
		}

		/// <summary>
		/// Overridden.  Pass the given repaint request up the tree if the image cache is
		/// not currently being created.
		/// </summary>
		/// <param name="bounds">
		/// The bounds to repaint, specified in the local coordinate system.
		/// </param>
		/// <param name="childOrThis">
		/// If childOrThis does not equal this then this node's matrix will be applied to
		/// the bounds paramater.
		/// </param>
		public override void RepaintFrom(RectangleF bounds, PNode childOrThis) {
			if (!validatingCache) {
				base.RepaintFrom (bounds, childOrThis);
				InvalidateCache();
			}
		}
		#endregion

		#region Painting
		/// <summary>
		/// Overridden.  Paints the cached image representation of this node's children if
		/// it is not currently being created.
		/// </summary>
		/// <param name="paintContext"></param>
		public override void FullPaint(PPaintContext paintContext) {
			if (validatingCache) {
				base.FullPaint (paintContext);
			} else {
				Graphics g = paintContext.Graphics;
				g.DrawImage(ImageCache, (int) X, (int) Y);
			}
		}
		#endregion

		#region Picking
		/// <summary>
		/// Overridden.  Return false since this node should never be picked.
		/// </summary>
		/// <param name="pickPath">The pick path used for the pick operation.</param>
		/// <returns>False since this node should never be picked.</returns>
		protected override bool PickAfterChildren(PPickPath pickPath) {
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
		/// Read this this PNodeCache and all its children from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">
		/// The StreamingContext of this serialization operation.
		/// </param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PNodeCache(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
		#endregion
	}
}