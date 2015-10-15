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
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX.Util;

namespace UMD.HCIL.PiccoloX.Handles {
	#region Delegates
	/// <summary>
	/// A delegate used to notify classes of the start of a new handle drag sequence.
	/// </summary>
	public delegate bool StartHandleDragDelegate(object sender, PointF point, PInputEventArgs e);

	/// <summary>
	/// A delegate used to notify classes of drag events in a handle drag sequence.
	/// </summary>
	public delegate void HandleDragDelegate(object sender, SizeF size, PInputEventArgs e);

	/// <summary>
	/// A delegate used to notify classes of the end of a handle drag sequence.
	/// </summary>
	public delegate void EndHandleDragDelegate(object sender, PointF point, PInputEventArgs e);
	#endregion

	/// <summary>
	/// <b>PHandle</b> is used to modify some aspect of Piccolo when it is dragged.
	/// </summary>
	/// <remarks>
	/// Each handle has a <see cref="PLocator"/> that it uses to automatically position itself.
	/// See <see cref="PBoundsHandle"/> for an example of a handle that resizes the bounds
	/// of another node.
	/// </remarks>
	[Serializable]
	public class PHandle : PPath, ISerializable {
		#region Fields
		/// <summary>
		/// The default width and height of a handle.
		/// </summary>
		public static float DEFAULT_HANDLE_SIZE = 8;

		/// <summary>
		/// The default color of a handle.
		/// </summary>
		public static Color DEFAULT_COLOR = Color.White;

		private static PMatrix TEMP_MATRIX = new PMatrix();
	
		private PLocator locator;

		[NonSerialized] private PInputEventListener handleDragger;

		/// <summary>
		/// Used to notify classes of the start of a new handle drag sequence.
		/// </summary>
		public StartHandleDragDelegate StartHandleDrag;

		/// <summary>
		/// Used to notify classes of drag events in a handle drag sequence.
		/// </summary>
		public HandleDragDelegate HandleDrag;

		/// <summary>
		/// Used to notify classes of the end of a handle drag sequence.
		/// </summary>
		public EndHandleDragDelegate EndHandleDrag;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new handle that will use the given locator to locate itself
		/// on its parent node.
		/// </summary>
		/// <param name="aLocator">
		/// The locator used by this handle to locate itself on its parent node.
		/// </param>
		public PHandle(PLocator aLocator) {
			AddPath(CreatePath, false);
			locator = aLocator;
			Brush = new SolidBrush(DEFAULT_COLOR);
			InstallHandleEventHandlers();
		}

		/// <summary>
		/// Creates the path that will represent this handle.
		/// </summary>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses should override this method to use a specialized
		/// path for the handle.
		/// </remarks>
		public virtual GraphicsPath CreatePath {
			get {
				GraphicsPath path = new GraphicsPath();
				path.AddEllipse(0f, 0f, DEFAULT_HANDLE_SIZE, DEFAULT_HANDLE_SIZE);
				return path;
			}
		}
		#endregion

		#region Handle Dragging
		//****************************************************************
		// Handle Dragging - These are the methods the subclasses should
		// normally override to give a handle unique behavior.
		//****************************************************************

		/// <summary>
		/// Subclasses should override this method to get notified when the handle starts
		/// to get dragged.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="point">The drag position relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnStartHandleDrag(object sender, PointF point, PInputEventArgs e) {
			if (StartHandleDrag != null) {
				StartHandleDrag(sender, point, e);
			}
		}

