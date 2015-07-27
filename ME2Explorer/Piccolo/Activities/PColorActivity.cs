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
using System.Text;

namespace UMD.HCIL.Piccolo.Activities {
	/// <summary>
	/// <b>PColorActivity</b> interpolates between two colors for its target over the
	/// duration of the animation. 
	/// </summary>
	/// <remarks>
	/// The source color is retrieved from the target just before the activity is
	/// scheduled to start.
	/// </remarks>
	public class PColorActivity : PInterpolatingActivity {
		#region Fields
		private Color source;
		private Color destination;
		private Target target;
		#endregion

		#region Target Interface
		/// <summary>
		/// <b>Target</b> objects that want their color to be set by the color activity
		/// must implement this interface.
		/// </summary>
		public interface Target {
			/// <summary>
			/// Gets or sets the color of the target.
			/// </summary>
			Color Color {
				// This is called right before the color activity starts.  That way an
				// object's color is always animated from its current color the
				// destination color that is specified in the color activity.
				get;

				// This will be called by the color activity for each new
				// interpolated color that it computes while it is stepping.
				set;
			}
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PColorActivity that will animate from the source color
		/// to no color, unless the destination color is later set.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between
		/// steps.
		/// </param>
		/// <param name="aTarget">
		/// The object that the activity will be applied to and where the source state
		/// will be taken from.
		/// </param>
		public PColorActivity(long duration, long stepInterval, Target aTarget) :
			this(duration, stepInterval, aTarget, Color.Empty) {
		}

		/// <summary>
		/// Constructs a new PColorActivity that will animate from the source color
		/// to the destination color.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between
		/// steps.
		/// </param>
		/// <param name="aTarget">
		/// The object that the activity will be applied to and where the source state
		/// will be taken from.
		/// </param>
		/// <param name="aDestination">The destination color state.</param>
		public PColorActivity(long duration, long stepInterval, Target aTarget, Color aDestination) :
			this(duration, stepInterval, 1, ActivityMode.SourceToDestination, aTarget, aDestination) {
		}

		/// <summary>
		/// Constructs a new PColorActivity that animate between the source and destination
		/// colors in the order specified by the mode, looping the given number of iterations.
		/// </summary>
		/// <param name="duration">The length of one loop of the activity.</param>
		/// <param name="stepInterval">
		/// The minimum number of milliseconds that this activity should delay between steps.
		/// </param>
		/// <param name="loopCount">
		/// The number of times the activity should reschedule itself.
		/// </param>
		/// <param name="mode">
		/// Defines how the activity interpolates between states.
		/// </param>
		/// <param name="aTarget">
		/// The object that the activity will be applied to and where the source state
		/// will be taken from.
		/// </param>
		/// <param name="aDestination">The destination color state.</param>
		public PColorActivity(long duration, long stepInterval, int loopCount, ActivityMode mode, Target aTarget, Color aDestination) :
			base(duration, stepInterval, loopCount, mode) {
			target = aTarget;
			destination = aDestination;
		}	
		#endregion

		#region Basics
		/// <summary>
		/// Overridden.  Gets a value indicating whether this activity is performing an animation.
		/// </summary>
		/// <value>True if this activity is performing an animation; false otherwise.</value>
		/// <remarks>
		/// This property will always return true since a PColorActivity is an animating
		/// activity.
		/// <para>
		/// This is used by the PCanvas to determine if it should set the render quality to
		/// PCanvas.animatingRenderQuality or not for each frame it renders. 
		/// </para>
		///</remarks>
		public override bool IsAnimation {
			get { return true; }
		}

		/// <summary>
		/// Return the final color that will be set on the color activities target
		/// when the activity stops stepping.
		/// </summary>
		public virtual Color DestinationColor {
			get { return destination; }
			set { destination = value; }
		}
		#endregion

		#region Stepping
		/// <summary>
		/// Overridden.
		/// See <see cref="PInterpolatingActivity.OnActivityStarted">
		/// PInterpolatingActivity.OnActivityStarted</see>.
		/// </summary>
		protected override void OnActivityStarted() {
			if (FirstLoop) source = target.Color;
			base.OnActivityStarted ();
		}

		
		/// <summary>
		/// Overridden.
		/// See <see cref="PInterpolatingActivity.SetRelativeTargetValue">
		/// PInterpolatingActivity.SetRelativeTargetValue</see>.
		/// </summary>
		public override void SetRelativeTargetValue(float zeroToOne) {
			base.SetRelativeTargetValue (zeroToOne);
			int red = (int)Math.Round(source.R + (zeroToOne * (destination.R - source.R)));
			int green = (int)Math.Round(source.G + (zeroToOne * (destination.G - source.G)));
			int blue = (int)Math.Round(source.B + (zeroToOne * (destination.B - source.B)));
			int alpha = (int)Math.Round(source.A + (zeroToOne * (destination.A - source.A)));
			target.Color = Color.FromArgb(alpha, red, green, blue);
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
		protected override String ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("source=" + source.ToString());
				result.Append(",destination=" + destination.ToString());
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}
