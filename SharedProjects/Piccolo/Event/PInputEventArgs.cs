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
using System.Windows.Forms;
using Piccolo.Util;

namespace Piccolo.Event {
	#region Input Event Types
	/// <summary>
	/// This enumeration is used by the PInputEventArgs.  It represents the
	/// types of PInputEvents that are dispatched to Piccolo.
	/// </summary>
	public enum PInputType {
		/// <summary>
		/// Identifies a <see cref="PNode.KeyDown">KeyDown</see> event.
		/// </summary>
		KeyDown,

		/// <summary>
		/// Identifies a <see cref="PNode.KeyPress">KeyPress</see> event.
		/// </summary>
		KeyPress,

		/// <summary>
		/// Identifies a <see cref="PNode.KeyUp">KeyUp</see> event.
		/// </summary>
		KeyUp,

		/// <summary>
		/// Identifies a <see cref="PNode.Click">Click</see> event.
		/// </summary>
		Click,

		/// <summary>
		/// Identifies a <see cref="PNode.DoubleClick">DoubleClick</see> event.
		/// </summary>
		DoubleClick,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseDown">MouseDown</see> event.
		/// </summary>
		MouseDown,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseUp">MouseUp</see> event.
		/// </summary>
		MouseUp,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseMove">MouseMove</see> event.
		/// </summary>
		MouseMove,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseDrag">MouseDrag</see> event.
		/// </summary>
		MouseDrag,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseEnter">MouseEnter</see> event.
		/// </summary>
		MouseEnter,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseLeave">MouseLeave</see> event.
		/// </summary>
		MouseLeave,

		/// <summary>
		/// Identifies a <see cref="PNode.MouseWheel">MouseWheel</see> event.
		/// </summary>
		MouseWheel,

		/// <summary>
		/// Identifies a <see cref="PNode.DragEnter">DragEnter</see> event.
		/// </summary>
		DragEnter,

		/// <summary>
		/// Identifies a <see cref="PNode.DragLeave">DragLeave</see> event.
		/// </summary>
		DragLeave,

		/// <summary>
		/// Identifies a <see cref="PNode.DragOver">DragOver</see> event.
		/// </summary>
		DragOver,

		/// <summary>
		/// Identifies a <see cref="PNode.DragDrop">DragDrop</see> event.
		/// </summary>
		DragDrop,

		/// <summary>
		/// Identifies a <see cref="PNode.GotFocus">GotFocus</see> event.
		/// </summary>
		GotFocus,

		/// <summary>
		/// Identifies a <see cref="PNode.LostFocus">LostFocus</see> event.
		/// </summary>
		LostFocus
	};
	#endregion

	/// <summary>
	/// <b>PInputEventArgs</b> is used to pass keyboard and mouse event data to
	/// PInputEventListeners.
	/// </summary>
	/// <remarks>
	/// This class has methods for normal event properties such as event modifier keys
	/// and event canvas location.
	/// <para>
	/// In addition it has methods to get the mouse position and delta in a variety 
	/// of coordinate systems.
	/// </para>
	/// <para>
	/// Last of all, it provides access to the dispatch manager that can be queried
	/// to find the current mouse over, mouse focus, and keyboard focus.
	/// </para>
	/// </remarks>
	public sealed class PInputEventArgs {
		#region Fields
		private EventArgs e;
		private PPickPath pickPath;
		private PInputManager inputManager;
		private bool handled;
		private PInputType type;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PInputEventArgs
		/// </summary>
		/// <param name="inputManager">The input manager that dispatched this event.</param>
		/// <param name="e">An EventArgs that contains the event data.</param>
		/// <param name="type">The type of input event.</param>
		public PInputEventArgs(PInputManager inputManager, EventArgs e, PInputType type) {
			this.inputManager = inputManager;
			this.e = e;
			this.type = type;
		}
		#endregion

		#region Accessing Picked Objects
		//****************************************************************
		// Accessing Picked Objects - Methods to access the objects associated
		// with this event.
		// 
		// Cameras can view layers that have 
		// other cameras on them, so events may be arriving through a stack
		// of many cameras. The Camera property returns the bottom-most
		// camera on that stack. The TopCamera property returns the top-most
		// camera on that stack, this is also the camera through which the
		// event originated.
		//****************************************************************

		/// <summary>
		/// Set the canvas cursor, and remember the previous cursor on the cursor stack.
		/// </summary>
		/// <param name="cursor">The new canvas cursor.</param>
		public void PushCursor(Cursor cursor) {
			TopCamera.Canvas.PushCursor(cursor);
		}
	