		/// <summary>
		/// Subclasses should override this method to get notified as the handle is dragged.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="size">The drag delta relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnHandleDrag(object sender, SizeF size, PInputEventArgs e) {
			if (HandleDrag != null) {
				HandleDrag(sender, size, e);
			}
		}

		/// <summary>
		/// Subclasses should override this method to get notified when the handle stops
		/// getting dragged.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="point">The drag position relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void OnEndHandleDrag(object sender, PointF point, PInputEventArgs e) {
			if (EndHandleDrag != null) {
				EndHandleDrag(sender, point, e);
			}
		}

		/// <summary>
		/// Adds the event handler that will be responsible for the drag handle interaction.
		/// </summary>
		protected virtual void InstallHandleEventHandlers() {
			handleDragger = new HandleDragEventHandler(this);
			AddInputEventListener(handleDragger);
		}

		/// <summary>
		/// <b>HandleDragEventHandler</b> is the event handler responsible for the drag handle
		/// interaction.
		/// </summary>
		/// <remarks>
		/// This event handler will delegate drag events to this handle's <c>OnHandleDrag</c>
		/// methods.
		/// </remarks>
		private class HandleDragEventHandler : PDragSequenceEventHandler {
			private PHandle handle;

			/// <summary>
			/// Constructs a new HandleDragEventHandler.
			/// </summary>
			/// <param name="handle">
			/// The node that this event handler will handle drag events for.
			/// </param>
			public HandleDragEventHandler(PHandle handle) {
				this.handle = handle;
			}

			/// <summary>
			/// Overridden.  See <see cref="PBasicInputEventHandler.DoesAcceptEvent">
			/// PBasicInputEventHandler.DoesAcceptEvent</see>.
			/// </summary>
			public override bool DoesAcceptEvent(PInputEventArgs e) {
				bool validType = (e.Type != PInputType.MouseEnter && e.Type != PInputType.MouseLeave && e.Type != PInputType.MouseMove);
				bool validButton = (e.IsMouseEvent && e.Button == System.Windows.Forms.MouseButtons.Left);

				if (base.DoesAcceptEvent(e) && validType && validButton) {
					e.Handled = true;
					return true;
				}
				return false;
			}

			/// <summary>
			/// Overridden.  See <see cref="PDragSequenceEventHandler.OnStartDrag">
			/// PDragSequenceEventHandler.OnStartDrag</see>.
			/// </summary>
			protected override void OnStartDrag(object sender, PInputEventArgs e) {
				base.OnStartDrag (sender, e);
				handle.OnStartHandleDrag(sender, e.GetPositionRelativeTo(handle), e);
			}

			/// <summary>
			/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDrag">
			/// PDragSequenceEventHandler.OnDrag</see>.
			/// </summary>
			protected override void OnDrag(object sender, PInputEventArgs e) {
				base.OnDrag (sender, e);
				SizeF aDelta = e.GetDeltaRelativeTo(handle); 	
				if (aDelta.Width != 0 || aDelta.Height != 0) {
					handle.OnHandleDrag(sender, aDelta, e);
				}
			}

			/// <summary>
			/// Overridden.  See <see cref="PDragSequenceEventHandler.OnEndDrag">
			/// PDragSequenceEventHandler.OnEndDrag</see>.
			/// </summary>
			protected override void OnEndDrag(object sender, PInputEventArgs e) {
				base.OnEndDrag (sender, e);
				handle.OnEndHandleDrag(sender, e.GetPositionRelativeTo(handle), e);
			}
		}

		/// <summary>
		/// Gets the event handler that is responsible for the drag handle interaction.
		/// </summary>
		/// <value>The event handler responsible for the drag handle interaction.</value>
		public virtual PInputEventListener HandleDragHandler {
			get { return this.handleDragger; }
		}

		/// <summary>
		/// Gets or sets the locator that this handle uses to position itself on its parent
		/// node.
		/// </summary>
		/// <value>
		/// The locator that this handle uses to position itself on its parent node.
		/// </value>
		public virtual PLocator Locator {
			get { return locator; }
			set {
				locator = value;
				InvalidatePaint();
				RelocateHandle();
			}
		}
		#endregion

		#region Layout
		//****************************************************************
		// Layout - When a handle's parent's layout changes the handle
		// invalidates its own layout and then repositions itself on its
		// parents bounds using its locator to determine that new
		// position.
		//****************************************************************

		/// <summary>
		/// Overridden.  Relocate the handle whenever the parent changes.
		/// </summary>
		public override PNode Parent {
			set {
				base.Parent = value;
				RelocateHandle();
			}
		}

		/// <summary>
		/// Overridden.  Relocate the handle whenever the parent's bounds change.
		/// </summary>
		public override void ParentBoundsChanged() {
			RelocateHandle();
		}

		/// <summary>
		/// Force this handle to relocate itself using its locator.
		/// </summary>
		public virtual void RelocateHandle() {
			if (locator != null) {
				RectangleF b = Bounds;
				PointF aPoint = locator.LocatePoint;

				if (locator is PNodeLocator) {
					PNode located = ((PNodeLocator)locator).Node;
					PNode parent = Parent;
				
					aPoint = located.LocalToGlobal(aPoint);
					aPoint = GlobalToLocal(aPoint);
				
					if (parent != located && parent is PCamera) {
						aPoint = ((PCamera)parent).ViewToLocal(aPoint);
					}
				}
			
				float newCenterX = aPoint.X;
				float newCenterY = aPoint.Y;

				PointF bCenter = PUtil.CenterOfRectangle(b);

				if (newCenterX != bCenter.X ||
					newCenterY != bCenter.Y) {
					CenterBoundsOnPoint(newCenterX, newCenterY);
				}
			}
		}
		#endregion

		#region Serialization
		//****************************************************************
		// Serialization - Nodes conditionally serialize their parent.
		// This means that only the parents that were unconditionally
		// (using GetObjectData) serialized by someone else will be restored
		// when the node is deserialized.
		//****************************************************************

		/// <summary>
		/// Read this PHandle and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">The StreamingContext of this serialization operation.</param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PHandle(SerializationInfo info, StreamingContext context) :
			base(info, context) {

			InstallHandleEventHandlers();
		}

		/// <summary>
		/// Write this PHandle and all of its descendent nodes to the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to write to.</param>
		/// <param name="context">The streaming context of this serialization operation.</param>
		/// <remarks>
		/// This node's parent is written out conditionally, that is it will only be written out
		/// if someone else writes it out unconditionally.
		/// </remarks>
		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData (info, context);
		}
		#endregion
	}
}