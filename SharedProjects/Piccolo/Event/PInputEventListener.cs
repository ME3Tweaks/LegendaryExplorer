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

// included to avoid specifying fully qualified paths in comment links.

namespace Piccolo.Event {
	/// <summary>
	/// <b>PInputEventListener</b> defines an interface for objects that want to listen to
	/// PNodes for input events.  If you are just using Piccolo's default input management
	/// system then you will most often use PBasicInputEventHandler to register with a node
	/// for input events. 
	/// </summary>
	public interface PInputEventListener {
		/// <summary>
		/// Returns true if the filter accepts the given event and false otherwise.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>True if the filter accepts the event; otherwise, false.</returns>
		bool DoesAcceptEvent(PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.KeyDown">KeyDown</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnKeyDown(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.KeyPress">KeyPress</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnKeyPress(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.KeyUp">KeyUp</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnKeyUp(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.Click">Click</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnClick(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.DoubleClick">DoubleClick</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnDoubleClick(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseDown">MouseDown</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseDown(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseDrag">MouseDrag</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseDrag(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseUp">MouseUp</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseUp(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseMove">MouseMove</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseMove(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseEnter">MouseEnter</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseEnter(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseLeave">MouseLeave</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseLeave(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.MouseWheel">MouseWheel</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnMouseWheel(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.DragEnter">DragEnter</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnDragEnter(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.DragLeave">DragLeave</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnDragLeave(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.DragOver">DragOver</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnDragOver(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.DragDrop">DragDrop</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnDragDrop(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.GotFocus">GotFocus</see> event is sent to te
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnGotFocus(object sender, PInputEventArgs e);

		/// <summary>
		/// Called when a <see cref="PNode.LostFocus">LostFocus</see> event is sent to the
		/// listener that implements this interface.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		void OnLostFocus(object sender, PInputEventArgs e);
	}
}