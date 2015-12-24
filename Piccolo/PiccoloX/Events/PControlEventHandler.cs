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
using System.Windows.Forms;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Activities;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX.Nodes;

namespace UMD.HCIL.PiccoloX.Events {
	/// <summary>
	/// <b>PControlEventHandler</b> implements simple focus based navigation for
	/// <see cref="PControl"/> nodes.
	/// </summary>
	/// <remarks>
	/// When a mouse button is pressed over a <see cref="PControl"/> node, the
	/// view is zoomed so that the node is at the natural size of the control
	/// (100 percent scale).  The view is also panned, if necessary, to keep the
	/// node on the screen.  The <see cref="PControl"/> node is then switched to
	/// editing mode.
	/// <para>
	/// A control can only have one parent.  But, Piccolo nodes can be viewed by
	/// multiple cameras, which in turn may be displayed on multiple canvases.
	/// For simplicity, this event handler only allows one <see cref="PControl"/>
	/// node to be editable at a time.  So, whenever a new PControl gets the focus,
	/// the previously focused node is made non-editable.
	/// </para>
	/// </remarks>
	public class PControlEventHandler : PBasicInputEventHandler {
		#region Fields
		private PActivity navigationActivity;
		private PControl lastEditedControl;
		#endregion

		#region Focus Change Events
		/// <summary>
		/// Overridden.  Moves the focus to the <see cref="PControl"/> node under
		/// the cursor.
		/// </summary>
		/// <param name="sender">The source of the PInputEvent.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public override void OnMouseDown(object sender, PInputEventArgs e) {
			base.OnMouseDown (sender, e);

			if (lastEditedControl != null) lastEditedControl.Editing = false;

			PNode node = e.PickedNode;
			if (node is PControl) {
				e.Handled = true;
				DirectCameraViewToControl(e.Camera, (PControl)node, e.Path, 300);
				lastEditedControl = (PControl)node;
			}
		}
		#endregion

		#region Canvas Movement
		/// <summary>
		/// Animates the camera's view to keep the control node on the screen and at 100
		/// percent scale with minimal view movement.
		/// </summary>
		/// <param name="aCamera">The camera whose view will be animated.</param>
		/// <param name="aControlNode">The control node to animate to.</param>
		/// <param name="path">The pick path through which the control node was picked.</param>
		/// <param name="duration">The length of the animation.</param>
		/// <returns>
		/// The activity that animates the camera's view to the control node.
		/// </returns>
		public virtual PActivity DirectCameraViewToControl(PCamera aCamera, PControl aControlNode, PPickPath path, int duration) {
			PMatrix originalViewMatrix = aCamera.ViewMatrix;

			// Scale the canvas to include
			SizeF s = new SizeF(1, 0);
			s = aControlNode.GlobalToLocal(s);
		
			float scaleFactor = s.Width / aCamera.ViewScale;
			PointF scalePoint = PUtil.CenterOfRectangle(aControlNode.GlobalFullBounds);
			if (scaleFactor != 1) {
				aCamera.ScaleViewBy(scaleFactor, scalePoint.X, scalePoint.Y);
			}
		
			// Pan the canvas to include the view bounds with minimal canvas
			// movement.
			aCamera.AnimateViewToPanToBounds(aControlNode.GlobalFullBounds, 0);

			// Get rid of any white space. The canvas may be panned and
			// zoomed in to do this. But make sure not stay constrained by max
			// magnification.
			//FillViewWhiteSpace(aCamera);

			PMatrix resultingMatrix = aCamera.ViewMatrix;
			aCamera.ViewMatrix = originalViewMatrix;

			PControl controlNode = (PControl)aControlNode;

			// Animate the canvas so that it ends up with the given
			// view transform.
			PActivity animateCameraViewActivity = AnimateCameraViewMatrixTo(aCamera, resultingMatrix, duration);
			aCamera.Root.WaitForActivities();

			PointF pf = path.GetPathTransformTo(controlNode).Transform(new PointF(controlNode.X, controlNode.Y));
			controlNode.ControlLocation = new Point((int)pf.X, (int)pf.Y);
			controlNode.CurrentCanvas = path.TopCamera.Canvas;
			controlNode.Editing = true;

			return animateCameraViewActivity;
		}

		/// <summary>
		/// Animate the camera's view matrix from its current value when the activity
		/// starts to the new destination matrix value.
		/// </summary>
		/// <param name="aCamera">The camera whose view matrix will be animated.</param>
		/// <param name="aMatrix">The final matrix value.</param>
		/// <param name="duration">
		/// The amount of time that the animation should take.
		/// </param>
		/// <returns>
		/// The newly scheduled activity, if the duration is greater than 0; else null.
		/// </returns>
		protected virtual PActivity AnimateCameraViewMatrixTo(PCamera aCamera, PMatrix aMatrix, int duration) {
			bool wasOldAnimation = false;
		
			// first stop any old animations.
			if (navigationActivity != null) {
				navigationActivity.Terminate();
				wasOldAnimation = true;
			}
			
			if (duration == 0) {
				aCamera.ViewMatrix = aMatrix;
				return null;
			}

			PMatrix source = aCamera.ViewMatrixReference;

			if (!source.MatrixReference.Equals(aMatrix.MatrixReference)) {
				navigationActivity = aCamera.AnimateViewToMatrix(aMatrix, duration);
				((PTransformActivity)navigationActivity).SlowInSlowOut = !wasOldAnimation;
				return navigationActivity;			
			}
		
			return null;
		}
		#endregion
	}
}
