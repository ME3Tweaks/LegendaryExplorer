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
using Piccolo.Util;

namespace Piccolo.Activities {
	#region Enums
	/// <summary>
	/// This enumeration is used by the PInterpolatingActivity class.  It represents the
	/// types of interpolation that the activity can perform.
	/// </summary>
	public enum ActivityMode {
		/// <summary>
		/// The activity should interpolate from the source to the destination.
		/// </summary>
		SourceToDestination,

		/// <summary>
		/// The activity should interpolate from the destination to the source.
		/// </summary>
		DestinationToSource,

		/// <summary>
		/// The activity should interpolate from the source to the destination and back
		/// to the source again.
		/// </summary>
		SourceToDestinationToSource
	};
	#endregion

	/// <summary>
	/// <b>PInterpolatingActivity</b> interpolates between two states (source and
	/// destination) over the duration of the activity.
	/// </summary>
	/// <remarks>
	/// The interpolation can be either linear or slow- in, slow-out.
	/// <para>
	/// The mode determines how the activity interpolates between the two states. The
	/// default mode interpolates from source to destination, but you can also go
	/// from destination to source, and from source to destination to source.
	/// </para>
	/// <para>
	/// A loopCount of greater then one will make the activity reschedule itself when
	/// it has finished. This makes the activity loop between the two states.
	/// </para>
	/// </remarks>
	public class PInterpolatingActivity : PActivity {
		#region Fields
		private ActivityMode mode;
		private bool slowInSlowOut;
		private int loopCount;
		private bool firstLoop;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PInterpolatingActivity that will interpolate from the source
		/// state to the destination state.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		public PInterpolatingActivity(long duration, long stepInterval) : 
			this(duration, stepInterval, 1, ActivityMode.SourceToDestination) {
		}

		/// <summary>
		/// Constructs a new PInterpolatingActivity that will interpolate from the source
		/// state to the destination state, looping the given number of iterations.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="loopCount">
		/// The number of times the activity should reschedule itself.
		/// </param>
		/// <param name="mode">
		/// The mode defines how the activity interpolates between states.
		/// </param>
		public PInterpolatingActivity(long duration, long stepInterval, int loopCount, ActivityMode mode) :
			this(duration, stepInterval, PUtil.CurrentTimeMillis, loopCount, mode) {}

		/// <summary>
		/// Constructs a new PInterpolatingActivity that will interpolate between two states
		/// in the order specified by the mode, starting at the given start time and looping
		/// the given number of iterations.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="startTime">
		/// The time (relative to <c>PUtil.CurrentTimeMillis()</c>) that
		/// this activity should start.
		/// </param>
		/// <param name="loopCount">
		/// The number of times the activity should reschedule itself.
		/// </param>
		/// <param name="mode">
		/// The mode defines how the activity interpolates between states.
		/// </param>
		public PInterpolatingActivity(long duration, long stepInterval, long startTime, int loopCount, ActivityMode mode) :
			base(duration, stepInterval, startTime) {
			this.loopCount = loopCount;
			this.mode = mode;
			slowInSlowOut = true;
			firstLoop = true;
		}
		#endregion

		#region Basics
		/// <summary>
		/// Overriden.  Set the amount of time that this activity should take to complete,
		/// after the startStepping method is called.
		/// </summary>
		/// <remarks>
		/// The duration must be greater than zero so that the interpolation value can be
		/// computed.
		/// </remarks>
		public override long Duration {
			set {
				if (value <= 0)
					throw new ArgumentException ("Duration for PInterpolatingActivity must be greater then 0");

				base.Duration = value;
			}
		}

		/// <summary>
		/// Gets or sets the mode that defines how the activity interpolates between states.
		/// </summary>
		/// <value>The mode that defines how the activity interpolates between states.</value>
		public virtual ActivityMode Mode {
			get => mode;
            set => mode = value;
        }

		/// <summary>
		/// Gets or sets the number of times the activity should automatically reschedule
		/// itself after it has finished.
		/// </summary>
		/// <value>The number of times the activity should loop.</value>
		public virtual int LoopCount {
			get => loopCount;
            set => loopCount = value;
        }

