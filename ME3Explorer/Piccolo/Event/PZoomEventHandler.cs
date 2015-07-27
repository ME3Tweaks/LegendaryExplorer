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
using System.Windows.Forms;
using System.Drawing;
using System.Text;

namespace UMD.HCIL.Piccolo.Event {
	/// <summary>
	/// <b>ZoomEventhandler</b> provides event handlers for basic zooming
	/// of the canvas view with the right (third) button.
	/// </summary>
	/// <remarks>
	/// Tthe initial mouse press defines the zoom anchor point, and then
	/// moving the mouse to the right zooms with a speed proportional
	/// to the amount the mouse is moved to the right of the anchor point.
	/// Similarly, if the mouse is moved to the left, the the view is
	/// zoomed out.
	/// </remarks>
	public class PZoomEventHandler : PDragSequenceEventHandler {
		#region Fields
		private float minScale = .0001f;
		private float maxScale = 100000; //float.MaxValue;
		private PointF viewZoomPoint;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PZoomEventHandler.
		/// </summary>
		public PZoomEventHandler() {
			this.AcceptsEvent = new AcceptsEventDelegate(PZoomEventHandlerAcceptsEvent);
		}
		#endregion

		#region Zooming
		/// <summary>
		/// The filter for a PZoomEventHandler.  This method only accepts right mouse button
		/// events that have not yet been handled.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the event is an unhandled right mouse button event; otherwise, false.
		/// </returns>		
		protected virtual bool PZoomEventHandlerAcceptsEvent(PInputEventArgs e) {
			if (!e.Handled && e.IsMouseEvent && e.Button == MouseButtons.Right) {
				return true;
			}
			return false;
		}

		/// <summary>
		/// Gets or sets the minimum view magnification factor that this event handler is
		/// bound by.
		/// </summary>
		/// <value>The minimum view magnification factor.</value>
		/// <remarks>
		/// When this property is set the camera is left at its current scale evem if the
		/// value is larger than the current scale.
		/// <para>
		/// The value must be greater than 0.
		/// </para>
		/// </remarks>
		public virtual float MinScale {
			get { return minScale; }
			set {minScale = value; }
		}

		/// <summary>
		/// Gets or sets the maximum view magnification factor that this event handler is
		/// bound by.
		/// </summary>
		/// <value>The maximum view magnification factor.</value>
		/// <remarks>
		/// When this property is set the camera is left at its current scale even if
		/// the value is smaller than the current scale.
		/// <para>
		/// The value must be greater than 0.
		/// </para>
		/// </remarks>
		public virtual float MaxScale {
			get { return maxScale; }
			set {maxScale = value; }
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDragActivityFirstStep">
		/// PDragSequenceEventHandler.OnDragActivityFirstStep</see>.
		/// </summary>
		protected override void OnDragActivityFirstStep(object sender, PInputEventArgs e) {
			viewZoomPoint = e.Position;
			base.OnDragActivityFirstStep(sender, e);
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDragActivityStep">
		/// PDragSequenceEventHandler.OnDragActivityStep</see>.
		/// </summary>
		protected override void OnDragActivityStep(object sender, PInputEventArgs e) {
			base.OnDragActivityStep(sender, e);

			PCamera camera = e.Camera;
			float dx = e.CanvasPosition.X - MousePressedCanvasPoint.X;
			float scaleDelta = (1.0f + (0.001f * dx));		

			float currentScale = camera.ViewScale;
			float newScale = currentScale * scaleDelta;

			if (newScale < minScale) {
				scaleDelta = minScale / currentScale;
			}

			if ((maxScale > 0) && (newScale > maxScale)) {
				scaleDelta = maxScale / currentScale;
			}

			camera.ScaleViewBy(scaleDelta, viewZoomPoint.X, viewZoomPoint.Y);
		}
		#endregion
		
		#region Debugging
		/// <summary>
		/// Returns a string representing the state of this object.
		/// </summary>
		/// <value>A string representing the state of this object.</value>
		/// <remarks>
		/// This method is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override String ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("minScale=" + minScale);
				result.Append(",maxScale=" + maxScale);
				result.Append(",viewZoomPoint=" + viewZoomPoint.ToString());
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}