		/// <summary>
		/// Pop the cursor on top of the cursorStack and set it as the canvas cursor.
		/// </summary>
		public void PopCursor() {
			TopCamera.Canvas.PopCursor();
		}

		/// <summary>
		/// Gets the bottom-most camera that is currently painting.
		/// </summary>
		/// <value>The bottom-most camera.</value>
		/// <remarks>
		/// If you are using internal cameras this may be different than what is
		/// returned by TopCamera.
		/// </remarks>
		public PCamera Camera => Path.BottomCamera;

        /// <summary>
		/// Gets the top-most camera this is painting.
		/// </summary>
		/// <value>The top-most camera.</value>
		/// <remarks>
		/// This is the camera associated with the PCanvas that requested the current
		/// repaint.
		/// </remarks>
		public PCamera TopCamera => Path.TopCamera;

        /// <summary>
		/// Gets the canvas associated with the top camera.
		/// </summary>
		/// <value>The canvas associated with the top camera.</value>
		/// <remarks>
		/// This is the canvas where the originating event came from.
		/// </remarks>
		public PCanvas Canvas => TopCamera.Canvas;

        /// <summary>
		/// Gets the input manager that dispatched this event.
		/// </summary>
		/// <value>The input manager that dispatched this event.</value>
		/// <remarks>
		/// You can use this input manager to find the current mouse focus, mouse
		/// over, and key focus nodes. You can also set a new key focus node.
		/// </remarks>
		public PInputManager InputManager => inputManager;

        /// <summary>
		/// Gets or sets the PPickPath associated with this input event.
		/// </summary>
		/// <value>The PPickPath associated with this input event.</value>
		public PPickPath Path {
			get => pickPath;
            set => pickPath = value;
        }

		/// <summary>
		/// Gets the bottom node on the current pickpath, that is the picked node
		/// furthest from the root node.
		/// </summary>
		/// <value>The picked node.</value>
		public PNode PickedNode {
			get => Path.PickedNode;
            set => Path.PushNode(value);
        }
		#endregion

		#region Basics
		//****************************************************************
		// Basics - Methods for accessing event data.
		//****************************************************************

