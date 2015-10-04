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
using System.Windows.Forms;
using System.Drawing;
using System.Text;

namespace UMD.HCIL.Piccolo.Event {
	/// <summary>
	/// <b>PDragEventHandler</b> is a simple event handler for dragging a
	/// node on the canvas.
	/// </summary>
	public class PDragEventHandler : PDragSequenceEventHandler {
		#region Fields
		private PNode draggedNode;
		private bool moveToFrontOnPress = false;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PDragEventhandler.
		/// </summary>
		public PDragEventHandler() {
			this.AcceptsEvent = new AcceptsEventDelegate(PDragEventHandlerAcceptsEvent);
		}
		#endregion

		#region Dragging
		/// <summary>
		/// The filter for a PDragEventHandler.  This method only accepts left mouse button
		/// events that have not yet been handled.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the event is an unhandled left mouse button event; otherwise, false.
		/// </returns>
		protected virtual bool PDragEventHandlerAcceptsEvent(PInputEventArgs e) {
			if (!e.Handled && e.IsMouseEvent && e.Button == MouseButtons.Left) {
				return true;
			}
			return false;
		}

		/// <summary>
		/// Gets or sets the node being dragged.
		/// </summary>
		/// <value>The node being dragged.</value>
		protected virtual PNode DraggedNode {
			get { return draggedNode; }
			set { draggedNode = value; }
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.ShouldStartDragInteraction">
		/// PDragSequenceEventHandler.ShouldStartDragInteraction</see>.
		/// </summary>
		protected override bool ShouldStartDragInteraction(PInputEventArgs e) {
			if (base.ShouldStartDragInteraction(e)) {
				return e.PickedNode != e.TopCamera;
			}
			return false;
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnStartDrag">
		/// PDragSequenceEventHandler.OnStartDrag</see>.
		/// </summary>
		protected override void OnStartDrag(object sender, PInputEventArgs e) {
			base.OnStartDrag(sender, e); 	
			draggedNode = e.PickedNode;
			if (moveToFrontOnPress) {
				draggedNode.MoveToFront();
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDrag">
		/// PDragSequenceEventHandler.OnDrag</see>.
		/// </summary>
		protected override void OnDrag(object sender, PInputEventArgs e) {
			base.OnDrag(sender, e);
			SizeF s = e.GetDeltaRelativeTo(draggedNode);
			s = draggedNode.LocalToParent(s);
			draggedNode.OffsetBy(s.Width, s.Height);
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnEndDrag">
		/// PDragSequenceEventHandler.OnEndDrag</see>.
		/// </summary>
		protected override void OnEndDrag(object sender, PInputEventArgs e) {
			base.OnEndDrag(sender, e);
			draggedNode = null;
		}	

		/// <summary>
		/// Gets or sets a value indicating whether or not a node should move to the
		/// front in the z-order at the beginning of a drag operation.
		/// </summary>
		public virtual bool MoveToFrontOnPress {
			get { return moveToFrontOnPress; }
			set { moveToFrontOnPress = value; }
		}
		#endregion
		
		#region Debugging
		//****************************************************************
		// Debugging - Methods for debugging.
		//****************************************************************

		/// <summary>
		/// Overridden.  Returns a string representing the state of this object.
		/// </summary>
		/// <value>A string representing the state of this object.</value>
		/// <remarks>
		/// This method is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override String ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("draggedNode=" + (draggedNode == null ? "null" : draggedNode.ToString()));
				if (moveToFrontOnPress) result.Append(",moveToFrontOnPress");
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}