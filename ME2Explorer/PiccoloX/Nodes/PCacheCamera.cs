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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.Piccolo.Activities;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// <b>PCacheCamera</b> is an extension to PCamera that provides a fast
	/// image based animationToCenterBounds method.
	/// </summary>
	/// <remarks>
	/// Java code contributed by Lance Good and ported by Aaron Clamage.
	/// </remarks>
	public class PCacheCamera : PCamera {
		private Image paintBuffer;
		private bool imageAnimate;
		private RectangleF imageAnimateBounds;
	
		/// <summary>
		/// Get the buffer used to provide fast image based animation.
		/// </summary>
		public Image PaintBuffer {
			get {
				RectangleF fRef = FullBounds;
				if (paintBuffer == null || paintBuffer.Width < fRef.Width || paintBuffer.Height < fRef.Height) {
					paintBuffer = new Bitmap((int)Math.Ceiling(fRef.Width),(int)Math.Ceiling(fRef.Height));
				}
				return paintBuffer;
			}
		}

		/// <summary>
		/// Caches the information necessary to animate from the current view bounds to the
		/// specified centerBounds
		/// </summary>
		/// <param name="centerBounds">The bounds to center the view on.</param>
		/// <param name="scaleToFit">
		/// Indicates whether the camera should scale it's view when necessary to fully fit
		/// the given bounds within the camera's view bounds.
		/// </param>
		/// <returns>The new view matrix to center the specified bounds.</returns>
		private PMatrix CacheViewBounds(RectangleF centerBounds, bool scaleToFit) {
			RectangleF viewBounds = ViewBounds;
	    
			// Initialize the image to the union of the current and destination bounds
			RectangleF imageBounds = viewBounds;
			imageBounds = RectangleF.Union(imageBounds, centerBounds);

			AnimateViewToCenterBounds(imageBounds,scaleToFit,0);

			imageAnimateBounds = ViewBounds;

			// Now create the actual cache image that we will use to animate fast 
        
			Image buffer = PaintBuffer;
			Brush fBrush = Brushes.White;
			if (Brush != null) {
				fBrush = Brush;
			}
			ToImage(buffer,fBrush);

			// Do this after the painting above!
			imageAnimate = true;
		
			// Return the bounds to the previous viewbounds
			AnimateViewToCenterBounds(viewBounds,scaleToFit,0);
		
			// The code below is just copied from animateViewToCenterBounds to create the
			// correct transform to center the specified bounds
		
			SizeF delta = PUtil.DeltaRequiredToCenter(viewBounds, centerBounds);
			PMatrix newMatrix = ViewMatrix;
			newMatrix.TranslateBy(delta.Width, delta.Height);

			if (scaleToFit) {
				float s = Math.Min(viewBounds.Width / centerBounds.Width, viewBounds.Height / centerBounds.Height);
				PointF center = PUtil.CenterOfRectangle(centerBounds);
				newMatrix.ScaleBy(s, center.X, center.Y);
			}
		
			return newMatrix;
		}

		/// <summary>
		/// Turns off the fast image animation and does any other applicable cleanup 
		/// </summary>
		private void ClearViewCache() {
			imageAnimate = false;
			imageAnimateBounds = RectangleF.Empty;
		}


		/// <summary>
		///  Mimics the standard <see cref="PCamera.AnimateViewToCenterBounds">AnimateViewToCenterBounds</see>
		///  but uses a cached image for performance rather than re-rendering the scene at each step 
		/// </summary>
		/// <param name="centerBounds">The bounds to center the view on.</param>
		/// <param name="shouldScaleToFit">
		/// Indicates whether the camera should scale it's view when necessary to fully fit
		/// the given bounds within the camera's view bounds.
		/// </param>
		/// <param name="duration">The amount of time that the animation should take.</param>
		/// <returns>
		/// The newly scheduled activity, if the duration is greater than 0; else null.
		/// </returns>
		public PTransformActivity AnimateStaticViewToCenterBoundsFast(RectangleF centerBounds, bool shouldScaleToFit, long duration) {
			if (duration == 0) {
				return AnimateViewToCenterBounds(centerBounds,shouldScaleToFit,duration);	        
			}
	    
			PMatrix newViewMatrix = CacheViewBounds(centerBounds,shouldScaleToFit);

			return AnimateStaticViewToTransformFast(newViewMatrix, duration);
		}

		/// <summary>
		/// This copies the behavior of the standard
		/// <see cref="PCamera.AnimateViewToMatrix">AnimateViewToMatrix</see> but clears the cache
		/// when it is done.
		/// </summary>
		/// <param name="destination">The final matrix value.</param>
		/// <param name="duration">The amount of time that the animation should take.</param>
		/// <returns>
		/// The newly scheduled activity, if the duration is greater than 0; else null.
		/// </returns>
		protected PTransformActivity AnimateStaticViewToTransformFast(PMatrix destination, long duration) {
			if (duration == 0) {
				this.ViewMatrix = destination;
				return null;
			}

			PTransformActivity ta = new FastTransformActivity(this, duration, PUtil.DEFAULT_ACTIVITY_STEP_RATE, new PCamera.PCameraTransformTarget(this), destination);
		
			PRoot r = Root;
			if (r != null) {
				r.ActivityScheduler.AddActivity(ta);
			}
	
			return ta;
		}

		/// <summary>
		/// A transform activity that does fast rendering when possible.
		/// </summary>
		class FastTransformActivity : PTransformActivity {
			PCacheCamera target;
			public FastTransformActivity(PCacheCamera target, long duration, long stepInterval, Target aTarget, PMatrix aDestination) :
				base(duration, stepInterval, 1, ActivityMode.SourceToDestination, aTarget, aDestination) {
				this.target = target;
			}
			protected override void OnActivityFinished() {
				target.ClearViewCache();
				target.Repaint();
				base.OnActivityFinished();
			}
		}

		/// <summary>
		/// Overridden.  Does fast rendering when possible.
		/// </summary>
		public override void FullPaint(PPaintContext paintContext) {
			if (imageAnimate) {
				RectangleF fRef = FullBounds;
				RectangleF viewBounds = ViewBounds;
				float scale = FullBounds.Width/imageAnimateBounds.Width;
				float xOffset = (viewBounds.X-imageAnimateBounds.X)*scale;
				float yOffset = (viewBounds.Y-imageAnimateBounds.Y)*scale;
				float scaleW = viewBounds.Width*scale;
				float scaleH = viewBounds.Height*scale;

				RectangleF destRect = new RectangleF(0,0,(int)Math.Ceiling(fRef.Width),(int)Math.Ceiling(fRef.Height));
				RectangleF srcRect = new RectangleF((int)Math.Floor(xOffset),(int)Math.Floor(yOffset),(int)Math.Ceiling(xOffset+scaleW),(int)Math.Ceiling(yOffset+scaleH));
				paintContext.Graphics.DrawImage(paintBuffer, destRect, srcRect, GraphicsUnit.Pixel);
			}
			else {
				base.FullPaint(paintContext);
			}			
		}
	}
}