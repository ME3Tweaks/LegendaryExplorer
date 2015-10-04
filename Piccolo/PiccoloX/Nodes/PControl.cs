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
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Util;

using UMD.HCIL.PiccoloX.Util;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// <b>PControl</b> is a wrapper around a <see cref="Control">
	/// System.Windows.Forms.Control</see>.
	/// </summary>
	/// <remarks>
	/// This class provides a mechanism for adding standard controls to the piccolo scene
	/// graph, with some limitations.  When a PControl's <see cref="Editing"/> property is set
	/// to <c>true</c>, the underlying control is added to the specified canvas and treated as
	/// a normal windows component.  However, when the <see cref="Editing"/> property is set to
	/// <c>false</c>, the control is rendered as an image.  Typically, you will want to
	/// turn off editing while the user is zooming or panning the camera and turn editing
	/// back on when the user is ready to interact with the control.
	/// <para>
	/// The underlying control's "natural size" or the actual bounds of the control is
	/// equivalent to the bounds of the node at 100% scale.  So, setting the
	/// bounds of PControl will directly set the bounds of the underlying control.  Typically,
	/// you will use PControl nodes in conjunction with
	/// <see cref="UMD.HCIL.PiccoloX.Events.PControlEventHandler">PControlEventHandler</see>,
	/// which only allows a PControl node to be editable when it is displayed at its
	/// natural size.
	/// </para>
	/// <para>
	/// The <see cref="CurrentCanvas"/> property indicates which canvas will display the
	/// editable control when editing is turned on.  All other canvases will display an
	/// image.  This is necessary since an instance of a control cannot be added to a
	/// canvas more than once or to more than one canvas.
	/// </para>
	/// </remarks>
	public class PControl : PNode {
		private static PMatrix TEMP_MATRIX = new PMatrix();

		private Control control = null;
		private Image image = null;
        private bool imageDirty = false;
		private PCanvas currentCanvas = null;

        /// <summary>
        /// Constructs a new PControl,
        /// setting the current canvas to the given canvas.
        /// </summary>
        /// <param name="currentCanvas">
        /// The canvas to add the control to when <see cref="Editing"/> is turned on.
        /// </param>
        public PControl(PCanvas currentCanvas) {
            this.currentCanvas = currentCanvas;
        }

		/// <summary>
		/// Constructs a new PControl, wrapping the given
		/// <see cref="System.Windows.Forms.Control">System.Windows.Forms.Control</see>.
		/// </summary>
		/// <param name="control">The control to wrap.</param>
		/// <remarks>
		/// This constructor will set the current canvas to
		/// <see cref="PCanvas.CURRENT_PCANVAS">PCanvas.CURRENT_PCANVAS</see>.
		/// </remarks>
		public PControl(Control control)
			: this(control, PCanvas.CURRENT_PCANVAS) {}

		/// <summary>
		/// Constructs a new PControl, wrapping the given
		/// <see cref="System.Windows.Forms.Control">System.Windows.Forms.Control</see> and
		/// setting the current canvas to the given canvas.
		/// </summary>
		/// <param name="control">The control to wrap.</param>
		/// <param name="currentCanvas">
		/// The canvas to add the control to when <see cref="Editing"/> is turned on.
		/// </param>
		public PControl(Control control, PCanvas currentCanvas) {
			this.currentCanvas = currentCanvas;
            this.Control = control;
		}

		/// <summary>
		/// Updates the image of the control, used to render the node when <see cref="Editing"/>
		/// is turned off.
		/// </summary>
		/// <remarks>
		/// Inheritors can override this method to modify how the image is generated for a
		/// given control.
		/// </remarks>
		public virtual void UpdateImage() {
            imageDirty = true;
        }

        /// <summary>
        /// Gets or sets the image used to display the control when not Editing
        /// </summary>
        /// <value>The image used to display the control when not Editing</value>
        protected Image Image {
            get {
                if (imageDirty) {
                    image = GenerateImage();
                    imageDirty = false;
                }
                return image; 
            }
            set { image = value; }
        }

        /// <summary>
        /// Generates an image that represents the control to be used when 
        /// the control is unmapped.
        /// </summary>
        /// <returns></returns>
        protected virtual Image GenerateImage() {
            Image i;

            if (control.Parent == null) {
                i = PXUtil.OffscreenGrab(control);
            } else {
                i = PXUtil.GrabControl(control);
            }

            return i;
        }

		/// <summary>
		/// Gets or sets the control wrapped by this node.
		/// </summary>
		/// <value>The control wrapped by this node.</value>
		public Control Control {
			get { return control; }
			set { 
                control = value;
                Bounds = control.Bounds;
            }
		}

		/// <summary>
		/// Gets or sets the current location of the control wrapped by this node.
		/// </summary>
		/// <value>The current location of the control wrapped by this node.</value>
		public Point ControlLocation {
			get { return control.Location; }
			set { control.Location = value; }
		}

		/// <summary>
		/// Gets or sets the canvas the control will be added to when <see cref="Editing"/> is
		/// turned on.
		/// </summary>
		/// <value>
		/// The canvas this control will be added to when <see cref="Editing"/> is turned on.
		/// </value>
		public PCanvas CurrentCanvas {
			get { return currentCanvas; }
			set {
				if (currentCanvas != value) {
					bool editing = Editing;
					currentCanvas = value;

					if (editing) {
						AddControlToCanvas();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether or not the control is editable.
		/// </summary>
		/// <value>Indicates whether or not the control is editable.</value>
		/// <remarks>
		/// When the property is set to <c>true</c>, the underlying control is added to the
		/// current canvas and treated as a normal windows component.  However, when the
		/// property is set to <c>false</c>, the control is rendered as an image.  
		/// </remarks>
		public virtual bool Editing {
			get { return control.Parent == currentCanvas; }

			set {
				if (value) {
					// Add the control to the canvas if it is not already added and if this node
					// is in the scene graph.
					if (!Editing && this.Parent != null) {
						AddControlToCanvas();
					}
				}
				else {
					if (control.Parent != null) {
						UpdateImage();
						control.Parent = null;
					}
				}
			}
		}

		/// <summary>
		/// Adds the control to the current canvas.
		/// </summary>
		protected virtual void AddControlToCanvas() {
			control.Parent = currentCanvas;

			// This immediate redraw is necessary to force the image to be
			// created when the window is layered.  Without this call,
			// PXUtil.GrabControl may not work properly if it is called
			// soon after the control is added to the canvas.
			control.Refresh(); 
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.SetBounds">PNode.SetBounds</see>.
		/// </summary>
		public override bool SetBounds(float x, float y, float width, float height) {
			if (base.SetBounds (x, y, width, height)) {
				control.Bounds = new Rectangle(control.Left, control.Top, (int)width, (int)height);
				UpdateImage();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.Paint">PNode.Paint</see>.
		/// </summary>
		protected override void Paint(PPaintContext paintContext) {
			base.Paint (paintContext);

			Graphics g = paintContext.Graphics;
			PMatrix matrix = new PMatrix(g.Transform);
			RectangleF transRectF = matrix.Transform(bounds);
			Rectangle transRect = new Rectangle((int)transRectF.X, (int)transRectF.Y, (int)transRectF.Width, (int)transRectF.Height);

			// Draw the image if the control node is not in editing mode or
			// if the control is being rendered in a view other than the one
			// that owns the control.
			if (!Editing || control.Bounds != transRect || paintContext.Canvas != currentCanvas) {
				if (Image != null) g.DrawImage(Image, bounds);
			}
		}
	}
}