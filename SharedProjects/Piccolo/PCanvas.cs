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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo {
	#region Render Quality Delegates
	/// <summary>
	/// A delegate that is notified when Piccolo renders in low quality.
	/// </summary>
	/// <remarks>
	/// This delegate will be called whenever Piccolo renders in low quality so that you
	/// can set additional flags on the graphics object.  Piccolo will set various
	/// flags for you by default.
	/// </remarks>
	public delegate void LowRenderQualityDelegate(Graphics graphics);

	/// <summary>
	/// A delegate that is notified when Piccolo renders in high quality.
	/// </summary>
	/// <remarks>
	/// This delegate will be called whenever Piccolo renders in high quality so that you
	/// can set additional flags on the graphics object.  Piccolo will set various
	/// flags for you by default.
	/// </remarks>
	public delegate void HighRenderQualityDelegate(Graphics graphics);
	#endregion

	/// <summary>
	/// <b>PCanvas</b> is a simple C# Control that can be used to embed Piccolo into a
	/// C# application.
	/// </summary>
	/// <remarks>
	/// Canvases view the Piccolo scene graph through a camera.  The canvas manages
	/// screen updates coming from this camera, and forwards mouse and keyboard events
	/// to the camera.
	/// </remarks>
	public class PCanvas : Control {
		#region Fields

        /// <summary>
		/// Used to specify what flags should be set for low quality rendering.
		/// </summary>
		public LowRenderQualityDelegate LowRenderQuality;

		/// <summary>
		/// Used to specify what flags should be set for high quality rendering.
		/// </summary>
		public HighRenderQualityDelegate HighRenderQuality;

		private RectangleF invalidatedBounds = RectangleF.Empty;
		public bool IsInvalidated { get; private set; }
		private PCamera camera;
		private Stack<Cursor> cursorStack;
		private int interacting;
		private RenderQuality defaultRenderQuality;
		private RenderQuality animatingRenderQuality;
		private RenderQuality interactingRenderQuality;
		private PPanEventHandler panEventHandler;
		private PZoomEventHandler zoomEventHandler;
		private bool paintingImmediately;
		private bool animatingOnLastPaint;
		private bool regionManagement;

        /// <summary>
		/// Occurs when the interacting state of a canvas changes.
		/// </summary>
		/// <remarks>
		/// When a canvas is interacting, the canvas will render at lower quality that is
		/// faster.
		/// </remarks>
		public event PPropertyEventHandler InteractingChanged;

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private Container components = null;
		#endregion

		#region Constructors
		/// <summary>
		/// Construct a canvas with the basic scene graph consisting of a root, camera,
		/// and layer. Event handlers for zooming and panning are automatically
		/// installed.
		/// </summary>
		public PCanvas() {
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

            cursorStack = new Stack<Cursor>();
			Camera = CreateBasicScenegraph();
			DefaultRenderQuality = RenderQuality.HighQuality;
			AnimatingRenderQuality = RenderQuality.LowQuality;
			InteractingRenderQuality = RenderQuality.LowQuality;
			PanEventHandler = new PPanEventHandler();
			ZoomEventHandler = new PZoomEventHandler();
			BackColor = Color.White;
			
			AllowDrop = true;

			RegionManagement = true;

			SetStyle(ControlStyles.DoubleBuffer, true);
			SetStyle(ControlStyles.Selectable, true);
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}
		#endregion

		#region Scene Graph
		//****************************************************************
		// Scene Graph - Methods for constructing the scene graph.
		//****************************************************************

		/// <summary>
		/// Override this method to modify the way the scene graph is created.
		/// </summary>
		/// <returns>The main camera node in the new scene graph.</returns>
		protected virtual PCamera CreateBasicScenegraph() {
			return PUtil.CreateBasicScenegraph();
		}
		#endregion

		#region Basics
		//****************************************************************
		// Basics - Methods for accessing common piccolo nodes.
		//****************************************************************

		/// <summary>
		/// Gets or sets the camera associated with this canvas.
		/// </summary>
		/// <value>The camera associated with this canvas.</value>
		/// <remarks>
		/// All input events from this canvas go through this camera. And this is the
		/// camera that paints this canvas.
		/// </remarks>
		[Category("Scene Graph")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual PCamera Camera {
			get => camera;
            set {
				if (camera != null) { camera.Canvas = null; }
				camera = value;
				if (camera != null) {
					camera.Canvas = this;
					camera.Bounds = Bounds;
				}
			}
		}

		/// <summary>
		/// Gets the root of the scene graph viewed by the camera.
		/// </summary>
		/// <value>The root for this canvas.</value>
		[Category("Scene Graph")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual PRoot Root => camera.Root;

        /// <summary>
		/// Gets the main layer of the scene graph viewed by the camera.
		/// </summary>
		/// <value>The layer for this canvas.</value>
		[Category("Scene Graph")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public PLayer Layer => camera.GetLayer(0);

        /// <summary>
		/// Gets or sets the pan event handler associated with this canvas.
		/// </summary>
		/// <value>The pan event handler for this canvas.</value>
		/// <remarks>
		/// This event handler is set up to get events from the camera associated
		/// with this canvas by default.
		/// </remarks>
		[Category("PInputEvent Handlers")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual PPanEventHandler PanEventHandler {
			get => panEventHandler;
            set {
				if (panEventHandler != null) {
					RemoveInputEventListener(panEventHandler);
				}
		
				panEventHandler = value;
		
				if (panEventHandler != null) {
					AddInputEventListener(panEventHandler);
				}
			}
		}

		/// <summary>
		/// Gets or sets the zoom event handler associated with this canvas.
		/// </summary>
		/// <value>The zoom event handler for this canvas.</value>
		/// <remarks>
		/// This event handler is set up to get events from the camera associated
		/// with this canvas by default.
		/// </remarks>
		[Category("PInputEvent Handlers")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual PZoomEventHandler ZoomEventHandler {
			get => zoomEventHandler;
            set {
				if(zoomEventHandler != null) {
					RemoveInputEventListener(zoomEventHandler);
				}
		
				zoomEventHandler = value;
		
				if(zoomEventHandler != null) {
					AddInputEventListener(zoomEventHandler);
				}
			}
		}

		/// <summary>
		/// Add an input listener to the camera associated with this canvas.
		/// </summary>
		/// <param name="listener">The listener to add.</param>
		public virtual void AddInputEventListener(PInputEventListener listener) {
			Camera.AddInputEventListener(listener);
		}
	
		/// <summary>
		/// Remove an input listener to the camera associated with this canvas.
		/// </summary>
		/// <param name="listener">The listener to remove.</param>
		public virtual void RemoveInputEventListener(PInputEventListener listener) {
			Camera.RemoveInputEventListener(listener);
		}
		#endregion

		#region Painting
		//****************************************************************
		// Painting - Methods for painting the camera and it's view on the
		// canvas.
		//****************************************************************

		/// <summary>
		/// Gets or sets a value indicating whether or not region management should be
		/// used when painting.
		/// </summary>
		/// <value>
		/// A value that indicates whether or not region management should be used when
		/// painting.
		/// </value>
		/// <remarks>
		/// When region management is turned on, only the nodes that have been
		/// invalidated will be painted to the screen.  Typically, this will provide the
		/// best performance.  In some cases, however, the cost of invalidating lots of
		/// rectangles is greater than the cost of painting the entire window.  For
		/// example, if you have a scene filled with lots of animating rectangles where
		/// the bounds of each rectangle are invalidated on every frame, you might get
		/// better performance by turning off region management.
		/// </remarks>
		[Category("Appearance")]
		public virtual bool RegionManagement {
			get => regionManagement;
            set => regionManagement = value;
        }

		/// <summary>
		/// Gets or sets a value indicating if text should be rendered with hinting.
		/// </summary>
		/// <remarks>
		/// When grid-fitting is turned on, the position of the pixels in each rendered glyph
		/// are adjusted to make the glyph easily legible at various screen sizes.  This will
		/// result in higher quality text.  However, grid-fitting will also change the size of
		/// the glyphs, which can make it difficult to display adjacent text.  And, while
		/// zooming, the text may appear jumpy.
		/// </remarks>
		[Category("Appearance")]
		public bool GridFitText { get; set; }

        /// <summary>
		/// Gets or sets a value indicating if this canvas is interacting.
		/// </summary>
		/// <value>True if the canvas is interacting; otherwise, false.</value>
		/// <remarks>
		/// If this property is true, the canvas will normally render at a lower
		/// quality that is faster.
		/// </remarks>
		[Category("Appearance")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual bool Interacting {
			get => interacting > 0;
            set {
				bool wasInteracting = Interacting;

				if (value) {
					interacting++;
				} else {
					interacting--;
				}

				if (!Interacting) { // determine next render quality and repaint if it's greater then the old
					// interacting render quality.
					RenderQuality nextRenderQuality = defaultRenderQuality;
					if (Animating) nextRenderQuality = animatingRenderQuality;
					if (nextRenderQuality > interactingRenderQuality) {
						Invalidate();
					}
				}

				// If interacting changed, fire the appropriate event.
				bool isInteracting = Interacting;
				if (wasInteracting != isInteracting) {
					OnInteractingChanged(new PPropertyEventArgs(wasInteracting, isInteracting));
				}
			}
		}

		/// <summary>
		/// Raises the InteractingChanged event by invoking the delegates.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		/// <remarks>
		/// This event is raised when the interacting state of the canvas changes.
		/// </remarks>
		protected virtual void OnInteractingChanged(PPropertyEventArgs e)
        {
            //Invokes the delegates.
            InteractingChanged?.Invoke(this, e);
        }

		/// <summary>
		/// Gets a value indicating if this canvas is animating.
		/// </summary>
		/// <value>True if the canvas is animating; otherwise, false.</value>
		/// <remarks>
		/// Returns true if any activities that respond with true to the method isAnimating
		/// were run in the last PRoot.ProcessInputs() loop. 
		/// </remarks>
		[Category("Appearance")]
		public virtual bool Animating => Root.ActivityScheduler.Animating;

        /// <summary>
		/// Sets the render quality that should be used for rendering this canvas when it
		/// is not interacting or animating.
		/// </summary>
		/// <value>The default render quality for this canvas.</value>
		/// <remarks>The default value is <c>RenderQuality.HighQuality</c>.</remarks>
		public virtual RenderQuality DefaultRenderQuality {
			set {
				defaultRenderQuality = value;
				Invalidate();
			}
		}

		/// <summary>
		/// Sets the render quality that should be used for rendering this canvas when it
		/// is animating.
		/// </summary>
		/// <value>The animating render quality for this canvas.</value>
		/// <remarks>The default value is <c>RenderQuality.LowQuality</c>.</remarks>
		public virtual RenderQuality AnimatingRenderQuality {
			set {
				animatingRenderQuality = value;
				if (Animating) Invalidate();
			}
		}

		/// <summary>
		/// Set the render quality that should be used for rendering this canvas when it
		/// is interacting.
		/// </summary>
		/// <value>The interacting render quality for this canvas.</value>
		/// <remarks>The default value is <c>RenderQuality.LowQuality</c>.</remarks>
		public virtual RenderQuality InteractingRenderQuality {
			set {
				interactingRenderQuality = value;
				if (Interacting) Invalidate();
			}
		}

		/// <summary>
		/// Set the canvas cursor, and remember the previous cursor on the cursor stack.
		/// </summary>
		/// <param name="cursor">The new canvas cursor.</param>
		public virtual void PushCursor(Cursor cursor) {
			cursorStack.Push(Cursor);
			Cursor = cursor;
		}
	
		/// <summary>
		/// Pop the cursor on top of the cursorStack and set it as the canvas cursor.
		/// </summary>
		public virtual void PopCursor() {
			Cursor = cursorStack.Pop();
		}	
		#endregion

		#region Dispose
		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing ) {
			if( disposing )
            {
                components?.Dispose();
            }
			base.Dispose( disposing );
		}
		#endregion

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			// 
			// PCanvas
			// 
			this.Text = "8";
		}
		#endregion

		#region Windows Forms Connection
		//****************************************************************
		// Windows Forms Connection - Code to manage connection to windows
		// events.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="Control.OnResize">Control.OnResize</see>.
		/// </summary>
		protected override void OnResize(EventArgs e) {
			base.OnResize (e);
			camera.Bounds = new RectangleF(camera.X, camera.Y, Bounds.Width, Bounds.Height);
		}

		/// <summary>
		/// Overridden.  Forwards the KeyDown event to the default input manager.
		/// </summary>
		/// <param name="e">A KeyEventArgs that contains the event data.</param>
		protected override void OnKeyDown(KeyEventArgs e) {
			base.OnKeyDown (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.KeyDown, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the KeyPress event to the default input manager.
		/// </summary>
		/// <param name="e">A KeyPressEventArgs that contains the event data.</param>
		protected override void OnKeyPress(KeyPressEventArgs e) {
			base.OnKeyPress (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.KeyPress, Camera, null);
		}
		
		/// <summary>
		/// Overridden.  Determines whether the specified key is a regular input key or a
		/// special key that requires preprocessing.
		/// </summary>
		/// <param name="keyData">One of the Keys values.</param>
		/// <returns>True if the specified key is a regular input key; otherwise, false.</returns>
		/// <remarks>
		/// This method is overridden so that events from the arrow keys will be sent to the
		/// control, rather than pre-processed.
		/// </remarks>
		protected override bool IsInputKey(Keys keyData) {
			bool ret = true;

			switch (keyData) {
				case Keys.Left:
					break;
				case Keys.Right:
					break;
				case Keys.Up:
					break;
				case Keys.Down:
					break;
				default:
					ret = base.IsInputKey(keyData);
					break;
			}
			return ret;
		}

		/// <summary>
		/// Overridden.  Forwards the KeyUp event to the default input manager.
		/// </summary>
		/// <param name="e">A KeyEventArgs that contains the event data.</param>
		protected override void OnKeyUp(KeyEventArgs e) {
			base.OnKeyUp (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.KeyUp, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the Click event to the default input manager.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnClick(EventArgs e) {
			base.OnClick (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.Click, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the DoubleClick event to the default input manager.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnDoubleClick(EventArgs e) {
			base.OnDoubleClick (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.DoubleClick, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the MouseDown event to the default input manager.
		/// </summary>
		/// <param name="e">A MouseEventArgs that contains the event data.</param>
		protected override void OnMouseDown(MouseEventArgs e) {
			base.OnMouseDown (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.MouseDown, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the MouseMove event to the default input manager.
		/// </summary>
		/// <param name="e">A MouseEventArgs that contains the event data.</param>
		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove (e);

			var prevPos = new Point((int)Root.DefaultInputManager.CurrentCanvasPosition.X, (int)Root.DefaultInputManager.CurrentCanvasPosition.Y);
			var currPos = new Point(e.X, e.Y);

			// This condition is here because of a .NET bug that sometimes MouseMove events are generated
			// when the mouse has not actually moved (for example with context menus).
			if (prevPos != currPos)
            {
                Root.DefaultInputManager.ProcessEventFromCamera(e, e.Button == MouseButtons.None ? PInputType.MouseMove : PInputType.MouseDrag, Camera, null);
            }
		}

		/// <summary>
		/// Overridden.  Forwards the MouseUp event to the default input manager.
		/// </summary>
		/// <param name="e">A MouseEventArgs that contains the event data.</param>
		protected override void OnMouseUp(MouseEventArgs e) {
			base.OnMouseUp (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.MouseUp, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the MouseEnter event to the default input manager.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnMouseEnter(EventArgs e) {
			base.OnMouseEnter (e);
			SimulateMouseMoveOrDrag();
		}

		/// <summary>
		/// Overridden.  Forwards the MouseLeave event to the default input manager.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnMouseLeave(EventArgs e) {
			SimulateMouseMoveOrDrag();
			base.OnMouseLeave (e);
		}

		/// <summary>
		/// Simulates a mouse move or drag event.
		/// </summary>
		/// <remarks>
		/// This method simulates a mouse move or drag event, which is sometimes necessary
		/// to ensure that the appropriate piccolo mouse enter and leave events are fired.
		/// </remarks>
		protected virtual void SimulateMouseMoveOrDrag() {
			MouseEventArgs simulated = null;

			Point currPos = PointToClient(MousePosition);
			simulated = new MouseEventArgs(MouseButtons, 0, currPos.X, currPos.Y, 0);

			if (MouseButtons != MouseButtons.None) {
				Root.DefaultInputManager.ProcessEventFromCamera(simulated, PInputType.MouseDrag, Camera, null);
			}
			else {
				Root.DefaultInputManager.ProcessEventFromCamera(simulated, PInputType.MouseMove, Camera, null);
			}
		}
	
		/// <summary>
		/// Overridden.  Forwards the MouseWheel event to the default input manager.
		/// </summary>
		/// <param name="e">A MouseEventArgs that contains the event data.</param>
		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel (e);
			Root.DefaultInputManager.ProcessEventFromCamera(e, PInputType.MouseWheel, Camera, null);
		}

		/// <summary>
		/// Overridden.  Forwards the DragDrop event to the default input manager.
		/// </summary>
		/// <param name="drgevent">A DragEventArgs that contains the event data.</param>
		protected override void OnDragDrop(DragEventArgs drgevent) {
			base.OnDragDrop (drgevent);
			Root.DefaultInputManager.ProcessEventFromCamera(drgevent, PInputType.DragDrop, Camera, this);
		}

		/// <summary>
		/// Overridden.  Forwards the DragOver event to the default input manager.
		/// </summary>
		/// <param name="drgevent">A DragEventArgs that contains the event data.</param>
		protected override void OnDragOver(DragEventArgs drgevent) {
			base.OnDragOver (drgevent);
			Root.DefaultInputManager.ProcessEventFromCamera(drgevent, PInputType.DragOver, Camera, this);
		}

		/// <summary>
		/// Overridden.  Forwards the DragEnter event to the default input manager.
		/// </summary>
		/// <param name="drgevent">A DragEventArgs that contains the event data.</param>
		protected override void OnDragEnter(DragEventArgs drgevent) {
			base.OnDragEnter (drgevent);
		}

		/// <summary>
		/// Overridden.  Forwards the DragLeave event to the default input manager.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnDragLeave(EventArgs e) {
			base.OnDragLeave (e);
		}

		/// <summary>
		/// Invalidates the specified region of the canvas (adds it to the canvas's update region,
		/// which is the area that will be repainted at the next paint operation), and causes a paint
		/// message to be sent to the canvas.
		/// </summary>
		/// <param name="bounds">A rectangle object that represents the region to invalidate.</param>
		public virtual void InvalidateBounds(RectangleF bounds) {
			PDebug.ProcessInvalidate();
            IsInvalidated = true;
			if (regionManagement) {
				// Hack: Invalidate the bounds of the previously invalidated rectangle
				// and the current rectangle, since invalidating lots of small rectangles
				// causes a performance hit.
				if (invalidatedBounds.IsEmpty) {
					invalidatedBounds = bounds;
				} else {
					invalidatedBounds = RectangleF.Union(invalidatedBounds, bounds);
				}

				var insetRect = new RectangleF(invalidatedBounds.X - 1, invalidatedBounds.Y - 1,
					invalidatedBounds.Width + 2, invalidatedBounds.Height + 2);

				int x = (int)Math.Floor(insetRect.X);
				int y = (int)Math.Floor(insetRect.Y);
				int width = (int)Math.Ceiling(insetRect.Right - x);
				int height = (int)Math.Ceiling(insetRect.Bottom - y);
				Invalidate(new Rectangle(x, y, width, height));
			} else {
				Invalidate(true);
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="Control.OnPaint">Control.OnPaint</see>.
		/// </summary>
		protected override void OnPaint(PaintEventArgs pe) {
			PDebug.StartProcessingOutput();

            IsInvalidated = false;

			// Clear the invalidatedBounds cache.
			invalidatedBounds = RectangleF.Empty;

			PPaintContext paintContext = CreatePaintContext(pe);
			PaintPiccolo(paintContext);

			// Calling the base class OnPaint
			base.OnPaint(pe);

			PDebug.EndProcessingOutput(paintContext);
		}

		/// <summary>
		/// Paints the piccolo hierarchy.
		/// </summary>
		/// <remarks>
		/// Subclasses that add painting code should override this method rather than
		/// <see cref="PCanvas.OnPaint">PCanvas.OnPaint</see> to ensure that any extra processing
		/// will be included in the output frame rate calculation.
		/// </remarks>
		/// <param name="paintContext">The paint context to use for painting piccolo.</param>
        private void PaintPiccolo(PPaintContext paintContext) {
			// create new paint context and set render quality to lowest common 
			// denominator render quality.
			if (Interacting || Animating) {
				if (interactingRenderQuality < animatingRenderQuality) {
					paintContext.RenderQuality = interactingRenderQuality;
				} else {
					paintContext.RenderQuality = animatingRenderQuality;
				}
			} else {
				paintContext.RenderQuality = defaultRenderQuality;
			}

			// paint 
			camera.FullPaint(paintContext);

			// if switched state from animating to not animating invalidate the entire
			// screen so that it will be drawn with the default instead of animating 
			// render quality.
			if (!Animating && animatingOnLastPaint) {
				Invalidate();
			}
			animatingOnLastPaint = Animating;
		}

		/// <summary>
		/// Override this method to return a subclass of PPaintContext.
		/// </summary>
		/// <remarks>
		/// This can be useful if you are trying to plug in a different renderer, other
		/// than GDI+.  See the PiccoloDirect3D sample.
		/// </remarks>
		/// <param name="pe">A PaintEventArgs that contains the PaintEvent data.</param>
		/// <returns>The paint context to use for painting piccolo.</returns>
		protected virtual PPaintContext CreatePaintContext(PaintEventArgs pe) {
			return new PPaintContext(pe.Graphics, this);
		}

		/// <summary>
		/// Causes the canvas to immediately paint it's invalidated regions.
		/// </summary>
		public virtual void PaintImmediately() {
			if (paintingImmediately) {
				return;
			}

			paintingImmediately = true;
			Update();
			paintingImmediately = false;
		}
		#endregion
	}
}