		/// <summary>
		/// Gets or sets a value indicating whether the activity is executing its first loop.
		/// </summary>
		/// <value>A value indicating whether the activity is executing its first loop.</value>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses normally initialize their source state on the first loop.
		/// <para>
		/// This property will rarely need to be set, unless your are reusing activities.
		/// </para>
		/// </remarks>
		public virtual bool FirstLoop {
			get => firstLoop;
            set => firstLoop = value;
        }

		/// <summary>
		/// Gets or sets a value indicating whether the activity will be a pure interpolation, or
		/// simulate acceleration and friction.
		/// </summary>
		/// <value>
		/// A value indicating whether the activity should simulate acceleration and friction.
		/// </value>
		public virtual bool SlowInSlowOut {
			get => slowInSlowOut;
            set => slowInSlowOut = value;
        }
		#endregion

		#region Stepping
		/// <summary>
		/// Overridden.  See <see cref="PActivity.OnActivityStarted">PActivity.OnActivityStarted</see>.
		/// </summary>
		protected override void OnActivityStarted() {
			base.OnActivityStarted ();
			RelativeTargetValue = 0;
		}

		/// <summary>
		/// Overridden.  See <see cref="PActivity.OnActivityStep">PActivity.OnActivityStep</see>.
		/// </summary>
		protected override void OnActivityStep(long elapsedTime) {
			base.OnActivityStep (elapsedTime);

			float t = elapsedTime / (float) Duration;
				
			t = Math.Min(1, t);
			t = Math.Max(0, t);
		
			if (SlowInSlowOut) {
				t = ComputeSlowInSlowOut(t);
			}
		
			RelativeTargetValue = t;
		}

		/// <summary>
		/// Overridden.  See <see cref="PActivity.OnActivityFinished">PActivity.OnActivityFinished</see>.
		/// </summary>
		protected override void OnActivityFinished() {
			RelativeTargetValue = 1;
			base.OnActivityFinished();
			
			PActivityScheduler scheduler = ActivityScheduler;
			if (loopCount > 1) {
				if (loopCount != int.MaxValue) loopCount--;
				firstLoop = false;
				StartTime = scheduler.Root.GlobalTime;
				scheduler.AddActivity(this);
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PActivity.Terminate()">PActivity.Terminate</see>.
		/// </summary>
		public override void Terminate() {
			loopCount = 0; // set to zero so that we don't reschedule self.
			base.Terminate ();
		}

		/// <summary>
		/// Subclasses should override this method and set the value on their
		/// target (the object that they are modifying) accordingly.
		/// </summary>
		/// <param name="zeroToOne">The current interpolation value.</param>
		public virtual void SetRelativeTargetValue(float zeroToOne) {
		}

		/// <summary>
		/// Compute the adjusted t value for simulating acceleration and friction.
		/// </summary>
		/// <param name="zeroToOne">The t value, between 0 and 1.</param>
		/// <returns>The adjusted t value.</returns>
		public virtual float ComputeSlowInSlowOut(float zeroToOne) {
			if (zeroToOne < 0.5) {
				return 2.0f * zeroToOne * zeroToOne;
			} else {
				float complement = 1.0f - zeroToOne;
				return 1.0f - (2.0f * complement * complement);
			}
		}

		/// <summary>
		/// Calls <c>SetRelativeTargetValue</c> with the appropriate t value for the current mode.
		/// </summary>
		protected virtual float RelativeTargetValue {
			set {
				float zeroToOne = value;
				switch (mode) {
					case ActivityMode.SourceToDestination:
						break;

					case ActivityMode.DestinationToSource:
						zeroToOne = 1 - zeroToOne;
						break;

					case ActivityMode.SourceToDestinationToSource:
						if (zeroToOne <= 0.5) {
							zeroToOne *= 2;
						} else {
							zeroToOne = 1 - ((zeroToOne - 0.5f) * 2);
						}
						break;
				}

				SetRelativeTargetValue(zeroToOne);
			}
		}
		#endregion

		#region Debugging
		/// <summary>
		/// Overridden.  Gets a string representing the state of this object.
		/// </summary>
		/// <value>A string representation of this object's state.</value>
		/// <remarks>
		/// This method is intended to be used only for debugging purposes, and the content and
		/// format of the returned string may vary between implementations. The returned string
		/// may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override string ParamString {
			get {
				StringBuilder result = new StringBuilder();

				if (slowInSlowOut) {
					result.Append("slowinSlowOut,");			
				}
		
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}
