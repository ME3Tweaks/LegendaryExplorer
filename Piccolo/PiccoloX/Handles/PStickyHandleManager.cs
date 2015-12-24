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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Handles {
	/// <summary>
	/// <b>PStickHandleManager</b> is used to add "sticky" handles to a node.
	/// </summary>
	/// <remarks>
	/// Sticky handles are handles that will not be affected by the view matrix of the
	/// camera they are stuck to.  This means, for example, they will not change in size
	/// as the camera is zoomed in and out.
	/// </remarks>
	public class PStickyHandleManager : PNode {
		#region Fields
		private PNode target;
		private PCamera camera;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PStickyHandleManager, adding sticky bounds handles (with
		/// respect to the given camera) to the specified target node.
		/// </summary>
		/// <param name="newCamera">The camera to stick the bounds handles to.</param>
		/// <param name="newTarget">
		/// The node that will be resized by the sticky bounds handles.
		/// </param>
		public PStickyHandleManager(PCamera newCamera, PNode newTarget) {
			SetCameraTarget(newCamera, newTarget);		
			PBoundsHandle.AddBoundsHandlesTo(this);
		}
		#endregion

		#region Camera and Target
		/// <summary>
		/// Sets the camera the bounds handles will be stuck to and the target node that
		/// will be resized by the sticky bounds handles.
		/// </summary>
		/// <param name="newCamera">The camera to stick the bounds handles to.</param>
		/// <param name="newTarget">
		/// The node that will be resized by the sticky bounds handles.
		/// </param>
		public virtual void SetCameraTarget(PCamera newCamera, PNode newTarget) {
			camera = newCamera;
			camera.AddChild(this);
			target = newTarget;
		}
		#endregion

		#region Bounds Management
		/// <summary>
		/// Overridden.  Sets the bounds of the target node.
		/// </summary>
		/// <param name="x">The new x coordinate of the bounds.</param>
		/// <param name="y">The new y coordinate of the bounds.</param>
		/// <param name="width">The new width of the bounds.</param>
		/// <param name="height">The new height of the bounds.</param>
		/// <returns>True if the bounds have changed; otherwise, false.</returns>
		/// <remarks>
		/// These bounds are stored in the local coordinate system of the target node.
		/// </remarks>
		public override bool SetBounds(float x, float y, float width, float height) {
			RectangleF b = new RectangleF(x, y, width, height);
			b = camera.LocalToGlobal(b);
			b = camera.LocalToView(b);
			b = target.GlobalToLocal(b);
			target.Bounds = b;
			return base.SetBounds (x, y, width, height);
		}

		/// <summary>
		/// Overridden.  Returns true since this nodes bounds are volatile (they may
		/// change at any time and should not be cached).
		/// </summary>
		protected override bool BoundsVolatile {
			get {
				return true;
			}
		}

		/// <summary>
		/// Overridden.  Sets the bounds of this node to the bounds of the target and
		/// returns those bounds.
		/// </summary>
		public override RectangleF Bounds {
			get {
				RectangleF targetBounds = target.FullBounds;
				targetBounds = camera.ViewToLocal(targetBounds);
				targetBounds = camera.GlobalToLocal(targetBounds);
				bounds = targetBounds;
				return base.Bounds;
			}
 		}

		/// <summary>
		/// Overridden.  Notifies the target node that <c>SetBounds</c> will be repeatedly
		/// called.
		/// </summary>
		public override void StartResizeBounds() {
			base.StartResizeBounds();
			target.StartResizeBounds();
		}

		/// <summary>
		/// Overridden.  Notifies the target node that the resize bounds sequence is
		/// finished.
		/// </summary>
		public override void EndResizeBounds() {
			base.EndResizeBounds ();
			target.EndResizeBounds();
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
	}
}