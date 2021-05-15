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
using System.Text;

using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.Piccolo.Activities {
	#region Enums
	/// <summary>
	/// This enumeration is used by the PActivity class.  It defines the behavior an activity
	/// has when its <see cref="PActivity.Terminate(TerminationBehavior)">Terminate</see>
	/// method is called.
	/// </summary>
	public enum TerminationBehavior {
		/// <summary>
		/// The method OnActivityFinished will never get called and so the activity
		/// will be terminated midway.
		/// </summary>
		TerminateWithoutFinishing,

		/// <summary>
		/// The method OnActivityFinished will always get called.  And so the activity
		/// will always end in it's completed state.  If the activity has not yet started
		/// the method OnActivityStarted will also be called.
		/// </summary>
		TerminateAndFinish,

		/// <summary>
		/// The method OnActivityFinished will only be called if the activity has previously
		/// started.
		/// </summary>
		TerminateAndFinishIfStepping
	}
	#endregion

	#region Activity Stepping Delegates
	/// <summary>
	/// Used to notify classes when the activity has started.
	/// </summary>
	public delegate void ActivityStartedDelegate(PActivity activity);

	/// <summary>
	/// Used to notify classes when the activity is running.
	/// </summary>
	public delegate void ActivitySteppedDelegate(PActivity activity);

	/// <summary>
	/// Used to notify classes when the activity has finished.
	/// </summary>
	public delegate void ActivityFinishedDelegate(PActivity activity);
	#endregion

	#region Summary
	/// <summary>
	/// <b>PActivity</b> controls some time dependent aspect of Piccolo, such
	/// as animation.
	/// </summary>
	/// <remarks>
	/// Once created activities must be scheduled with the PActivityScheduler
	/// managed by the PRoot to run. They are automatically removed from the
	/// scheduler when the animation has finished.
	/// </remarks>
	/// <example>
	/// There are several ways to be notfied of changes in an activity's state.  You can
	/// extend PActivity and override the <c>OnActivityStarted()</c>, <c>OnActivityStep()</c>, and
	/// <c>OnActivityFinished()</c> methods.  You can instantiate a PActivity and set its
	/// ActivityDelegate to a class that implements the PActivityDelegate interface methods
	/// (<c>ActivityStarted()</c>, <c>ActivityStepped()</c>, and <c>ActivityFinished()</c>).  Or,
	/// if you only wish to be notified of some of these changes you can directly set any of
	/// the individual delegates (<c>ActivityStarted</c>, <c>ActivityStepped</c> and
	/// <c>ActivityFinished</c>).  Each of these approaches are illustrated below:
	/// <para>
	/// <b>Extend PActivity</b>
	/// <code>
	///	public class MyActivity : PActivity {
	///	
	///		...
	///		
	///		protected override void OnActivityStarted() {
	///			base.OnActivityStarted ();
	///			// Do something when the activity starts.
    ///		}
    ///					
	///		protected override void OnActivityStep(long elapsedTime) {
	///			base.OnActivityStep (elapsedTime);
	///			// Do something while the activity is running.
	///		}
	///					
	///		protected override void OnActivityFinished() {
	///			base.OnActivityFinished ();
	///			// Do something when the activity finishes.
	///		}
	///		
	///		...
	///		
	///	}
	///	</code>
	/// </para>
	/// 
	/// <para>
	/// <b>Set the ActivityDelegate</b>
	/// <code>
	///	...
	///		
	///	PActivty activity = new PActivity();
	///	activity.ActivityDelegate = new MyActivityDelegate();
	///		
	///	...
	///		
	///	public class MyActivityDelegate : PActivity.PActivityDelegate {
	///		public void ActivityStarted(PActivity activity) {
	///			// Do something when the activity starts.
	///		}
	///			
	///		public void ActivityStepped(PActivity activity) {
	///			// Do something while the activity is running.
	///		}
	///			
	///		public void ActivityFinished(PActivity activity) {
	///			// Do something when the activity finishes.
	///		}
	///	}	
	///	</code>
	/// </para>
	/// 
	/// <para>
	/// <b>Set an Individual Delegate</b>
	/// <code>
	/// ...
	/// 
	/// PActivity activity = new PActivity();
	/// activity.ActivityStepped = new ActivitySteppedDelegate(MySteppedDelegate);
	/// 
	/// ...
	/// 
	/// protected void MySteppedDelegate(PActivity activity) {
	///		// Do something while the activity is running.
	/// }
	/// </code>
	/// </para>
	/// 
	/// <para>
	/// See the <c>PNode.Animate*()</c> methods for more examples of how to setup and run
	/// different activities.
	/// </para>
	/// </example>
	#endregion
	public class PActivity {
		#region Fields
		private PActivityScheduler scheduler;

		private long startTime;
		private long duration;
		private long stepInterval;

		/// <summary>
		/// Used to notify classes when the activity has started.
		/// </summary>
		/// <remarks>
		/// When the PActivityDelegate is set, the <c>ActivityStarted()</c> method is connected to the
		/// this delegate.
		/// </remarks>
		public ActivityStartedDelegate ActivityStarted;

		/// <summary>
		/// Used to notify classes when the activity is running.
		/// </summary>
		/// <remarks>
		/// When the PActivityDelegate is set, the <c>ActivityStepped()</c> method is connected to the
		/// this delegate.
		/// </remarks>
		public ActivitySteppedDelegate ActivityStepped;

		/// <summary>
		/// Used to notify classes when the activity has finished.
		/// </summary>
		/// <remarks>
		/// When the PActivityDelegate is set, the <c>ActivityFinished()</c> method is connected to this
		/// delegate.
		/// </remarks>
		public ActivityFinishedDelegate ActivityFinished;
	
		private bool stepping;
		private long nextStepTime;
		#endregion

		#region Activity Delegate Interface
		/// <summary>
		/// <b>PActivityDelegate</b> is used by classes to learn about and act on the
		/// different states that a PActivity goes through, such as when the activity
		/// starts and stops stepping.
		/// </summary>
		public interface PActivityDelegate {
			/// <summary>
			/// This method is used to notify a user right before an activity is scheduled
			/// to start running.
			/// </summary>
			/// <remarks>
			/// This method will be connected to the ActivityStarted delegate, which is called
			/// from the <c>OnActivityStarted()</c> method.
			/// </remarks>
			/// <param name="activity">The activity that is about to start.</param>
			void ActivityStarted(PActivity activity);

			/// <summary>
			/// This method is used to notify a user when the activity is running.
			/// </summary>
			/// <remarks>
			/// This method will be connected to the ActivityStepped delegate, which is called
			/// from the <c>OnActivityStep()</c> method.
			/// </remarks>
			/// <param name="activity">The activity that is running.</param>
			void ActivityStepped(PActivity activity);

			/// <summary>
			/// This method is used to notify a user after an activity has finished running and
			/// the activity has been removed from the PActivityScheduler queue.
			/// </summary>
			/// <remarks>
			/// This method will be connected to the ActivityFinished delegate, which is called
			/// from the <c>OnActivityStarted()</c> method.
			/// </remarks>
			/// <param name="activity">The activity that has just finished.</param>
			void ActivityFinished(PActivity activity);
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PActivity.
		/// </summary>
		/// <param name="aDuration">
		/// The amount of time this activity should take to complete, -1 for infinite.
		/// </param>
		public PActivity(long aDuration) : this(aDuration, PUtil.DEFAULT_ACTIVITY_STEP_RATE) {}
	
		/// <summary>
		/// Constructs a new PActivity.
		/// </summary>
		/// <param name="aDuration">
		/// The amount of time this activity should take to complete, -1 for infinite.
		/// </param>
		/// <param name="aStepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		public PActivity(long aDuration, long aStepInterval)
			: this(aDuration, aStepInterval, PUtil.CurrentTimeMillis) {}

		/// <summary>
		/// Constructs a new PActivity.
		/// </summary>
		/// <param name="aDuration">
		/// The amount of time this activity should take to complete, -1 for infinite.
		/// </param>
		/// <param name="aStepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="aStartTime">
		/// The time (relative to <c>PUtil.CurrentTimeMillis</c>) that this activity should start.
		/// </param>
		public PActivity(long aDuration, long aStepInterval, long aStartTime) {
			duration = aDuration;
			stepInterval = aStepInterval;
			startTime = aStartTime;
			nextStepTime = aStartTime;
			stepping = false;
		}
		#endregion

		#region Basics
		/// <summary>
		/// Gets or sets the time that this activity should start running in PRoot global time.
		/// </summary>
		/// <value>The time that this activity should start.</value>
		/// <remarks>
		/// When this time is reached (or soon after) this activity will have its
		/// <c>OnActivityStarted()</c> method called.
		/// </remarks>
		public virtual long StartTime {
			get { return startTime; }
			set { startTime = value; }
		}

		/// <summary>
		/// Gets or sets the minimum number of milliseconds that this activity should delay
		/// between steps.
		/// </summary>
		/// <value>
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </value>
		public virtual long StepInterval {
			get { return stepInterval; }
			set { stepInterval = value; }
		}

		/// <summary>
		/// Gets the next time this activity should step, in PRoot global time.
		/// </summary>
		/// <value>The next step time for this activity.</value>
		public virtual long NextStepTime {
			get { return nextStepTime; }
		}

		/// <summary>
		/// Gets or sets the amount of time that this activity should take to complete, after
		/// the <c>OnActivityStarted</c> method is called.
		/// </summary>
		/// <value>The amount of time this activity should take to complete.</value>
		public virtual long Duration {
			get { return duration; }
			set { duration = value; }
		}

		/// <summary>
		/// Gets or sets the activity scheduler associated with this activity.
		/// </summary>
		/// <value>The activity scheduler associated with this activity.</value>
		/// <remarks>
		/// An activity scheduler manages a list of activities to be run.  Typically,
		/// activities are scheduled with the activity scheduler stored in the root node of
		/// the scene graph tree.
		/// <seealso cref="UMD.HCIL.Piccolo.Activities.PActivityScheduler"/>
		/// <seealso cref="UMD.HCIL.Piccolo.PRoot"/>
		/// </remarks>
		public virtual PActivityScheduler ActivityScheduler {
			get { return scheduler; }
			set { scheduler = value; }
		}
		#endregion

		#region Stepping
		/// <summary>
		/// Gets a value indicating whether this activity is stepping.
		/// </summary>
		/// <value>True if this activity is stepping; else false.</value>
		public virtual bool IsStepping {
			get { return stepping; }
		}

		/// <summary>
		/// Gets a value indicating whether this activity is performing an animation.
		/// </summary>
		/// <value>True if this activity is performing an animation; otherwise, false.</value>
		/// <remarks>
		/// This is used by the PCanvas to determine if it should set the render quality to
		/// PCanvas.animatingRenderQuality or not for each frame it renders. 
		///</remarks>
		public virtual bool IsAnimation {
			get { return false; }
		}

		/// <summary>
		/// This method is called right before an activity is scheduled to start running.
		/// </summary>
		/// <remarks>
		/// After this method is called <c>OnActivityStep()</c> will be called until the
		/// activity finishes.
		/// </remarks>
		protected virtual void OnActivityStarted() {
			if (ActivityStarted != null)
				ActivityStarted(this);
		}

		/// <summary>
		/// This method is called repeatedly when the activity is running.
		/// </summary>
		/// <param name="elapsedTime">
		/// The amount of time that has passed relative to the activities startTime.
		/// </param>
		/// <remarks>
		/// This is the method that most activities override to perform their behavior.
		/// </remarks>
		protected virtual void OnActivityStep(long elapsedTime) {
			if (ActivityStepped != null)
				ActivityStepped(this);
		}

		/// <summary>
		/// This method is called after an activity has finished running and the activity
		/// has been removed from the PActivityScheduler queue.
		/// </summary>
		protected virtual void OnActivityFinished() {
			if (ActivityFinished != null)
				ActivityFinished(this);
		}

		/// <summary>
		/// Sets the PActivityDelegate for this activity.
		/// </summary>
		/// <value>The PActivityDelegate associated with this activity.</value>
		/// <remarks>
		/// The delegate is notified when the activity starts and stops stepping.
		/// </remarks>
		public virtual PActivityDelegate ActivityDelegate {
			set {
				this.ActivityStarted = new ActivityStartedDelegate(value.ActivityStarted);
				this.ActivityStepped = new ActivitySteppedDelegate(value.ActivityStepped);
				this.ActivityFinished = new ActivityFinishedDelegate(value.ActivityFinished);
			}
		}
		#endregion

		#region Controlling
		/// <summary>
		/// Schedules this activity to start after the first activity has finished.
		/// </summary>
		/// <param name="first">The activity to start after.</param>
		/// <remarks>
		/// Note that no link is created between these activities, if the startTime or duration
		/// of the first activity is later changed this activities start time will not be updated
		/// to reflect that change.
		/// </remarks>
		public virtual void StartAfter(PActivity first) {
			StartTime = first.StartTime + first.Duration;
		}
		
		/// <summary>
		/// Stop this activity immediately, and remove it from the activity scheduler.
		/// </summary>
		/// <remarks>
		/// The default termination behavior is to call <c>OnActivityFinished()</c> if
		/// the activity is currently stepping. Use Terminate(TerminationBehavior) for
		/// a different termination behavior.
		/// </remarks>
		public virtual void Terminate() {
			Terminate(TerminationBehavior.TerminateAndFinishIfStepping);
		}

		/// <summary>
		/// Stop this activity immediately, and remove it from the activity scheduler.
		/// </summary>
		/// <remarks>
		/// The <see cref="TerminationBehavior">TerminationBehavior</see> determines when
		/// and if OnActivityStarted and OnActivityFinished get called.
		/// </remarks>
		/// <param name="terminationBehavior"></param>
		public virtual void Terminate(TerminationBehavior terminationBehavior) {
			if (scheduler != null) {
				scheduler.RemoveActivity(this);
			}
		
			switch (terminationBehavior) {
				case TerminationBehavior.TerminateWithoutFinishing:
					stepping = false;
					break;
				
				case TerminationBehavior.TerminateAndFinish:
					if (stepping) {
						stepping = false;
						OnActivityFinished();
					} else {
						OnActivityStarted();
						OnActivityFinished();
					}

					break;
				
				case TerminationBehavior.TerminateAndFinishIfStepping:
					if (stepping) {
						stepping = false;
						OnActivityFinished();
					}
					break;
			}
		}	

		/// <summary>
		/// The activity scheduler calls this method and it is here that the activity decides
		/// if it should do a step or not for the given time.
		/// </summary>
		/// <param name="currentTime">
		/// The time for which this activity must decide whether or not to step.
		/// </param>
		/// <returns>The step rate of this activity, -1 if past the stopTime.</returns>
		public virtual long ProcessStep(long currentTime) {
			// if before start time
			if (currentTime < startTime) {
				return startTime - currentTime;	 
			}
		
			// if past stop time
			if (currentTime > StopTime) {
				if (stepping) {
					stepping = false;
					scheduler.RemoveActivity(this);
					OnActivityFinished();
				} else {
					OnActivityStarted();
					scheduler.RemoveActivity(this);
					OnActivityFinished();
				}
				return -1;
			}
		
			// else should be stepping
			if (!stepping) { 
				OnActivityStarted();
				stepping = true;
			}

			if (currentTime >= nextStepTime) {
				OnActivityStep(currentTime - startTime);
				nextStepTime = currentTime + stepInterval;
			}

			return stepInterval;
		}
	
		/// <summary>
		/// Gets the time when this activity should finish running.
		/// </summary>
		/// <value>The time when this activity should finish running.</value>
		/// <remarks>
		/// At this time (or soon after) the <c>OnActivityFinished()</c> method will be called
		/// </remarks>
		public virtual long StopTime {
			get {
				if (duration == -1) {
					return long.MaxValue;
				}
				return startTime + duration;
			}
		}
		#endregion

		#region Debugging
		/// <summary>
		/// Returns a string representation of this object for debugging purposes.
		/// </summary>
		/// <returns>A string representation of this node's state.</returns>
		public override string ToString() {
			return base.ToString() + "[" + ParamString + "]";
		}

		/// <summary>
		/// Gets a string representing the state of this object.
		/// </summary>
		/// <value>A string representation of this object's state.</value>
		/// <remarks>
		/// This method is intended to be used only for debugging purposes, and the content and
		/// format of the returned string may vary between implementations. The returned string
		/// may be empty but may not be <c>null</c>.
		/// </remarks>
		protected virtual String ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("startTime=" + startTime);
				result.Append(",duration=" + duration);
				result.Append(",stepInterval=" + stepInterval);
				if (stepping) result.Append(",stepping");
				result.Append(",nextStepTime=" + nextStepTime);

				return result.ToString();
			}
		}
		#endregion
	}
}