		/// <summary>
		/// Gets the Keyboard code for a <see cref="PNode.KeyDown">KeyDown</see> or
		/// <see cref="PNode.KeyUp">KeyUp</see> event.
		/// </summary>
		/// <value>A Keys value that is the key code for the event.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public Keys KeyCode {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.KeyCode;
				}
				throw new InvalidOperationException("Can't get the KeyCode from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets the key data for a <see cref="PNode.KeyDown">KeyDown</see> or
		/// <see cref="PNode.KeyUp">KeyUp</see> event.
		/// </summary>
		/// <value>
		/// A Keys value representing the key code for the key that was pressed, combined
		/// with modifier flags that indicate which combination of CTRL, SHIFT, and ALT
		/// keys were pressed at the same time.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public Keys KeyData {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.KeyData;
				}
				throw new InvalidOperationException("Can't get the KeyData from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets the keyboard value for a <see cref="PNode.KeyDown">KeyDown</see> or
		/// <see cref="PNode.KeyUp">KeyUp</see> event.
		/// </summary>
		/// <value>The integer representation of the KeyData property.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public int KeyValue {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.KeyValue;
				}
				throw new InvalidOperationException("Can't get the KeyValue from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets the modifier flags.  This indicates which combination of modifier keys
		/// (CTRL, SHIFT, and ALT) were pressed.
		/// </summary>
		/// <value>A Keys value representing one or more modifier flags.</value>
		/// <remarks>
		/// If IsKeyEvent returns false, this value is equivalent to the value of
		/// <see cref="System.Windows.Forms.Control.ModifierKeys">Control.ModifierKeys</see>
		/// </remarks>
		public Keys Modifiers {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.Modifiers;
				}
				else return System.Windows.Forms.Control.ModifierKeys;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the SHIFT key was pressed.
		/// </summary>
		/// <value>True if the SHIFT key was pressed; otherwise, false.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public bool Shift {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.Shift;
				}
			    return Modifiers.HasFlag(Keys.Shift);
			}
		}

		/// <summary>
		/// Gets a value indicating whether the ALT key was pressed.
		/// </summary>
		/// <value>True if the ALT key was pressed; otherwise, false.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public bool Alt {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.Alt;
				}
				return Modifiers.HasFlag(Keys.Alt);
			}
		}

		/// <summary>
		/// Gets a value indicating whether the CTRL key was pressed.
		/// </summary>
		/// <value>True if the CTRL key was pressed; otherwise, false.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key event.
		/// </exception>
		public bool Control {
			get {
				if (IsKeyEvent) {
					KeyEventArgs ke = (KeyEventArgs)e;
					return ke.Control;
				}

			    return Modifiers.HasFlag(Keys.Control);
			}
		}

		/// <summary>
		/// Gets the character corresponding to the key pressed.
		/// </summary>
		/// <value>
		/// The ASCII character that is composed. For example, if the user presses the
		/// SHIFT + K, this property returns an uppercase K.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a key press event.
		/// </exception>
		public char KeyChar {
			get {
				if (IsKeyPressEvent) {
					KeyPressEventArgs kpe = (KeyPressEventArgs)e;
					return kpe.KeyChar;
				}
				throw new InvalidOperationException("Can't get KeyChar from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets which mouse button was pressed.
		/// </summary>
		/// <value>One of the <see cref="MouseButtons">MouseButtons</see> values.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a mouse event.
		/// </exception>
		public MouseButtons Button {
			get {
				if (IsMouseEvent) {
					MouseEventArgs me = (MouseEventArgs)e;
					return me.Button;
				}
				throw new InvalidOperationException("Can't get Button from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets the number of times the mouse button was pressed and released.
		/// </summary>
		/// <value>The number of times the mouse button was pressed and released.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a mouse event.
		/// </exception>
		public int Clicks {
			get {
				if (IsMouseEvent) {
					MouseEventArgs me = (MouseEventArgs)e;
					return me.Clicks;
				}
				throw new InvalidOperationException("Can't get Clicks from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets a signed count of the number of detents the mouse wheel has rotated.
		/// A detent is one notch of the mouse wheel.
		/// </summary>
		/// <value>
		/// A signed count of the number of detents the mouse wheel has rotated.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a mouse event.
		/// </exception>
		public int WheelDelta {
			get {
				if (IsMouseEvent) {
					MouseEventArgs me = (MouseEventArgs)e;
					return me.Delta;
				}
				throw new InvalidOperationException("Can't get WheelDelta from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets which drag-and-drop operations are allowed by the originator (or source)
		/// of the drag drop event.
		/// </summary>
		/// <value>One of the <see cref="DragDropEffects">DragDropEffects</see> values.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a drag drop event.
		/// </exception>
		public DragDropEffects AllowedDragDropEffects {
			get {
				if (IsDragDropEvent) {
					DragEventArgs de = (DragEventArgs)e;
					return de.AllowedEffect;
				}
				throw new InvalidOperationException("Can't get AllowedDragDropEffects from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets the <see cref="IDataObject">IDataObject</see> that contains the data
		/// associated with this event.
		/// </summary>
		/// <value>The data associated with this event.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a drag drop event.
		/// </exception>
		public IDataObject DragDropData {
			get {
				if (IsDragDropEvent) {
					DragEventArgs de = (DragEventArgs)e;
					return de.Data;
				}
				throw new InvalidOperationException("Can't get DragDropData from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets or sets the target drop effect in a drag-and-drop operation.
		/// </summary>
		/// <value>One of the <see cref="DragDropEffects">DragDropEffects</see> values.</value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a drag drop event.
		/// </exception>
		public DragDropEffects DragDropEffect {
			get {
				if (IsDragDropEvent) {
					DragEventArgs de = (DragEventArgs)e;
					return de.Effect;
				}
				throw new InvalidOperationException("Can't get DragDropEffect from a " + type + " event");
			}
			set {
				if (IsDragDropEvent) {
					DragEventArgs de = (DragEventArgs)e;
					de.Effect = value;
				}
				else {
					throw new InvalidOperationException("Can't set DragDropEffect on a " + type + " event");
				}
			}
		}

		/// <summary>
		/// Gets the current state of the SHIFT, CTRL, and ALT keys, as well as the state
		/// of the mouse buttons.
		/// </summary>
		/// <value>
		/// The current state of the SHIFT, CTRL, and ALT keys and of the mouse buttons.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// Thrown if the event is not a drag drop event.
		/// </exception>
		public int DragDropKeyState {
			get {
				if (IsDragDropEvent) {
					return ((DragEventArgs)e).KeyState;
				}
				throw new InvalidOperationException("Can't get DragDropKeyState from a " + type + " event");
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether an event handler has handled this
		/// event.
		/// </summary>
		/// <value>True if this event has been handled; otherwise, false.</value>
		/// <remarks>
		/// This is a relaxed form of consuming events.  The event will continue to get
		/// dispatched to event handlers even after it is marked as handled, but other
		/// event handlers that might conflict are expected to ignore events that have
		/// already been handled.
		/// </remarks>
		public bool Handled {
			get => handled;
            set => handled = value;
        }

		/// <summary>
		/// Gets the underlying source EventArgs for this PInputEventArgs.
		/// </summary>
		/// <value>The underlying source EventArgs for this PInputEventArgs.</value>
		public EventArgs SourceEventArgs => e;

        #endregion

		#region Classification
		//****************************************************************
		// Classification - Methods to distinguish between mouse and key
		// events.
		//****************************************************************

		/// <summary>
		/// Gets or sets the type of this input event.
		/// </summary>
		/// <value>The type of this event.</value>
		public PInputType Type {
			get => type;
            set => type = value;
        }

		/// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.KeyDown">KeyDown</see>
		/// or <see cref="PNode.KeyUp">KeyUp</see> event.
		/// </summary>
		/// <value>True if this is a key event; otherwise, false.</value>
		public bool IsKeyEvent => e is KeyEventArgs;

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.KeyPress">KeyPress</see>
		/// event.
		/// </summary>
		/// <value>True if this is a key press event; otherwise, false.</value>
		public bool IsKeyPressEvent => e is KeyPressEventArgs;

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.MouseDown">MouseDown</see>,
		/// <see cref="PNode.MouseUp">MouseUp</see>, <see cref="PNode.MouseMove">MouseMove</see>,
		/// <see cref="PNode.MouseDrag">MouseDrag</see>, <see cref="PNode.MouseEnter">MouseEnter</see>,
		/// <see cref="PNode.MouseLeave">MouseLeave</see>, or <see cref="PNode.MouseWheel">MouseWheel</see>
		/// event.
		/// </summary>
		/// <value>True if this is a mouse event; otherwise, false.</value>
		public bool IsMouseEvent => (e is MouseEventArgs);

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.DragEnter">DragEnter</see>,
		/// <see cref="PNode.DragLeave">DragLeave</see>, <see cref="PNode.DragOver">DragOver</see>,
		/// or <see cref="PNode.DragDrop">DragDrop</see> event.
		/// </summary>
		/// <value>True if this is drag drop event; otherwise, false.</value>
		public bool IsDragDropEvent => (e is DragEventArgs);

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.Click">Click</see> event.
		/// </summary>
		/// <value>True if this is a click event; otherwise, false.</value>
		public bool IsClickEvent => (Type == PInputType.Click);

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.GotFocus">GotFocus</see> or
		/// <see cref="PNode.LostFocus">LostFocus</see> event.
		/// </summary>
		/// <value>True if this is a focus event; otherwise, false.</value>
		public bool IsFocusEvent => e == null;

        /// <summary>
		/// Gets a value indicating if this is a <see cref="PNode.MouseEnter">MouseEnter</see>
		/// of <see cref="PNode.MouseLeave">MouseLeave</see> event.
		/// </summary>
		/// <value>
		/// True if this is a mouse enter or mouse leave event; otherwise, false.
		/// </value>
		public bool IsMouseEnterOrMouseLeave => (Type == PInputType.MouseEnter || Type == PInputType.MouseLeave);

        #endregion

		#region Coordinate Systems
		//****************************************************************
		// Coordinate Systems - Methods for getting mouse location data 
		// These methods are only designed for use with PInputEvents that 
		// return true to the IsMouseEvent method.
		//****************************************************************

		/// <summary>
		/// Gets the mouse position in PCanvas coordinates.
		/// </summary>
		/// <value>The mouse position in canvas coordinates.</value>
		public PointF CanvasPosition => inputManager.CurrentCanvasPosition;

        /// <summary>
		/// Gets the delta between the last and current mouse position in PCanvas
		/// coordinates.
		/// </summary>
		/// <value>
		/// The delta between the last and current mouse position in canvas coordinates.
		/// </value>
		public SizeF CanvasDelta {
			get {
				PointF last = inputManager.LastCanvasPosition;
				PointF current = inputManager.CurrentCanvasPosition;
				return new SizeF(current.X - last.X, current.Y - last.Y);
			}
		}

		/// <summary>
		/// Return the mouse position relative to a given node on the pick path.
		/// </summary>
		/// <param name="nodeOnPath">
		/// The returned position will be in the local coordinate system of this node.
		/// </param>
		/// <returns>The mouse position relative to a given node on the pick path.</returns>
		public PointF GetPositionRelativeTo(PNode nodeOnPath) {
			PointF r = CanvasPosition;
			return pickPath.CanvasToLocal(r, nodeOnPath);
		}

		/// <summary>
		/// Return the delta between the last and current mouse positions relative to a
		/// given node on the pick path.
		/// </summary>
		/// <param name="nodeOnPath">
		/// The returned delta will be in the local coordinate system of this node.
		/// </param>
		/// <returns>
		/// The delta between the last and current mouse positions relative to a given
		/// node on the pick path.
		/// </returns>
		public SizeF GetDeltaRelativeTo(PNode nodeOnPath) {
			SizeF r = CanvasDelta;
			return pickPath.CanvasToLocal(r, nodeOnPath);
		}

		/// <summary>
		/// Return the mouse position transformed through the view matrix of the bottom
		/// camera.
		/// </summary>
		/// <value>
		/// The mouse position transformed through the view matrix of the bottom camera.
		/// </value>
		public PointF Position {
			get {
				PointF r = CanvasPosition;
				r = pickPath.CanvasToLocal(r, Camera);
				return Camera.LocalToView(r);
			}
		}

		/// <summary>
		/// Return the delta between the last and current mouse positions transformed
		/// through the view matrix of the bottom camera.
		/// </summary>
		/// <value>
		/// The delta between the last and current mouse positions transformed through
		/// the view matrix of the bottom camera.
		/// </value>
		public SizeF Delta {
			get {
				SizeF r = CanvasDelta;
				r = pickPath.CanvasToLocal(r, Camera);
				return Camera.LocalToView(r);
			}
		}
		#endregion

		#region Dispatch	
		//****************************************************************
		// Dispatch - Methods for sending the event to the nodes in the
		// pick path.
		//****************************************************************

		/// <summary>
		/// Raises the appropriate event on the given node.
		/// </summary>
		/// <param name="sender">The node for which the event will be raised.</param>
		public void DispatchTo(object sender) {
			switch (type) {
				case PInputType.KeyDown:
					((PNode)sender).OnKeyDown(this);
					break;

				case PInputType.KeyPress:
					((PNode)sender).OnKeyPress(this);
					break;

				case PInputType.KeyUp:
					((PNode)sender).OnKeyUp(this);
					break;

				case PInputType.Click:
					((PNode)sender).OnClick(this);
					break;

				case PInputType.DoubleClick:
					((PNode)sender).OnDoubleClick(this);
					break;

				case PInputType.MouseDown:
					((PNode)sender).OnMouseDown(this);
					break;

				case PInputType.MouseDrag:
					((PNode)sender).OnMouseDrag(this);
					break;

				case PInputType.MouseUp:
					((PNode)sender).OnMouseUp(this);
					break;

				case PInputType.MouseMove:
					((PNode)sender).OnMouseMove(this);
					break;

				case PInputType.MouseEnter:
					((PNode)sender).OnMouseEnter(this);
					break;

				case PInputType.MouseLeave:
					((PNode)sender).OnMouseLeave(this);
					break;

				case PInputType.MouseWheel:
					((PNode)sender).OnMouseWheel(this);
					break;

				case PInputType.DragEnter:
					((PNode)sender).OnDragEnter(this);
					break;

				case PInputType.DragLeave:
					((PNode)sender).OnDragLeave(this);
					break;

				case PInputType.DragOver:
					((PNode)sender).OnDragOver(this);
					break;

				case PInputType.DragDrop:
					((PNode)sender).OnDragDrop(this);
					break;

				case PInputType.GotFocus:
					((PNode)sender).OnGotFocus(this);
					break;

				case PInputType.LostFocus:
					((PNode)sender).OnLostFocus(this);
					break;
			}
		}
		#endregion
		
		#region Debugging
		//****************************************************************
		// Debugging - Methods for debugging
		//****************************************************************

		/// <summary>
		/// Overridden.  Returns a string representation of this object for debugging purposes.
		/// </summary>
		/// <returns>A string representation of this object.</returns>
		public override string ToString() {
			StringBuilder result = new StringBuilder();

			result.Append(base.ToString());
			result.Append('[');
			if (handled) {
				result.Append("handled");
			}
			result.Append(']');

			return result.ToString();
		}
		#endregion
	}
}
