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
using System.Drawing.Drawing2D;

using UMD.HCIL.Piccolo.Activities;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Activities {
	/// <summary>
	/// <b>PPositionPathActivity</b> animates through a sequence of points.
	/// </summary>
	public class PPositionPathActivity : PPathActivity {
		#region Fields
		/// <summary>
		/// The sequence of points that this activity animates through.
		/// </summary>
		protected PointF[] positions;

		/// <summary>
		/// The target that will be positioned as the activity runs.
		/// </summary>
		protected Target target;
		#endregion

		#region Target
		/// <summary>
		/// <b>Target</b> objects that want their position to be set by the position path
		/// activity must implement this interface.
		/// </summary>
		public interface Target {
			/// <summary>
			/// This will be called by the position path activity for each new
			/// interpolated position that it computes while it is stepping.
			/// </summary>
			/// <param name="x">The x coordinate of the new position.</param>
			/// <param name="y">The y coordinate of the new position.</param>
			void SetPosition(float x, float y);
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPositionPathActivity that will move the target through multiple
		/// positions (from source to destination), transitioning from position to position
		/// as specified by the knot values.
		/// </summary>
		/// <param name="duration">The length of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="aTarget">The object that the activity will be applied to.</param>
		public PPositionPathActivity(long duration, long stepInterval, Target aTarget)
			: this(duration, stepInterval, aTarget, null, null) {
		}

		/// <summary>
		/// Constructs a new PPositionPathActivity that will move the target through the given
		/// positions (from source to destination), transitioning from position to position
		/// as specified by the given knot values.
		/// </summary>
		/// <param name="duration">The length of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="aTarget">The object that the activity will be applied to.</param>
		/// <param name="knots">
		/// An array of values between 0 and 1 that indicate when the position transitions should
		/// occur.
		/// </param>
		/// <param name="positions">
		/// The sequence of points that this activity animates through.
		/// </param>
		public PPositionPathActivity(long duration, long stepInterval, Target aTarget, float[] knots, PointF[] positions)
			: this(duration, stepInterval, 1, ActivityMode.SourceToDestination, aTarget, knots, positions) {
		}

		/// <summary>
		/// Constructs a new PPositionPathActivity that will move the target through the given
		/// positions in the order specified by the mode, transitioning from position to position
		/// as specified by the given knot values and looping the given number of iterations.
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
		/// <param name="aTarget">The object that the activity will be applied to.</param>
		/// <param name="knots">
		/// An array of values between 0 and 1 that indicate when the position transitions should
		/// occur.
		/// </param>
		/// <param name="positions">
		/// The sequence of points that this activity animates through.
		/// </param>
		public PPositionPathActivity(long duration, long stepInterval, int loopCount, ActivityMode mode, Target aTarget, float[] knots, PointF[] positions)
			: base(duration, stepInterval, loopCount, mode, knots) {
			target = aTarget;
			this.positions = positions;
		}
		#endregion

		#region Basics
		/// <summary>
		/// Overridden.  See <see cref="PActivity.IsAnimation">PActivity.IsAnimation</see>.
		/// </summary>
		public override bool IsAnimation {
			get {
				return true;
			}
		}
		#endregion

		#region Positions
		/// <summary>
		/// Gets or sets the sequence of points that this activity animates through.
		/// </summary>
		/// <value>The sequence of points this activity animates through.</value>
		public virtual PointF[] Positions {
			get { return positions; }
			set { positions = value; }
		}

		/// <summary>
		/// Sets the sequence of points that this activity animates through to the points along
		/// the given graphics path.
		/// </summary>
		/// <value>
		/// A graphics path whose points will be used to set the positions that this activity
		/// animates through.
		/// </value>
		public virtual GraphicsPath PositionPath {
			set {
				value.Flatten();
				PointF[] pathPoints = value.PathPoints;
				int length = pathPoints.Length+1;
				PointF[] points = new PointF[length];
				float[] knots = new float[length];

				float distanceSum = 0;
				for (int i = 0; i < length; i++) {
					if (i == (length - 1)) {
						points[i] = pathPoints[0];
					}
					else {
						points[i] = pathPoints[i];
					}

					if (i > 0) {
						distanceSum += PUtil.DistanceBetweenPoints(points[i - 1], points[i]);
					}
				}

				for (int i = 0; i < length; i++) {
					if (i > 0) {
						if (i < (length-1)) {
							float dist = PUtil.DistanceBetweenPoints(points[i - 1], points[i]);
							knots[i] = knots[i - 1] + (dist / distanceSum);							
						} else {
							knots[i] = 1;
						}
					}
				}

				Positions = points;
				Knots = knots;
			}
		}

		/// <summary>
		/// Gets the position at the specified index.
		/// </summary>
		/// <param name="index">The index of the desired position.</param>
		/// <returns>The position at the specified index.</returns>
		public virtual PointF GetPosition(int index) {
			return positions[index];
		}

		/// <summary>
		/// Sets the position at the specified index.
		/// </summary>
		/// <param name="index">The index at which to set the position.</param>
		/// <param name="position">The new position to set.</param>
		public virtual void SetPosition(int index, PointF position) {
			positions[index] = position;
		}
		#endregion

		#region Path Interpolation
		/// <summary>
		/// Overridden.  See <see cref="PPathActivity.SetRelativeTargetValue(float,int,int)">
		/// PPathActivity.SetRelativeTargetValue</see>.
		/// </summary>
		public override void SetRelativeTargetValue(float zeroToOne, int startKnot, int endKnot) {
			PointF start = GetPosition(startKnot);
			PointF end = GetPosition(endKnot);
			target.SetPosition(start.X + (zeroToOne * (end.X - start.X)),
				start.Y + (zeroToOne * (end.Y - start.Y)));
		}
		#endregion
	}
}