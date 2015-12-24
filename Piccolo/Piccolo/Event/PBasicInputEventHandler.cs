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

namespace UMD.HCIL.Piccolo.Event {
	#region Delegates
	/// <summary>
	/// A delegate that is used to add a filter to this event handler class.
	/// </summary>
	public delegate bool AcceptsEventDelegate(PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.KeyDown">KeyDown</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void KeyDownDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.KeyPress">KeyPress</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void KeyPressDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.KeyUp">KeyUp</see> events
	/// sent to this event handler class.
	/// </summary>
	public delegate void KeyUpDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.Click">Click</see> events
	/// sent to this event handler class.
	/// </summary>
	public delegate void ClickDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.DoubleClick">DoubleClick</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void DoubleClickDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseDown">MouseDown</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void MouseDownDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseUp">MouseUp</see> events
	/// sent to this event handler class.
	/// </summary>
	public delegate void MouseUpDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseMove">MouseMove</see>
	/// sent to this event handler class.
	/// </summary>
	public delegate void MouseMoveDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseDrag">MouseDrag</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void MouseDragDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseEnter">MouseEnter</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void MouseEnterDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseLeave">MouseLeave</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void MouseLeaveDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.MouseWheel">MouseWheel</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void MouseWheelDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.DragEnter">DragEnter</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void DragEnterDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.DragLeave">DragLeave</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void DragLeaveDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.DragOver">DragOver</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void DragOverDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.DragDrop">DragDrop</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void DragDropDelegate(object sender, PInputEventArgs e);
	
	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.GotFocus">GotFocus</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void GotFocusDelegate(object sender, PInputEventArgs e);

	/// <summary>
	/// A delegate that is used to handle <see cref="PNode.LostFocus">LostFocus</see>
	/// events sent to this event handler class.
	/// </summary>
	public delegate void LostFocusDelegate(object sender, PInputEventArgs e);
	#endregion

	/// <summary>
	/// <b>PBasicInputEventHandler</b> is the standard class in Piccolo that
	/// is used to register for mouse and keyboard events on a PNode.
	/// </summary>
	/// <remarks>
	/// The events that you get depends on the node that you have registered with.
	/// For example you will only get mouse moved events when the mouse is over the node
	/// that you have registered with, not when the mouse is over some other node.
	/// <para>
	/// There are a couple of ways to use this event handler class.  You can extend
	/// PBasicInputEventHandler and override the OnEvent methods or you can instantiate
	/// a PBasicInputEventHandler and attach a handler method to one of the event
	/// delegates.
	/// </para>
	/// <para>
	/// <b>Note: </b>you can also attach an event handler method directly to an event on a node,
	/// but you will lose the ability to implement a filter or add other state to your
	/// event handler.  See the events in <c>PNode</c> for more details.
	/// </para>
	/// </remarks>
	public class PBasicInputEventHandler : PInputEventListener {
		#region Fields
		/// <summary>
		/// Used to add a filter to this event handler class.
		/// </summary>
		public AcceptsEventDelegate AcceptsEvent;

		/// <summary>
		/// Used to handle <see cref="PNode.KeyDown">KeyDown</see> events sent to this
		/// event handler class.
		/// </summary>
		public KeyDownDelegate KeyDown;

		/// <summary>
		/// Used to handle <see cref="PNode.KeyPress">KeyPress</see> events sent to this
		/// event handler class.
		/// </summary>
		public KeyPressDelegate KeyPress;

		/// <summary>
		/// Used to handle <see cref="PNode.KeyUp">KeyUp</see> events sent to this event
		/// handler class.
		/// </summary>
		public KeyUpDelegate KeyUp;

		/// <summary>
		/// Used to handle <see cref="PNode.KeyPress">Click</see> events sent to this event
		/// handler class.
		/// </summary>
		public ClickDelegate Click;

		/// <summary>
		/// Used to handle <see cref="PNode.DoubleClick">DoubleClick</see> events sent
		/// to this event handler class.
		/// </summary>
		public DoubleClickDelegate DoubleClick;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseDown">MouseDown</see> events sent to
		/// this event handler class.
		/// </summary>
		public MouseDownDelegate MouseDown;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseUp">MouseUp</see> events sent to this
		/// event handler class.
		/// </summary>
		public MouseUpDelegate MouseUp;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseMove">MouseMove</see> sent to this event
		/// handler class.
		/// </summary>
		public MouseMoveDelegate MouseMove;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseDrag">MouseDrag</see> events sent to
		/// this event handler class.
		/// </summary>
		public MouseDragDelegate MouseDrag;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseEnter">MouseEnter</see> events sent to
		/// this event handler class.
		/// </summary>
		public MouseEnterDelegate MouseEnter;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseLeave">MouseLeave</see> events sent to
		/// this event handler class.
		/// </summary>
		public MouseLeaveDelegate MouseLeave;

		/// <summary>
		/// Used to handle <see cref="PNode.MouseWheel">MouseWheel</see> events sent to
		/// this event handler class.
		/// </summary>
		public MouseWheelDelegate MouseWheel;

		/// <summary>
		/// Used to handle <see cref="PNode.DragEnter">DragEnter</see> events sent to this
		/// event handler class.
		/// </summary>
		public DragEnterDelegate DragEnter;

		/// <summary>
		/// Used to handle <see cref="PNode.DragLeave">DragLeave</see> events sent to this
		/// event handler class.
		/// </summary>
		public DragLeaveDelegate DragLeave;

		/// <summary>
		/// Used to handle <see cref="PNode.KeyPress">DragOver</see> events sent to this
		/// event handler class.
		/// </summary>
		public DragOverDelegate DragOver;
	
		/// <summary>
		/// Used to handle <see cref="PNode.DragDrop">DragDrop</see> events sent to this
		/// event handler class.
		/// </summary>
		public DragDropDelegate DragDrop;

		/// <summary>
		/// Used to handle <see cref="PNode.GotFocus">GotFocus</see> events sent to this
		/// event handler class.
		/// </summary>
		public GotFocusDelegate GotFocus;

		/// <summary>
		/// Used to handle <see cref="PNode.LostFocus">LostFocus</see> events sent to
		/// this event handler class.
		/// </summary>
		public LostFocusDelegate LostFocus;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PBasicInputEventHandler.
		/// </summary>
		public PBasicInputEventHandler() {
			this.AcceptsEvent = new AcceptsEventDelegate(PBasicInputEventHandlerAcceptsEvent);
		}
		#endregion

		#region Event Filter
		//****************************************************************
		// Event Filter - All event listeners can be associated with event
		// filters.  An event filter is simply a callback that either
		// accepts or rejects events.  Inheriters can override
		// DoesAcceptEvent and filter out undesirable events there.  Or,
		// the AcceptsEvent delegate can be set directly to a method that
		// filters events.
		//****************************************************************

		/// <summary>
		/// Returns true if the filter accepts the given event and false otherwise.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>True if the filter accepts the event; otherwise, false.</returns>
		public virtual bool DoesAcceptEvent(PInputEventArgs e) {
			if (this.AcceptsEvent != null) return AcceptsEvent(e);
			return true;
		}

		/// <summary>
		/// The filter for a PBasicInputEventHandler.  This method only rejects an event
		/// if it has already been marked as handled.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>True if the event has not been handled; otherwise, false.</returns>
		protected virtual bool PBasicInputEventHandlerAcceptsEvent(PInputEventArgs e) {
			return !e.Handled;
		}
		#endregion

		#region Event Handlers
		//****************************************************************
		// Events - Methods for handling events sent to the event listener.
		// Subclasses will want to override these methods to be notified
		// of events.
		//****************************************************************

		/// <summary>
		/// Called when a <see cref="PNode.KeyDown">KeyDown</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnKeyDown(object sender, PInputEventArgs e) {
			if (KeyDown != null) {
				KeyDown(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.KeyPress">KeyPress</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnKeyPress(object sender, PInputEventArgs e) {
			if (KeyPress != null) {
				KeyPress(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.KeyUp">KeyUp</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnKeyUp(object sender, PInputEventArgs e) {
			if (KeyUp != null) {
				KeyUp(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.Click">Click</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnClick(object sender, PInputEventArgs e) {
			if (Click != null) {
				Click(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.DoubleClick">DoubleClick</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnDoubleClick(object sender, PInputEventArgs e) {
			if (DoubleClick != null) {
				DoubleClick(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseDown">MouseDown</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseDown(object sender, PInputEventArgs e) {
			if (MouseDown != null) {
				MouseDown(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseUp">MouseUp</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseUp(object sender, PInputEventArgs e) {
			if (MouseUp != null) {
				MouseUp(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseMove">MouseMove</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseMove(object sender, PInputEventArgs e) {
			if (MouseMove != null) {
				MouseMove(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseDrag">MouseDrag</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseDrag(object sender, PInputEventArgs e) {
			if (MouseDrag != null) {
				MouseDrag(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseEnter">MouseEnter</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseEnter(object sender, PInputEventArgs e) {
			if (MouseEnter != null) {
				MouseEnter(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseLeave">MouseLeave</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseLeave(object sender, PInputEventArgs e) {
			if (MouseLeave != null) {
				MouseLeave(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.MouseWheel">MouseWheel</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnMouseWheel(object sender, PInputEventArgs e) {
			if (MouseWheel != null) {
				MouseWheel(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.DragEnter">DragEnter</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnDragEnter(object sender, PInputEventArgs e) {
			if (DragEnter != null) {
				DragEnter(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.DragLeave">DragLeave</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnDragLeave(object sender, PInputEventArgs e) {
			if (DragLeave != null) {
				DragLeave(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.DragOver">DragOver</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnDragOver(object sender, PInputEventArgs e) {
			if (DragOver != null) {
				DragOver(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.DragDrop">DragDrop</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnDragDrop(object sender, PInputEventArgs e) {
			if (DragDrop != null) {
				DragDrop(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.GotFocus">GotFocus</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnGotFocus(object sender, PInputEventArgs e) {
			if (GotFocus != null) {
				GotFocus(sender, e);
			}
		}

		/// <summary>
		/// Called when a <see cref="PNode.LostFocus">LostFocus</see> event is sent to this
		/// listener.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnLostFocus(object sender, PInputEventArgs e) {
			if (LostFocus != null) {
				LostFocus(sender, e);
			}
		}
		#endregion
		
		#region Debugging
		//****************************************************************
		// Debugging - Methods for debugging.
		//****************************************************************

		/// <summary>
		/// Overridden.  Returns a string representation of this object for debugging
		/// purposes.
		/// </summary>
		/// <returns>A string representation of this object.</returns>
		public override string ToString() {
			return base.ToString() + "[" + ParamString + "]";
		}

		/// <summary>
		/// Returns a string representing the state of this object.
		/// </summary>
		/// <value>A string representing the state of this object.</value>
		/// <remarks>
		/// This method is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected virtual String ParamString {
			get {
				StringBuilder result = new StringBuilder();
				result.Append("AcceptsEvent=" + (AcceptsEvent == null ? "null" : AcceptsEvent.ToString()));
				return result.ToString();
			}
		}
		#endregion
	}
}
