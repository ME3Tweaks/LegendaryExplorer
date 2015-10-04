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
using System.Windows.Forms;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.Piccolo {
	/// <summary>
	/// <b>PInputManager</b> is responsible for dispatching PInputEvents to node's
	/// event listeners.
	/// </summary>
	/// <remarks>Events are dispatched from PRoot's processInputs method.</remarks>
	public class PInputManager : InputSource {
		#region Fields
		private PointF lastCanvasPosition;
		private PointF currentCanvasPosition;

		private EventArgs nextInput;
		private PInputType nextType;
		private PCamera nextInputSource;
		private PCanvas nextWindowsSource;

		private PPickPath mouseFocus;
		private PPickPath previousMouseFocus;
		private PPickPath mouseOver;
		private PPickPath previousMouseOver;
		private PPickPath keyboardFocus;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PInputManager.
		/// </summary>
		public PInputManager() {
			lastCanvasPosition = new PointF(0, 0);
			currentCanvasPosition = new PointF(0, 0);
		}
		#endregion

		#region Basic
		//****************************************************************
		// Basic - Methods for setting/getting the current focus nodes and
		// canvas position.
		//****************************************************************

		/// <summary>
		/// Gets or sets the node that currently has the keyboard focus.
		/// </summary>
		/// <value>A pick path with the node that has the keyboard focus.</value>
		/// <remarks>This pick path receives the key events.</remarks>
		public virtual PPickPath KeyboardFocus {
			get { return keyboardFocus; }
			set {
				PInputEventArgs focusEvent = new PInputEventArgs(this, null, PInputType.LostFocus);

				if (keyboardFocus != null) {
					DispatchToPath(focusEvent, PInputType.LostFocus, keyboardFocus);
				}
		
				keyboardFocus = value;
		
				if (keyboardFocus != null) {
					DispatchToPath(focusEvent, PInputType.GotFocus, keyboardFocus);
				}
			}
		}

		/// <summary>
		/// Gets or sets the node that currently has the mouse focus.
		/// </summary>
		/// <remarks>
		/// This will return a pick path with the node that received the current mouse
		/// pressed event, or null if the mouse is not pressed. The mouse focus gets
		/// mouse dragged events even when the mouse is not over the mouse focus.
		/// </remarks>
		public virtual PPickPath MouseFocus {
			get { return mouseFocus; }
			set {
				previousMouseFocus = mouseFocus;
				mouseFocus = value;
			}
		}

		/// <summary>
		/// Gets the node the mouse is currently over
		/// </summary>
		/// <value>A pick path containing the node the mouse is over.</value>
		public virtual PPickPath MouseOver {
			get { return mouseOver; }
			set { mouseOver = value; }
		}

		/// <summary>
		/// Gets the mouse position before the last mouse event, in canvas coordinates.
		/// </summary>
		/// <value>The last mouse position.</value>
		public virtual PointF LastCanvasPosition {
			get { return lastCanvasPosition; }
		}

		/// <summary>
		/// Gets the current mouse position, in canvas coordinates.
		/// </summary>
		/// <value>The current mouse position.</value>
		public virtual PointF CurrentCanvasPosition {
			get { return currentCanvasPosition; }
		}
		#endregion

		#region Event Dispatch
		//****************************************************************
		// Event Handling - Methods for handling events
		// 
		// The dispatch manager updates the focus nodes based on the
		// incoming events, and dispatches those events to the appropriate
		// focus nodes.
		//****************************************************************
	
		/// <summary>
		/// Create a new PInputEvent based on the next windows event and dispatch it to Piccolo.
		/// </summary>
		public virtual void ProcessInput() {
			if (nextInput == null) return;

			PInputEventArgs e = new PInputEventArgs(this, nextInput, nextType);

			//The EventArgs object for a Click event does not provide the position, so
			//we just ignore it here.
			if (e.IsMouseEvent || e.IsDragDropEvent) {
				lastCanvasPosition = currentCanvasPosition;

				if (e.IsMouseEvent) {
					currentCanvasPosition = new PointF(((MouseEventArgs)nextInput).X, ((MouseEventArgs)nextInput).Y);
				} else {
					Point pt = new Point((int)((DragEventArgs)nextInput).X, (int)((DragEventArgs)nextInput).Y);
					currentCanvasPosition = nextWindowsSource.PointToClient(pt);
				}

				PPickPath pickPath = nextInputSource.Pick(currentCanvasPosition.X, currentCanvasPosition.Y, 1);
				MouseOver = pickPath;
			}

			nextInput = null;
			nextInputSource = null;

			Dispatch(e);
		}

		/// <summary>
		/// Dispatch the given event to the appropriate focus node.
		/// </summary>
		/// <param name="e">An PInputEventArgs that contains the event data.</param>
		public virtual void Dispatch(PInputEventArgs e) {
			switch (e.Type) {
				case PInputType.KeyDown:
					DispatchToPath(e, PInputType.KeyDown, KeyboardFocus);
					break;

				case PInputType.KeyPress:
					DispatchToPath(e, PInputType.KeyPress, KeyboardFocus);
					break;

				case PInputType.KeyUp:
					DispatchToPath(e, PInputType.KeyUp, KeyboardFocus);
					break;

				case PInputType.Click:
					//The click event occurs before the MouseRelease so we can
					//dispatch to the current focused node as opposed to the
					//previously focused node
					if (MouseFocus.PickedNode == MouseOver.PickedNode) {
						DispatchToPath(e, PInputType.Click, MouseFocus);
					}
					break;

				case PInputType.DoubleClick:
					//The double click event occurs before the MouseRelease so we can
					//dispatch to the current focused node as opposed to the
					//previously focused node
					if (MouseFocus.PickedNode == MouseOver.PickedNode) {
						DispatchToPath(e, PInputType.DoubleClick, MouseFocus);
					}
					break;

				case PInputType.MouseDown:
					MouseFocus = MouseOver;
					DispatchToPath(e, PInputType.MouseDown, MouseFocus);
					break;

				case PInputType.MouseUp:
					CheckForMouseEnteredAndExited(e);
					DispatchToPath(e, PInputType.MouseUp, MouseFocus);
					mouseFocus = null;
					break;

				case PInputType.MouseDrag:
					CheckForMouseEnteredAndExited(e);
					DispatchToPath(e, PInputType.MouseDrag, MouseFocus);
					break;

				case PInputType.MouseMove:
					CheckForMouseEnteredAndExited(e);
					DispatchToPath(e, PInputType.MouseMove, MouseOver);
					break;

				case PInputType.MouseWheel:
					MouseFocus = MouseOver;
					DispatchToPath(e, PInputType.MouseWheel, MouseOver);
					break;
				case PInputType.DragOver:
					CheckForMouseDragEnteredAndExited(e);
					DispatchToPath(e, PInputType.DragOver, MouseOver); 
					break;
				case PInputType.DragDrop:
					CheckForMouseDragEnteredAndExited(e);
					DispatchToPath(e, PInputType.DragDrop, MouseOver);
					break;
			}
		}

		/// <summary>
		/// Check if the mouse has entered or exited a node during a mouse move or drag
		/// operation and, if so, dispatch the appropriate event.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void CheckForMouseEnteredAndExited(PInputEventArgs e) {
			PNode c = (mouseOver != null) ? mouseOver.PickedNode : null; 
			PNode p = (previousMouseOver != null) ? previousMouseOver.PickedNode : null;

			if (c != p) {
				DispatchToPath(e, PInputType.MouseLeave, previousMouseOver);
				DispatchToPath(e, PInputType.MouseEnter, mouseOver);
				previousMouseOver = mouseOver;
			}
		}

		/// <summary>
		/// Check if the mouse has entered or exited a node during a drag and drop
		/// operation and, if so, dispatch the appropriate event.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void CheckForMouseDragEnteredAndExited(PInputEventArgs e) {
			PNode c = (mouseOver != null) ? mouseOver.PickedNode : null; 
			PNode p = (previousMouseOver != null) ? previousMouseOver.PickedNode : null;

			if (c != p) {
				DispatchToPath(e, PInputType.DragLeave, previousMouseOver);
				DispatchToPath(e, PInputType.DragEnter, mouseOver);
				previousMouseOver = mouseOver;
			}
		}

		/// <summary>
		/// Dispatch the given PInputEvent to the given pick path.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <param name="type">The type of PInputEvent being dispatched.</param>
		/// <param name="path">The pick path to which the PInputEvent will be dispatched.</param>
		public virtual void DispatchToPath(PInputEventArgs e, PInputType type, PPickPath path) {
			if (path != null) {
				//set the type and clear the handled bit since the same event object is
				//used to send multiple events such as mouseEntered/mouseExited and mouseMove.
				e.Type = type;
				e.Handled = false;

				path.ProcessEvent(e);
			}
		}

		/// <summary>
		/// Process the given windows event from the camera.
		/// </summary>
		/// <param name="e">The windows event to be processed.</param>
		/// <param name="type">The type of windows event being processed.</param>
		/// <param name="camera">The camera from which to process the windows event.</param>
		/// <param name="canvas">The source of the windows event being processed.</param>
		public virtual void ProcessEventFromCamera(EventArgs e, PInputType type, PCamera camera, PCanvas canvas) {
			nextInput = e;
			nextType = type;
			nextInputSource = camera;
			nextWindowsSource = canvas;
			camera.Root.ProcessInputs();
		}
		#endregion
	}
}
