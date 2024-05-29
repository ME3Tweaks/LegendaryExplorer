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

using System.Collections.Generic;
using Piccolo.Activities;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo {
	#region Delegates
	/// <summary>
	/// A delegate used to invoke the <c>ProcessScheduledInputs</c> method on
	/// the main UI thread.
	/// </summary>
	public delegate void ProcessScheduledInputsDelegate();
	#endregion

	#region Input Source Interface
	/// <summary>
	/// This interface is for advanced use only. If you want to implement a
	/// different kind of input framework than Piccolo provides you can hook
	/// it in here.
	/// </summary>
	public interface InputSource {
		/// <summary>
		/// Process pending input events.
		/// </summary>
		void ProcessInput();
	}
	#endregion

	/// <summary>
	/// <b>PRoot</b> serves as the top node in Piccolo's runtime structure.
	/// </summary>
	/// <remarks>
	/// The PRoot is responsible for running the main UI loop that processes
	/// input from activities and external events.
	/// </remarks>
	public sealed class PRoot : PNode {
		#region Fields
		/// <summary>
		/// The key that identifies a change in the set of this root's
		/// <see cref="InputSource">InputSource</see>s.
		/// </summary>
		/// <remarks>
		/// In a property change event the new value will be a reference to the list of this
		/// root's input sources, but the old value will always be null.
		/// </remarks>
        private static readonly object PROPERTY_KEY_INPUT_SOURCES = new();

		/// <summary>
		/// A bit field that identifies a <see cref="InputSourcesChanged">InputSourcesChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether InputSourcesChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_INPUT_SOURCES = 1 << 13;

		/// <summary>
		/// A flag that indicates whether Piccolo is currently processing inputs.
		/// </summary>
        private bool processingInputs;

		/// <summary>
		/// A flag that indicates whether inputs are scheduled to be processed.
		/// </summary>
        private bool processInputsScheduled;

		/// <summary>
		/// Used to invoke the <c>ProcessScheduledInputs</c> method on the main UI
		/// thread.
		/// </summary>
        private readonly ProcessScheduledInputsDelegate processScheduledInputsDelegate;

		private PInputManager defaultInputManager;
		private readonly List<InputSource> inputSources;
		private long globalTime;
		private readonly PActivityScheduler activityScheduler;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PRoot.
		/// </summary>
		/// <remarks>
		/// Note the PCanvas already creates a basic scene graph for you so usually you
		/// will not need to construct your own roots.
		/// </remarks>
		public PRoot() {
			inputSources = new List<InputSource>();
			processScheduledInputsDelegate = ProcessScheduledInputs;
			globalTime = PUtil.CurrentTimeMillis;
			activityScheduler = new PActivityScheduler(this);
		}
		#endregion

		#region InputSources
		/// <summary>
		/// Occurs when there is a change in the set of this root's
		/// <see cref="InputSource">InputSource</see>s.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the InputSourcesChanged Event as in
		/// InputSourcesChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_INPUT_SOURCES in the Events list).
		/// When a user removes an event handler from the InputSourcesChanged event as in 
		/// InputSourcesChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_INPUT_SOURCES in the Events list).
		/// </remarks>
		public event PPropertyEventHandler InputSourcesChanged {
			add => HandlerList.AddHandler(PROPERTY_KEY_INPUT_SOURCES, value);
            remove => HandlerList.RemoveHandler(PROPERTY_KEY_INPUT_SOURCES, value);
        }
		#endregion

		#region Activities
		//****************************************************************
		// Activities - Methods for scheduling activities to run.
		//****************************************************************

		/// <summary>
		/// Overridden.  Add an activity to the activity scheduler associated with
		/// this root.
		/// </summary>
		/// <param name="activity">The new activity to scheduled.</param>
		/// <returns>
		/// True if the activity is successfully scheduled; otherwise, false.
		/// </returns>
		/// <remarks>
		/// Activities are given a chance to run during each call to the root's
		/// <c>ProcessInputs</c> method. When the activity has finished running it
		/// will automatically get removed.
		/// </remarks>
		public override bool AddActivity(PActivity activity) {
			ActivityScheduler.AddActivity(activity);
			return true;
		}

		/// <summary>
		/// Get the activity scheduler associated with this root.
		/// </summary>
		public PActivityScheduler ActivityScheduler => activityScheduler;

        /// <summary>
		/// Wait for all scheduled activities to finish before returning from
		///	this method. This will freeze out user input, and so it is generally
		/// recommended that you use <c>PActivity.StartTime</c> and
		/// <c>PActivity.StartAfter</c> to offset activities instead of using
		/// this method.
		/// </summary>
		public void WaitForActivities() {
			while (activityScheduler.ActivitiesReference.Count > 0) {
				ProcessActivitiesNow();
			}
		}

		/// <summary>
		/// Wait for all the specified activity to finish before returning from
		///	this method. This will freeze out user input, and so it is generally
		/// recommended that you use <c>PActivity.StartTime</c> and
		/// <c>PActivity.StartAfter</c> to offset activities instead of using
		/// this method.
		/// </summary>
		public void WaitForActivity(PActivity activity) {
			while (activityScheduler.ActivitiesReference.Contains(activity)) {
				ProcessActivitiesNow();
			}
		}

		/// <summary>
		/// Step activities and update all associated canvases immediately.
		/// </summary>
        private void ProcessActivitiesNow() {
			ProcessInputs();

			List<PNode> nodes = GetAllNodes(PUtil.CAMERA_WITH_CANVAS_FILTER, null);
			foreach (PCamera each in nodes) {
				each.Canvas.PaintImmediately();
			}
		}
		#endregion

		#region Basics
		/// <summary>
		/// Overridden.  Get's this.
		/// </summary>
		/// <value>This root node.</value>
		public override PRoot Root => this;

        /// <summary>
		/// Gets the default input manager to be used when processing input events.
		/// </summary>
		/// <value>The default input manager.</value>
		/// <remarks>
		/// PCanvas's use this method when they forward new input events to the
		/// PInputManager.
		/// </remarks>
		public PInputManager DefaultInputManager {
			get {
				if (defaultInputManager == null) {
					defaultInputManager = new PInputManager();
					AddInputSource(defaultInputManager);
				}
				return defaultInputManager;
			}
		}

		/// <summary>
		/// Advanced. If you want to add additional input sources to the root's UI process
		/// you can do that here.
		/// </summary>
		/// <param name="inputSource">The new input source to add.</param>
		/// <remarks>
		/// You will seldom do this unless you are making additions to the piccolo framework.
		/// </remarks>
		public void AddInputSource(InputSource inputSource) {
			inputSources.Add(inputSource);
			FirePropertyChangedEvent(PROPERTY_KEY_INPUT_SOURCES, PROPERTY_CODE_INPUT_SOURCES, null, inputSources);
		}

		/// <summary>
		/// Advanced. If you want to remove an input source from the root's UI process you
		/// can do that here.
		/// </summary>
		/// <param name="inputSource">The input source to remove.</param>
		/// <remarks>
		/// You will seldom do this unless you are making additions to the piccolo framework.
		/// </remarks>
		public void RemoveInputSource(InputSource inputSource) {
			inputSources.Remove(inputSource);
			FirePropertyChangedEvent(PROPERTY_KEY_INPUT_SOURCES, PROPERTY_CODE_INPUT_SOURCES, null, inputSources);
		}
		#endregion

		#region UI Loop
		//****************************************************************
		// UI Loop - Methods for running the main UI loop of Piccolo. 
		//****************************************************************

		/// <summary>
		/// Gets the global Piccolo time.
		/// </summary>
		/// <remarks>
		/// This is set to <c>PUtil.CurrentTimeMillis</c> at the beginning of the root's
		/// <c>ProcessInputs</c> method.  Activities should usually use this global time
		/// instead of <c>PUtil.CurrentTimeMillis</c> so that multiple activities will be
		/// synchronized.
		/// </remarks>
		public long GlobalTime => globalTime;

        /// <summary>
		/// This is the heartbeat of the Piccolo framework, where all processing is done.
		/// </summary>
		/// <remarks>
		/// In this method, pending input events are processed, Activities are given a
		/// chance to run, and the bounds caches and any paint damage are validated.
		/// </remarks>
		public void ProcessInputs() {
			PDebug.StartProcessingInput();
			processingInputs = true;

			globalTime = PUtil.CurrentTimeMillis;
			foreach (InputSource each in inputSources) {
				each.ProcessInput();
			}

			activityScheduler.ProcessActivities(globalTime);
			ValidateFullBounds();
			ValidateFullPaint();

            processingInputs = false;
			PDebug.EndProcessingInput();

			//force the control to redraw when input has caused invalidation.
			//Without this there can be a huge lag when dragging nodes
            PCanvas invokeCanvas = InvokeCanvas;
            if (invokeCanvas.IsInvalidated)
            {
                invokeCanvas.Update();
            }
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.FullBoundsInvalid">PNode.FullBoundsInvalid</see>.
		/// </summary>
		protected override bool FullBoundsInvalid {
			set { 
				base.FullBoundsInvalid = value;
				ScheduleProcessInputsIfNeeded();
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.ChildBoundsInvalid">PNode.ChildBoundsInvalid</see>.
		/// </summary>
		protected override bool ChildBoundsInvalid {
			set { 
				base.ChildBoundsInvalid = value;
				ScheduleProcessInputsIfNeeded();
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.PaintInvalid">PNode.PaintInvalid</see>.
		/// </summary>
		public override bool PaintInvalid {
			set {
				base.PaintInvalid = value;
				ScheduleProcessInputsIfNeeded();
			}
		}
	
		/// <summary>
		/// Overridden.  See <see cref="PNode.ChildPaintInvalid">PNode.ChildPaintInvalid</see>.
		/// </summary>
		public override bool ChildPaintInvalid {
			set { 
				base.ChildPaintInvalid = value;
				ScheduleProcessInputsIfNeeded();
			}
		}

		/// <summary>
		/// Processes currently scheduled inputs and resets processInputsScheduled flag.
		/// </summary>
        private void ProcessScheduledInputs() {
			ProcessInputs();
			processInputsScheduled = false;
		}

		/// <summary>
		/// If something in the scene graph needs to be updated, this method will schedule
		/// ProcessInputs run at a later time.
		/// </summary>
		public void ScheduleProcessInputsIfNeeded() {
			PDebug.ScheduleProcessInputs();

			if (!processInputsScheduled && !processingInputs && 
				(FullBoundsInvalid || ChildBoundsInvalid || PaintInvalid || ChildPaintInvalid)) {
				PCanvas canvas = InvokeCanvas;
				if (canvas is { IsHandleCreated: true } && processScheduledInputsDelegate != null) {
					processInputsScheduled = true;
					canvas.BeginInvoke(processScheduledInputsDelegate);
				}
			}
		}

		/// <summary>
		/// Returns a canvas hosting the piccolo scene-graph, to be used for invoking.
		/// </summary>
		private PCanvas InvokeCanvas {
			get {
				PCanvas canvas = null;
				foreach (PNode child in this) {
					if (child is PCamera pCamera) {
						PCamera camera = pCamera;
						if (camera.Canvas != null) {
							canvas = camera.Canvas;
							break;
						}
					}
				}
				return canvas;
			}
		}
		#endregion
	}
}