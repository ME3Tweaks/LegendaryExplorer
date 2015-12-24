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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.Piccolo.Event {
	/// <summary>
	/// <b>PPanEventHandler</b> provides event handlers for basic panning of the canvas
	/// view with the left mouse button.
	/// </summary>
	/// <remarks>
	/// Clicking and dragging the mouse translates the view so that the point on the
	/// surface stays under the mouse.
	/// </remarks>
	public class PPanEventHandler : PDragSequenceEventHandler {
		#region Fields
		private bool autopan;
		private float minAutopanSpeed = 250f; // Pixels per second
		private float maxAutopanSpeed = 750f; // Pixels per second
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPanEventHandler.
		/// </summary>
		public PPanEventHandler() {
			this.AcceptsEvent = new AcceptsEventDelegate(PPanEventHandlerAcceptsEvent);
			Autopan = true;
		}
		#endregion

		#region Pan
		/// <summary>
		/// The filter for a PPanEventHandler.  This method only accepts left mouse button
		/// events that have not yet been handled.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the event is an unhandled left mouse button event; otherwise, false.
		/// </returns>
		protected virtual bool PPanEventHandlerAcceptsEvent(PInputEventArgs e) {
			if (!e.Handled && e.IsMouseEvent && e.Button == MouseButtons.Left) {
				return true;
			}
			return false;
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDrag">
		/// PDragSequenceEventHandler.OnDrag</see>.
		/// </summary>
		protected override void OnDrag(object sender, PInputEventArgs e) {
			base.OnDrag (sender, e);
			Pan(e);
		}

		/// <summary>
		/// Pans the camera as the mouse is dragged.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void Pan(PInputEventArgs e) {
			PCamera c = e.Camera;
			PointF l = e.Position;
		
			if (c.ViewBounds.Contains(l)) {
				SizeF s = e.Delta;
				c.TranslateViewBy(s.Width, s.Height);
			}
		}
		#endregion

		#region Auto Pan
		//****************************************************************
		// Auto Pan - Methods for autopanning the canvas view.
		//****************************************************************

		/// <summary>
		/// Gets or sets a value indicating if the autopan feature is turned on.
		/// </summary>
		/// <value>True if autopan is turned on; otherwise false.</value>
		public virtual bool Autopan {
			get { return autopan; }
			set { autopan = value; }
		}

		/// <summary>
		/// Gets or sets the minimum speed measured in pixels per second at which
		/// auto-panning occurs.
		/// </summary>
		public virtual float MinAutopanSpeed {
			get { return minAutopanSpeed; }
			set { minAutopanSpeed = value; }
		}

		/// <summary>
		/// Gets or sets the maximum speed measured in pixels per second at which
		/// auto-panning occurs.
		/// </summary>
		public virtual float MaxAutopanSpeed {
			get { return maxAutopanSpeed; }
			set { maxAutopanSpeed = value; }
		}

		/// <summary>
		/// Overridden.  Do auto-panning even when the mouse is not moving.
		/// </summary>
		/// <param name="sender">The source of the drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected override void OnDragActivityStep(object sender, PInputEventArgs e) {
			base.OnDragActivityStep(sender, e);

			if (!autopan) return;
		
			PCamera c = e.Camera;
			RectangleF b = c.Bounds;
			PointF l = e.GetPositionRelativeTo(c);
			PUtil.OutCode outcode = PUtil.RectangleOutCode(l, b);
			SizeF delta = SizeF.Empty;
		
			if ((outcode & PUtil.OutCode.Top) != 0) {
				delta.Height = ValidatePanningDelta(-1.0f - (0.5f * Math.Abs(l.Y - b.Y)));
			} else if ((outcode & PUtil.OutCode.Bottom) != 0) {
				delta.Height = ValidatePanningDelta(1.0f + (0.5f * Math.Abs(l.Y - (b.Y + b.Height))));
			}
		
			if ((outcode & PUtil.OutCode.Right) != 0) {
				delta.Width = ValidatePanningDelta(1.0f + (0.5f * Math.Abs(l.X - (b.X + b.Width))));
			} else if ((outcode & PUtil.OutCode.Left) != 0) {
				delta.Width = ValidatePanningDelta(-1.0f - (0.5f * Math.Abs(l.X - b.X)));
			}
		
			delta = c.LocalToView(delta);
		
			if (delta.Width != 0 || delta.Height != 0) {
				c.TranslateViewBy(delta.Width, delta.Height);
			}
		}

		/// <summary>
		/// Enforces the min and max auto-panning speeds, adjusting the given delta if necessary.
		/// </summary>
		/// <param name="delta">The distance moved out of the canvas on the last drag event/</param>
		/// <returns>The validated pan delta.</returns>
		protected virtual float ValidatePanningDelta(float delta) {
			// pixels per second / number of steps per second
			float minDelta = minAutopanSpeed / (1000f / DragActivityStepInterval);
			float maxDelta = maxAutopanSpeed / (1000f / DragActivityStepInterval);

			bool deltaNegative = delta < 0;
			delta = Math.Abs(delta);
			if (delta < minDelta) delta = minDelta;
			if (delta > maxDelta) delta = maxDelta;
			if (deltaNegative) delta = -delta;
			return delta;
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

				result.Append("minAutopanSpeed=" + minAutopanSpeed);
				result.Append(",maxAutopanSpeed=" + maxAutopanSpeed);
				if (autopan) result.Append(",autopan");
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}
