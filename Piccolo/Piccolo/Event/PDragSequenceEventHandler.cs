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

using UMD.HCIL.Piccolo.Activities;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.Piccolo.Event {
	#region Delegates
	/// <summary>
	/// A delegate used to notify classes of the start of a new drag sequence.
	/// </summary>
	public delegate void StartDragDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate used to notify classes of drag events in a drag sequence.
	/// </summary>
	public delegate void DragDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate used to notify classes of the end of a drag sequence.
	/// </summary>
	public delegate void EndDragDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate used to notify classes that the drag activity has started stepping.
	/// </summary>
	public delegate void DragActivityFirstStepDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate used to notify classes that the drag activity is stepping.
	/// </summary>
	public delegate void DragActivityStepDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate used to notify classes that the drag activity has stopped stepping.
	/// </summary>
	public delegate void DragActivityFinalStepDelegate(object sender, PInputEventArgs e);
	#endregion

	/// <summary>
	/// <b>PDragSequenceEventHandler</b> is designed to support mouse pressed, dragged, and
	/// released interaction sequences. Support is also provided for running a continuous
	/// activity during the drag sequence. 
	/// </summary>
	/// <remarks>
	/// PDragSequenceEventHandler should be subclassed by a concrete event handler
	/// that implements a particular interaction. See PPanEventHandler, PZoomEventHandler,
	/// and PDragEventHandler for examples
	/// </remarks>
	public abstract class PDragSequenceEventHandler : PBasicInputEventHandler, PActivity.PActivityDelegate {
		#region Fields
		private float minDragStartDistance = 0;
		private bool dragging = false;
		private PointF mousePressedCanvasPoint = PointF.Empty;
		private PActivity dragActivity;
		private PInputEventArgs dragEvent;
		private object source;
		private MouseButtons sequenceInitiatedButton = System.Windows.Forms.MouseButtons.None;

		/// <summary>
		/// Used to notify classes of the start of a new drag sequence.
		/// </summary>
		public StartDragDelegate StartDrag;

		/// <summary>
		/// Used to notify classes of drag events in a drag sequence.
		/// </summary>
		public DragDelegate Drag;

		/// <summary>
		/// Used to notify classes of the end of a drag sequence.
		/// </summary>
		public EndDragDelegate EndDrag;

		/// <summary>
		/// Used to notify classes that the drag activity has started stepping.
		/// </summary>
		public DragActivityFirstStepDelegate DragActivityFirstStep;

		/// <summary>
		/// Used to notify classes that the drag activity is stepping.
		/// </summary>
		public DragActivityStepDelegate DragActivityStep;

		/// <summary>
		/// Used to notify classes that the drag activity has stopped stepping.
		/// </summary>
		public DragActivityFinalStepDelegate DragActivityFinalStep;
		#endregion

		#region Constructors
		/// <summary>
		///  Constructs a new PDragSequenceEventHandler.
		/// </summary>
		public PDragSequenceEventHandler() {
		}
		#endregion

		#region Basics
		//****************************************************************
		// Basics - Methods for accessing basic information about the drag
		// sequence.
		//****************************************************************

		/// <summary>
		/// Gets or sets a value indicating whether the a drag sequence is in progress.
		/// </summary>
		/// <value>True if a drag sequence is in progress; otherwise, false.</value>
		public virtual bool Dragging {
			get { return dragging; }
			set { dragging = value; }
		}

		/// <summary>
		/// Gets or sets the minimum distance (in screen coordinates) the mouse must
		/// move before a drag sequence is initiated.
		/// </summary>
		/// <value>The minimum distance required to initate a drag sequence.</value>
		public virtual float MinDragStartDistance {
			get { return minDragStartDistance; }
			set { minDragStartDistance = value; }
		}

		/// <summary>
		/// Gets or sets the point in canvas coordinates where the mouse was last pressed.
		/// </summary>
		public virtual PointF MousePressedCanvasPoint {
			get { return mousePressedCanvasPoint; }
			set { mousePressedCanvasPoint = value; }
		}

		/// <summary>
		/// Gets the minimum number of milliseconds that the drag activity associated with
		/// this listener should delay between steps.
		/// </summary>
		/// <value>
		/// The minimum number of milliseconds that the drag activity should delay between
		/// steps.
		/// </value>
		public virtual long DragActivityStepInterval {
			get { return dragActivity.StepInterval; }
		}
		#endregion

		#region Dragging
		//****************************************************************
		// Dragging - Methods to indicate the stages of the drag sequence.
		//****************************************************************

		/// <summary>
		/// Subclasses should override this method to get notified of the start of a new
		/// drag sequence.
		/// </summary>
		/// <param name="sender">The source of the start drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// This method is called at the beginning of a drag sequence.
		/// <para>
		/// Unlike the <see cref="OnMouseDrag"/> method, this method will not get called
		/// until after the <see cref="MinDragStartDistance"/> has been reached.
		/// </para>
		/// <para>
		/// <b>Notes to Inheritors:</b>  Overriding methods must still call
		/// <c>base.OnStartDrag()</c> for correct behavior.
		/// </para>
		/// </remarks>
		protected virtual void OnStartDrag(object sender, PInputEventArgs e) {
			dragEvent = e;
			source = sender;
			StartDragActivity(e);
			Dragging = true;
			e.Canvas.Interacting = true;

			if (StartDrag != null) {
				StartDrag(sender, e);
			}
		}

		/// <summary>
		/// Subclasses should override this method to get notified of the drag events in
		/// a drag sequence. 
		/// </summary>
		/// <param name="sender">The source of the end drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// This method is called in the middle of a drag sequence, between the
		/// <see cref="OnStartDrag"/> and the <see cref="OnEndDrag"/> methods.
		/// <para>
		/// Unlike the <see cref="OnMouseDrag"/> method, this method will not get called
		/// until after the <see cref="MinDragStartDistance"/> has been reached.
		/// </para>
		/// <para>
		/// <b>Notes to Inheritors:</b>  Overriding methods must still call
		/// <c>base.OnDrag()</c> for correct behavior.
		/// </para>
		/// </remarks>
		protected virtual void OnDrag(object sender, PInputEventArgs e) {
			dragEvent = e;
			if (Drag != null) Drag(sender, e);
		}

		/// <summary>
		/// Subclasses should override this method to get notified of the end event in
		/// a drag sequence. 
		/// </summary>
		/// <param name="sender">The source of the end drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// This method is called at the end of a drag sequence.
		/// <para>
		/// </para>
		/// Unlike the <see cref="OnMouseDrag"/> method, this method will not get called
		/// until after the <see cref="MinDragStartDistance"/> has been reached.
		/// <para>
		/// <b>Notes to Inheritors:</b>  Overriding methods must still call
		/// <c>base.OnEndDrag()</c> for correct behavior.
		/// </para>
		/// </remarks>
		protected virtual void OnEndDrag(object sender, PInputEventArgs e) {
			StopDragActivity(e);
			dragEvent = null;
			source = null;
			e.Canvas.Interacting = false;
			Dragging = false;
			if (EndDrag != null) EndDrag(sender, e);
		}

		/// <summary>
		/// Returns true if a drag sequence should be initiated.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>True if a drag sequence should be initiated; otherwise, false.</returns>
		protected virtual bool ShouldStartDragInteraction(PInputEventArgs e) {
			return PUtil.DistanceBetweenPoints(MousePressedCanvasPoint, e.CanvasPosition)
				>= MinDragStartDistance;
		}
		#endregion

		#region Drag Activity
		//****************************************************************
		// Drag Activity - Used for scheduling an activity during a drag
		// sequence. For example zooming and auto panning are implemented
		// using this.
		//****************************************************************

		/// <summary>
		/// Gets the drag activity.
		/// </summary>
		/// <value>The drag activity.</value>
		public virtual PActivity DragActivity {
			get { return dragActivity; }
		}

		/// <summary>
		/// Schedules the drag activity to run.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void StartDragActivity(PInputEventArgs e) {
			dragActivity = new PActivity(-1, PUtil.DEFAULT_ACTIVITY_STEP_RATE);
			dragActivity.ActivityDelegate = this;

			e.Camera.Root.AddActivity(dragActivity);
		}

		/// <summary>
		/// Stops the drag activity.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		private void StopDragActivity(PInputEventArgs e) {
			dragActivity.Terminate();
			dragActivity = null;
		}

		/// <summary>
		/// Called when the drag activity starts running.
		/// </summary>
		/// <param name="activity">The drag activity.</param>
		public void ActivityStarted(PActivity activity) {
			OnDragActivityFirstStep(source, dragEvent);
		}
		
		/// <summary>
		/// Called when the drag activity is running.
		/// </summary>
		/// <param name="activity">The drag activity.</param>
		public void ActivityStepped(PActivity activity) {
			OnDragActivityStep(source, dragEvent);
		}

		/// <summary>
		/// Called when the drag activity stops running.
		/// </summary>
		/// <param name="activity">The drag activity.</param>
		public void ActivityFinished(PActivity activity) {
			OnDragActivityFinalStep(source, dragEvent);
		}

		/// <summary>
		/// Override this method to get notified when the drag activity is stepping.
		/// </summary>
		/// <param name="sender">The source of the drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// During a drag sequence an activity is scheduled that runs continuously while
		/// the drag sequence is active. This can be used to support some additional
		/// behavior that is not driven directly by mouse events. For example
		/// PZoomEventHandler uses it for zooming and PPanEventHandler uses it for auto
		/// panning.
		/// </remarks>
		protected virtual void OnDragActivityFirstStep(object sender, PInputEventArgs e) {
			if (DragActivityFirstStep != null) DragActivityFirstStep(sender, e);
		}

		/// <summary>
		/// Override this method to get notified when the drag activity starts stepping.
		/// </summary>
		/// <param name="sender">The source of the drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void OnDragActivityStep(object sender, PInputEventArgs e) {
			if (DragActivityStep != null) DragActivityStep(sender, e);
		}
		
		/// <summary>
		/// Override this method to get notified when the drag activity stops stepping.
		/// </summary>
		/// <param name="sender">The source of the drag event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void OnDragActivityFinalStep(object sender, PInputEventArgs e) {
			if (DragActivityFinalStep != null) DragActivityFinalStep(sender, e);
		}
		#endregion

		#region Events
		//****************************************************************
		// Events - Subclasses should not override these methods, instead
		// override the appropriate drag method.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseDown">
		/// PBasicInputEventHandler.OnMouseDown</see>.
		/// </summary>
		public override void OnMouseDown(object sender, PInputEventArgs e) {
			base.OnMouseDown (sender, e);

			if (sequenceInitiatedButton == MouseButtons.None) {
				sequenceInitiatedButton = e.Button;
			} else {
				return;
			}

			MousePressedCanvasPoint = e.CanvasPosition;
			if (!Dragging) {
				if (ShouldStartDragInteraction(e)) {
					OnStartDrag(sender, e);
				}
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseDrag">
		/// PBasicInputEventHandler.OnMouseDrag</see>.
		/// </summary>
		public override void OnMouseDrag(object sender, PInputEventArgs e) {
			base.OnMouseDrag (sender, e);

			if (sequenceInitiatedButton != MouseButtons.None) {
				if (!Dragging) {
					if (ShouldStartDragInteraction(e)) {
						OnStartDrag(sender, e);
					}
					return;
				}
				OnDrag(sender, e);
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseUp">
		/// PBasicInputEventHandler.OnMouseUp</see>.
		/// </summary>
		public override void OnMouseUp(object sender, PInputEventArgs e) {
			base.OnMouseUp (sender, e);

			if (sequenceInitiatedButton == e.Button) {
				if (Dragging) OnEndDrag(sender, e);
				sequenceInitiatedButton = MouseButtons.None;
			}
		}
		#endregion

		#region Debugging
		//****************************************************************
		// Debugging - methods for debugging
		//****************************************************************

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
		
				result.Append("minDragStartDistance=" + minDragStartDistance);
				result.Append(",mousePressedCanvasPoint=" + mousePressedCanvasPoint.ToString());
				result.Append(",sequenceInitiatedButton=" + sequenceInitiatedButton);
				if (dragging) result.Append(",dragging");
				result.Append(',');
				result.Append(base.ParamString);
		
				return result.ToString();
			}
		}
		#endregion
	}
}
