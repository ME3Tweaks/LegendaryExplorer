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

using UMD.HCIL.Piccolo.Activities;

namespace UMD.HCIL.PiccoloX.Activities {
	/// <summary>
	/// <b>PPathActivity</b> is the abstract base class for all path activity interpolators.
	/// Path activities interpolate between multiple states over the duration of the activity.
	/// </summary>
	/// <remarks>
	/// Knots are used to determine when in time the activity should move from state to state.
	/// Knot values should be increasing in value from 0 to 1 inclusive.
	/// <para>
    /// See <see cref="PPositionPathActivity"/> for a concrete path activity that will animate
    /// through a list of points.
    /// </para>
    /// </remarks>
	public abstract class PPathActivity : PInterpolatingActivity {
		#region Fields
		/// <summary>
		/// An array of values between 0 and 1 that indicate when the activity should
		/// transition from state to state.
		/// </summary>
		protected float[] knots;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPathActivity that will interpolate between multiple states from
		/// source to destination, transitioning from state to state as specified by the given
		/// knot values.
		/// </summary>
		/// <param name="duration">The length of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="knots">
		/// An array of values between 0 and 1 that indicate when the state transitions should
		/// occur.
		/// </param>
		public PPathActivity(long duration, long stepInterval, float[] knots)
			: this(duration, stepInterval, 0, ActivityMode.SourceToDestination, knots) {
		}

		/// <summary>
		/// Constructs a new PPathActivity that will interpolate between multiple states in the
		/// order specified by the mode, transitioning from state to state as specified by the
		/// given knot values and looping the given number of iterations.
		/// </summary>
		/// <param name="duration">The length of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="loopCount">
		/// The number of times the activity should reschedule itself.
		/// </param>
		/// <param name="mode">
		/// The mode defines how the activity interpolates between states.
		/// </param>
		/// <param name="knots">
		/// An array of values between 0 and 1 that indicate when the state transitions should
		/// occur.
		/// </param>
		public PPathActivity(long duration, long stepInterval, int loopCount, ActivityMode mode, float[] knots)
			: base (duration, stepInterval, loopCount, mode) {
			Knots = knots;
		}
		#endregion

		#region Knots
		/// <summary>
		/// Gets the current length of the knots array.
		/// </summary>
		/// <value>The current length of the knots array.</value>
		public virtual int KnotsLength {
			get { return knots.Length; }
		}

		/// <summary>
		/// Gets or sets the knots array.
		/// </summary>
		/// <value>The knots array.</value>
		public virtual float[] Knots {
			get {
				return knots;
			}
			set {
				knots = value;
			}
		}

		/// <summary>
		/// Sets the knot at the specified index.
		/// </summary>
		/// <param name="index">The index at which to set the knot.</param>
		/// <param name="knot">The knot to set.</param>
		public virtual void SetKnot(int index, float knot) {
			knots[index] = knot;
		}	
	
		/// <summary>
		/// Gets the knot at the specified index.
		/// </summary>
		/// <param name="index">The index of the desired knot.</param>
		/// <returns>The knot at the specified index.</returns>
		public virtual float GetKnot(int index) {
			return knots[index];
		}
		#endregion

		#region Path Interpolation
		/// <summary>
		/// Overridden.  See <see cref="PInterpolatingActivity.SetRelativeTargetValue">
		/// PInterpolatingActivity.SetRelativeTargetValue</see>.
		/// </summary>
		public override void SetRelativeTargetValue(float zeroToOne) {
			int currentKnotIndex = 0;

			while (zeroToOne > knots[currentKnotIndex]) {
				currentKnotIndex++;
			}

			int startKnot = currentKnotIndex - 1;
			int endKnot = currentKnotIndex;
		
			if (startKnot < 0) startKnot = 0;
			if (endKnot > KnotsLength - 1) endKnot = KnotsLength - 1;
		
			float currentRange = knots[endKnot] - knots[startKnot];
			float currentPointOnRange = zeroToOne - knots[startKnot];
			float normalizedPointOnRange = currentPointOnRange;
		
			if (currentRange != 0) {
				normalizedPointOnRange = currentPointOnRange / currentRange;
			}
		
			SetRelativeTargetValue(normalizedPointOnRange, startKnot, endKnot);
		}

		/// <summary>
		/// Subclasses should override this method and set the value on their target (the
		/// object that they are modifying) accordingly.
		/// </summary>
		/// <param name="zeroToOne">
		/// The current interpolation value (from 0 to 1) between the start knot and the end knot.
		/// </param>
		/// <param name="startKnot">The previous knot the activity is interpolating from.</param>
		/// <param name="endKnot">The next knot the activity is interpolating to.</param>
		public abstract void SetRelativeTargetValue(float zeroToOne, int startKnot, int endKnot);
		#endregion
	}
}