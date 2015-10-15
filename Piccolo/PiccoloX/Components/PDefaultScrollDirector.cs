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
using System.Collections;
using System.Drawing;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Components {
	/// <summary>
	/// <b>PDefaultScrollDirector</b> is the default scroll director implementation.
	/// </summary>
	/// <remarks>
	/// This default implementation follows the widely accepted model of scrolling - namely
	/// the scrollbars control the movement of the window over the document rather than the
	/// movement of the document under the window.
	/// </remarks>
	public class PDefaultScrollDirector : PScrollDirector {
		#region Fields
		/// <summary>
		/// The scrollablecontrol that signals the scrolldirector.
		/// </summary>
		protected PScrollableControl scrollableControl;

		/// <summary>
		/// The canvas that this class directs.
		/// </summary>
		protected PCanvas view;

		/// <summary>
		/// The canvas' camera.
		/// </summary>
		protected PCamera camera;

		/// <summary>
		/// The canvas' root. 
		/// </summary>
		protected PRoot root;

		/// <summary>
		/// Flag to indicate when scrolling is currently in progress. 
		/// </summary>
		protected bool scrollInProgress = false;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PDefaultScrollDirector.
		/// </summary>
		public PDefaultScrollDirector() {
		}
		#endregion

		#region Installation
		/// <summary>
		/// Installs the scroll director and adds the appropriate handlers.
		/// </summary>
		/// <param name="scrollableControl">
		/// The scrollable control on which this director directs.
		/// </param>
		/// <param name="view">The PCanvas that the scrollable control scrolls.</param>
		public virtual void Install(PScrollableControl scrollableControl, PCanvas view) {
			this.scrollableControl = scrollableControl;
			this.view = view;

			if (view != null) {
				this.camera = view.Camera;
				this.root = view.Root;
			}

			if (camera != null) {
				camera.ViewTransformChanged += new PPropertyEventHandler(camera_ViewTransformChanged);
				camera.BoundsChanged += new PPropertyEventHandler(camera_BoundsChanged);
				camera.FullBoundsChanged += new PPropertyEventHandler(camera_FullBoundsChanged);
			}
			if (root != null) {
				root.BoundsChanged += new PPropertyEventHandler(root_BoundsChanged);
				root.FullBoundsChanged += new PPropertyEventHandler(root_FullBoundsChanged);
			}

			if (scrollableControl != null) {
				scrollableControl.UpdateScrollbars();
			}
		}

		/// <summary>
		/// Uninstalls the scroll director from the scrollable control.
		/// </summary>
		public virtual void UnInstall() {
			scrollableControl = null;
			view = null;

			if (camera != null) {
				camera.ViewTransformChanged -= new PPropertyEventHandler(camera_ViewTransformChanged);
				camera.BoundsChanged -= new PPropertyEventHandler(camera_BoundsChanged);
				camera.FullBoundsChanged -= new PPropertyEventHandler(camera_FullBoundsChanged);
			}
			if (root != null) {
				root.BoundsChanged -= new PPropertyEventHandler(root_BoundsChanged);
				root.FullBoundsChanged -= new PPropertyEventHandler(root_FullBoundsChanged);
			}

			camera = null;
			root = null;
		}
		#endregion

		#region View Port
		/// <summary>
		/// Gets the view position given the specified camera bounds.
		/// </summary>
		/// <param name="bounds">
		/// The bounds for which the view position will be computed.
		/// </param>
		/// <returns>The view position.</returns>
		public virtual Point GetViewPosition(RectangleF bounds) {
			Point pos = Point.Empty;
			if (camera != null) {
				// First we compute the union of all the layers
				RectangleF layerBounds = UnionOfLayerFullBounds;

				// Then we put the bounds into camera coordinates and
				// union the camera bounds
				if (!layerBounds.IsEmpty) {
					layerBounds = camera.ViewToLocal(layerBounds);
				}
				layerBounds = RectangleF.Union(layerBounds, bounds);

				pos = new Point((int) (bounds.X - layerBounds.X + 0.5), (int) (bounds.Y - layerBounds.Y + 0.5));
			}

			return pos;
		}

		/// <summary>
		/// Sets the view position in a manner consistent with standardized scrolling.
		/// </summary>
		/// <param name="x">The x coordinate of the new position.</param>
		/// <param name="y">The y coordinate of the new position.</param>
		public virtual void SetViewPosition(float x, float y) {
			if (camera != null) {

				// Only process this scroll if a scroll is not already in progress;
				// otherwise, we could end up with an infinite loop, since the
				// scrollbars depend on the camera location.
				if (!scrollInProgress) {
					scrollInProgress = true;

					// Get the union of all the layers' bounds
					RectangleF layerBounds = UnionOfLayerFullBounds;
	
					PMatrix matrix = camera.ViewMatrix;
					layerBounds = matrix.Transform(layerBounds);

					// Union the camera bounds
					RectangleF viewBounds = camera.Bounds;
					layerBounds = RectangleF.Union(layerBounds, viewBounds);

					// Now find the new view position in view coordinates
					PointF newPoint = new PointF(layerBounds.X + x, layerBounds.Y + y);

					// Now transform the new view position into global coords
					newPoint = camera.LocalToView(newPoint);

					// Compute the new matrix values to put the camera at the
					// correct location
					float[] elements = matrix.Elements;
					elements[4] = - (elements[0] * newPoint.X + elements[1] * newPoint.Y);
					elements[5] = - (elements[2] * newPoint.X + elements[3] * newPoint.Y);

					matrix = new PMatrix(elements[0], elements[1], elements[2], elements[3], elements[4], elements[5]);

					// Now actually set the camera's transform
					camera.ViewMatrix = matrix;
					scrollInProgress = false;
				}
			}
		}

		/// <summary>
		/// Gets the size of the view based on the specified camera bounds.
		/// </summary>
		/// <param name="bounds">The view bounds for which the view size will be computed.</param>
		/// <returns>The view size.</returns>
		public virtual Size GetViewSize(RectangleF bounds) {
			Size size = Size.Empty;
			if (camera != null) {
				// First we compute the union of all the layers
				RectangleF layerBounds = UnionOfLayerFullBounds;

				// Then we put the bounds into camera coordinates and
				// union the camera bounds		
				if (!layerBounds.IsEmpty) {
					layerBounds = camera.ViewToLocal(layerBounds);
				}
				layerBounds = RectangleF.Union(layerBounds, bounds);

				size = new Size((int) (layerBounds.Width + 0.5), (int) (layerBounds.Height + 0.5));
			}

			return size;
		}

		/// <summary>
		/// Gets the the total bounds of all the layers.
		/// </summary>
		/// <remarks>
		/// Subclasses can override this method to add a margin.
		/// </remarks>
		/// <value>The total bounds of all the layers.</value>
		protected virtual RectangleF UnionOfLayerFullBounds {
			get {
				return camera.UnionOfLayerFullBounds;
			}
		}
		#endregion

		#region Update ScrollableControl
		/// <summary>
		/// Invoked when the camera's view changes.
		/// </summary>
		/// <param name="sender">The source of the property changed event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void camera_ViewTransformChanged(object sender, PPropertyEventArgs e) {
			scrollableControl.UpdateScrollbars();
		}

		/// <summary>
		/// Invoked when the bounds of the camera changes.
		/// </summary>
		/// <param name="sender">The source of the property changed event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void camera_BoundsChanged(object sender, PPropertyEventArgs e) {
			scrollableControl.UpdateScrollbars();
		}

		/// <summary>
		/// Invoked when the full bounds of the camera changes.
		/// </summary>
		/// <param name="sender">The source of the property changed event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void camera_FullBoundsChanged(object sender, PPropertyEventArgs e) {
			scrollableControl.UpdateScrollbars();
		}

		/// <summary>
		/// Invoked when the bounds of the root changes.
		/// </summary>
		/// <param name="sender">The source of the property changed event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void root_BoundsChanged(object sender, PPropertyEventArgs e) {
			scrollableControl.UpdateScrollbars();
		}

		/// <summary>
		/// Invoked when the full bounds of the root changes.
		/// </summary>
		/// <param name="sender">The source of the property changed event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void root_FullBoundsChanged(object sender, PPropertyEventArgs e) {
			scrollableControl.UpdateScrollbars();
		}
		#endregion
	}
}