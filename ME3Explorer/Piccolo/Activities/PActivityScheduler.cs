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
using System.Collections;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.Piccolo.Activities {
	/// <summary>
	/// <b>PActivityScheduler</b> is responsible for managing a list of
	/// activities to be processed.
	/// </summary>
	/// <remarks>
	/// PActivityScheduler is given a chance to process these activities from 
	/// the PRoot's ProcessInputs() method. Most users will not need to use
	/// the PActivityScheduler directly, instead you should look at:
	/// <ul>
	/// <li><c>PNode.AddActivity</c> - to schedule a new activity</li>
	/// <li><c>PActivity.Terminate</c> - to terminate a running activity</li>
	/// <li><c>PRoot.ProcessInputs</c> - already calls processActivities for you.</li>
	/// </ul>
	/// </remarks>
	public class PActivityScheduler {
		#region Fields
		private PRoot root;
		private PActivityList activities;
		private Timer activityTimer;
		private bool activitiesChanged;
		private bool animating;
		private PActivityList processingActivities;
		private int stepActivitiesRecur = 0;
		private int processActivitiesRecur = 0;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PActivityScheduler.
		/// </summary>
		/// <param name="rootNode">The root node to associate with this activity scheduler.</param>
		public PActivityScheduler(PRoot rootNode) {
			root = rootNode;
			activities = new PActivityList();
			processingActivities = new PActivityList();
		}
		#endregion

		#region Basics
		/// <summary>
		/// Gets the root node associated with this activity scheduler.
		/// </summary>
		/// <value>The root node assocated with this activity scheduler.</value>
		/// <remarks>
		/// The root's <c>ProcessInputs</c> method calls <c>ProcessActivities</c> on
		/// this activity scheduler at the appropriate time in the UI loop.
		/// </remarks>
		public virtual PRoot Root {
			get { return root; }
		}

		/// <summary>
		/// Add this activity to the scheduler.
		/// </summary>
		/// <param name="activity">The activity to be added.</param>
		/// <remarks>
		/// Adding an activity schedules that activity to run at some point in the
		/// future.
		/// </remarks>
		public virtual void AddActivity(PActivity activity) {
			AddActivity(activity, false);
		}

		/// <summary>
		/// Add this activity to the scheduler.
		/// </summary>
		/// <param name="activity">The activity to be added.</param>
		/// <param name="processLast">
		/// Specifies whether the activity should be run last.
		/// </param>
		/// <remarks>
		/// Adding an activity schedules that activity to run at some point in the
		/// future.
		/// <para>
		/// Sometimes it's useful to make sure that an activity is run after all other
		/// activities have been run. To do this set processLast to true when adding the
		/// activity.
		/// </para>
		/// </remarks>
		public virtual void AddActivity(PActivity activity, bool processLast) {
			if (activities.Contains(activity)) return;

			activitiesChanged = true;
		
			if (processLast) {
				activities.Insert(0, activity);
			} else {
				activities.Add(activity);
			}

			activity.ActivityScheduler = this;

			if (!ActivityTimer.Enabled) {
				StartActivityTimer();
			}		
		}

		/// <summary>
		/// Remove this activity from the scheduler.
		/// </summary>
		/// <param name="activity">The activity to be removed.</param>
		/// <remarks>
		/// Once an activity has been removed from the scheduler, it will no longer be
		/// run.
		/// </remarks>
		public virtual void RemoveActivity(PActivity activity) {
			if (!activities.Contains(activity)) return;

			activitiesChanged = true;
			activities.Remove(activity);

			if (activities.Count == 0) {
				StopActivityTimer();
			}					
		}

		/// <summary>
		/// Removes all activities currently scheduled to run.
		/// </summary>
		/// <remarks>This method clears the scheduler.</remarks>
		public virtual void RemoveAllActivities() {		
			activitiesChanged = true;	
			activities.Clear();
			StopActivityTimer();
		}

		/// <summary>
		/// Gets a reference to the list of scheduled activities.
		/// </summary>
		/// <value>A reference to the activities list.</value>
		/// <remarks>
		/// This list should not be modified.
		/// </remarks>
		public virtual PActivityList ActivitiesReference {
			get { return activities; }
		}

		/// <summary>
		/// Gets a value indicating whether any of the scheduled activities are
		/// performing animations.
		/// </summary>
		public virtual bool Animating {
			get {
				if (activitiesChanged) {
					animating = false;
					foreach(PActivity each in activities) {
						animating |= each.IsAnimation;
					}	
					activitiesChanged = false;
				}
				return animating;
			}
		}

		/// <summary>
		/// Starts the timer that controls the stepping of activities.
		/// </summary>
		protected virtual void StartActivityTimer() {
			ActivityTimer.Start();
		}
	
		/// <summary>
		/// Stops the timer that controls the stepping of activities.
		/// </summary>
		protected virtual void StopActivityTimer() {
			ActivityTimer.Stop();
		}
		#endregion

		#region Process Activities
		/// <summary>
		/// Gets the timer that controls the stepping of activities.
		/// </summary>
		/// <value>The timer that controls activity processing.</value>
		protected virtual Timer ActivityTimer {
			get {
				if (activityTimer == null) {
					activityTimer = new Timer();
					activityTimer.Interval = PUtil.ACTIVITY_SCHEDULER_FRAME_INTERVAL;
					activityTimer.Tick += new EventHandler(StepActivities);
				}
				return activityTimer;
			}
		}

		/// <summary>
		/// Steps all running activities by calling the root's <c>ProcessInputs</c>,
		/// which in turn will call <c>ProcessActivities</c>.
		/// </summary>
		/// <param name="sender">The source of this Tick Event.</param>
		/// <param name="eArgs">The arguments for this Tick Event.</param>
		protected virtual void StepActivities(object sender, EventArgs eArgs) {
			// In some cases, starting another timer or setting its interval can
			// cause a tick to occur.  So, we need to catch re-entrances here.
			if (stepActivitiesRecur > 0) {
				return;
			}

			stepActivitiesRecur++;
			Root.ProcessInputs();
			stepActivitiesRecur--;
		}

		/// <summary>
		/// Process all scheduled activities for the given time. Each activity is
		/// given one "step", equivalent to one frame of animation.
		/// </summary>
		/// <param name="currentTime">The time for which to process each activity.</param>
		public virtual void ProcessActivities(long currentTime) {
			// In some cases, starting another timer or setting its interval can
			// cause ProcessActivities to get re-called.  So, we need to catch re-entrances here.
			if (processActivitiesRecur > 0) {
				return;
			}

			processActivitiesRecur++;
			int size = activities.Count;		
			if (size > 0) {
				processingActivities.Clear();
				processingActivities.AddRange(activities);
				for (int i = size - 1; i >= 0; i--) {
					PActivity each = processingActivities[i];
					each.ProcessStep(currentTime);
				}
			}
			processActivitiesRecur--;
		}
		#endregion
	}
}
