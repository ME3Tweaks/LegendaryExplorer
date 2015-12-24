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
using System.Windows.Forms;
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX.Util;

namespace UMD.HCIL.PiccoloX.Handles {
	/// <summary>
	/// <b>PBoundsHandle</b> is a handle for resizing the bounds of another node.
	/// </summary>
	/// <remarks>
	/// If a bounds handle is dragged such that the target node's width or height becomes
	/// negative then each drag handle's locator associated with that target node is
	/// "flipped" so that they are attached to and dragging a different corner of the
	/// node's bounds.
	/// </remarks>
	[Serializable]
	public class PBoundsHandle : PHandle {
		#region Fields
		[NonSerialized] private PBasicInputEventHandler handleCursorHandler;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PBoundsHandle that will use the given locator to locate itself
		/// on another node's bounds.
		/// </summary>
		/// <param name="locator">
		/// The locator used by this handle to locate itself on another node's bounds.
		/// </param>
		public PBoundsHandle(PLocator locator) : base(locator) {
		}
		#endregion

		#region Handle Management
		/// <summary>
		/// Adds bounds handles to the given node.
		/// </summary>
		/// <param name="aNode">The node to add bounds handles to.</param>
		public static void AddBoundsHandlesTo(PNode aNode) {
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateEastLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateWestLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthLocator(aNode)));
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthEastLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthWestLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthEastLocator(aNode))); 
			aNode.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthWestLocator(aNode))); 	
		}

		/// <summary>
		/// Adds sticky bounds handles (with respect to the given camera) to the specified node.
		/// </summary>
		/// <param name="aNode">The node to add sticky bounds handles to.</param>
		/// <param name="camera">The camera to stick the bounds handles to.</param>
		/// <remarks>
		/// Sticky bounds handles are not affected by the view transform of the camera.  That
		/// is, they will remain a constant size as the view is zoomed in and out.
		/// </remarks>
		public static void AddStickyBoundsHandlesTo(PNode aNode, PCamera camera) {
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateEastLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateWestLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthEastLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateNorthWestLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthEastLocator(aNode)));
			camera.AddChild(new PBoundsHandle(PBoundsLocator.CreateSouthWestLocator(aNode)));
		}

		/// <summary>
		/// Removes bounds handles from the given node.
		/// </summary>
		/// <param name="aNode">The node to remove the bounds handles from.</param>
		public static void RemoveBoundsHandlesFrom(PNode aNode) {
			PNodeList handles = new PNodeList();
			PNodeList children = aNode.ChildrenReference;
			foreach (PNode each in children) {
				if (each is PBoundsHandle) {
					handles.Add(each);
				}
			}
			aNode.RemoveChildren(handles);
		}
		#endregion

		#region Handle Cursor
		/// <summary>
		/// Overridden.  Adds the event handler that will be responsible for setting the mouse
		/// cursor when it enters/leaves this handle.
		/// </summary>
		protected override void InstallHandleEventHandlers() {
			base.InstallHandleEventHandlers ();			
			handleCursorHandler = new HandleCursorHandler(this);
			AddInputEventListener(handleCursorHandler);
		}

		/// <summary>
		/// <b>HandleCursorHandler</b> is the event handler that is responsible for setting the
		/// mouse cursor when it enters/leaves this handle.
		/// </summary>
		class HandleCursorHandler : PBasicInputEventHandler {
			PBoundsHandle target;
			bool cursorPushed;

			/// <summary>
			/// Constructs a new HandleCursorHandler.
			/// </summary>
			/// <param name="target">
			/// The node that this event handler will change the cursor for.
			/// </param>
			public HandleCursorHandler(PBoundsHandle target) {
				this.target = target;
			}

			/// <summary>
			/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseEnter">
			/// PBasicInputEventHandler.OnMouseEnter</see>.
			/// </summary>
			public override void OnMouseEnter(object sender, PInputEventArgs e) {
				base.OnMouseEnter(sender, e);
				if (!cursorPushed) {
					e.PushCursor(target.GetCursorFor(((PBoundsLocator)target.Locator).Side));
					cursorPushed = true;
				}
			}

			/// <summary>
			/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseLeave">
			/// PBasicInputEventHandler.OnMouseLeave</see>.
			/// </summary>
			public override void OnMouseLeave(object sender, PInputEventArgs e) {
				base.OnMouseLeave(sender, e);
				PPickPath focus = e.InputManager.MouseFocus;
				if (cursorPushed) {
					if (focus == null || focus.PickedNode != target) {
						e.PopCursor();
						cursorPushed = false;
					}
				}
			}

			/// <summary>
			/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseUp">
			/// PBasicInputEventHandler.OnMouseUp</see>.
			/// </summary>
			public override void OnMouseUp(object sender, PInputEventArgs e) {
				base.OnMouseUp(sender, e);
				if (cursorPushed) {
					e.PopCursor();
					cursorPushed = false;
				}
			}
		}

		/// <summary>
		/// Gets the event handler that is responsible for setting the mouse cursor when it
		/// enters/exits this handle.
		/// </summary>
		public virtual PBasicInputEventHandler HandleCursorEventHandler {
			get { return handleCursorHandler; }
		}

		/// <summary>
		/// Gets the appropriate cursor for the given direction.
		/// </summary>
		/// <param name="side">The direction for which to get the appropriate cursor.</param>
		/// <returns>The appropriate cursor for the given direction.</returns>
		public virtual Cursor GetCursorFor(Direction side) {
			switch (side) {
				case Direction.North:
				case Direction.South:
					return Cursors.SizeNS;

				case Direction.West:
				case Direction.East:
					return Cursors.SizeWE;
				
				case Direction.NorthWest:
				case Direction.SouthEast:
					return Cursors.SizeNWSE;

				case Direction.NorthEast:
				case Direction.SouthWest:
					return Cursors.SizeNESW;
				
			}
			return null;
		}
		#endregion

		#region Dragging
		/// <summary>
		/// Overridden.  Notifies the node whose bounds this handle is locating itself on that
		/// <c>SetBounds</c> will be repeatedly called.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="point">The drag position relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public override void OnStartHandleDrag(object sender, PointF point, PInputEventArgs e) {
			base.OnStartHandleDrag(sender, point, e);
			PBoundsLocator l = (PBoundsLocator) Locator;
			l.Node.StartResizeBounds();
		}

		/// <summary>
		/// Overridden.  Determines if this bounds handle or any of it's siblings need to be
		/// flipped.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="size">The drag delta relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// While dragging the bounds handles, the node being resized may cross over itself and
		/// become reversed (if it is dragged through zero-width or zero-height).  In this case,
		/// the locators for some of the bounds handles will have to be flipped to the opposite
		/// side.
		/// </remarks>
		public override void OnHandleDrag(object sender, SizeF size, PInputEventArgs e) {
			base.OnHandleDrag(sender, size, e);
			PBoundsLocator l = (PBoundsLocator) Locator;
				
			PNode n = l.Node;
			RectangleF b = n.Bounds;

			PNode parent = Parent;
			if (parent != n && parent is PCamera) {
				size = ((PCamera)parent).LocalToView(size);
			}

			size = LocalToGlobal(size);
			size = n.GlobalToLocal(size);
		
			float dx = size.Width;
			float dy = size.Height;
			
			switch (l.Side) {
				case Direction.North:
					b = new RectangleF(b.X, b.Y + dy, b.Width, b.Height - dy);
					break;
			
				case Direction.South:
					b = new RectangleF(b.X, b.Y, b.Width, b.Height + dy);
					break;
			
				case Direction.East:
					b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height);
					break;
			
				case Direction.West:
					b = new RectangleF(b.X + dx, b.Y, b.Width - dx, b.Height);
					break;
			
				case Direction.NorthWest:
					b = new RectangleF(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy);
					break;
			
				case Direction.SouthWest:
					b = new RectangleF(b.X + dx, b.Y, b.Width - dx, b.Height + dy);
					break;
			
				case Direction.NorthEast:
					b = new RectangleF(b.X, b.Y + dy, b.Width + dx, b.Height - dy);
					break;
			
				case Direction.SouthEast:
					b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height + dy);
					break;
			}

			bool flipX = false;
			bool flipY = false;
		
			if (b.Width < 0) {
				flipX = true;
				b.Width = -b.Width;
				b.X -= b.Width;
			}
		
			if (b.Height < 0) {
				flipY = true;
				b.Height = -b.Height;
				b.Y -= b.Height;
			}
		
			if (flipX || flipY) {
				FlipSiblingBoundsHandles(flipX, flipY);
			}
		
			n.Bounds = b;
		}

		/// <summary>
		/// Overridden.  Notifies the node whose bounds this handle is locating itself on that
		/// the resize bounds sequence is finisheed.
		/// </summary>
		/// <param name="sender">The source of this handle drag event.</param>
		/// <param name="point">The drag position relative to the handle.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public override void OnEndHandleDrag(object sender, PointF point, PInputEventArgs e) {
			base.OnEndHandleDrag(sender, point, e);
			PBoundsLocator l = (PBoundsLocator) Locator;
			l.Node.EndResizeBounds();
		}

		/// <summary>
		/// Flips this bounds handle or any if it's siblings if necessary.
		/// </summary>
		/// <param name="flipX">
		/// True if bounds handles should be flipped in the x-direction.
		/// </param>
		/// <param name="flipY">
		/// True if bounds handles should be flipped in the y-direction.
		/// </param>
		/// <remarks>
		/// While dragging the bounds handles, the node being resized may cross over itself and
		/// become reversed (if it is dragged through zero-width or zero-height).  In this case,
		/// the locators for some of the bounds handles will have to be flipped to the opposite
		/// side.
		/// </remarks>
		public virtual void FlipSiblingBoundsHandles(bool flipX, bool flipY) {
			PNodeList list = Parent.ChildrenReference;
			foreach (PNode each in list) {
				if (each is PBoundsHandle) {
					((PBoundsHandle)each).FlipHandleIfNeeded(flipX, flipY);
				}
			}
		}

		/// <summary>
		/// Flips this bounds handle if necessary.
		/// </summary>
		/// <param name="flipX">
		/// True if this bounds handle should be flipped in the x-direction.
		/// </param>
		/// <param name="flipY">
		/// True if this bounds handle should be flipped in the y-direction.
		/// </param>
		/// <remarks>
		/// While dragging the bounds handles, the node being resized may cross over itself and
		/// become reversed (if it is dragged through zero-width or zero-height).  In this case,
		/// the locators for some of the bounds handles will have to be flipped to the opposite
		/// side.
		/// </remarks>
		public virtual void FlipHandleIfNeeded(bool flipX, bool flipY) {		
			PBoundsLocator l = (PBoundsLocator) Locator;
		
			if (flipX || flipY) {
				switch (l.Side) {
					case Direction.North: {
						if (flipY) {
							l.Side = Direction.South;
						}					
						break;
					}
				
					case Direction.South: {
						if (flipY) {
							l.Side = Direction.North;
						}					
						break;
					}
				
					case Direction.East: {
						if (flipX) {
							l.Side = Direction.West;
						}					
						break;
					}
				
					case Direction.West: {
						if (flipX) {
							l.Side = Direction.East;
						}					
						break;
					}
								
					case Direction.NorthWest: {
						if (flipX && flipY) {
							l.Side = Direction.SouthEast;
						} else if (flipX) {
							l.Side = Direction.NorthEast;
						} else if (flipY) {
							l.Side = Direction.SouthWest;
						}
						break;
					}
				
					case Direction.SouthWest: {
						if (flipX && flipY) {
							l.Side = Direction.NorthEast;
						} else if (flipX) {
							l.Side = Direction.SouthEast;
						} else if (flipY) {
							l.Side = Direction.NorthWest;
						}
						break;
					}
				
					case Direction.NorthEast: {
						if (flipX && flipY) {
							l.Side = Direction.SouthWest;
						} else if (flipX) {
							l.Side = Direction.NorthWest;
						} else if (flipY) {
							l.Side = Direction.SouthEast;
						}
						break;
					}
				
					case Direction.SouthEast: {
						if (flipX && flipY) {
							l.Side = Direction.NorthWest;
						} else if (flipX) {
							l.Side = Direction.SouthWest;
						} else if (flipY) {
							l.Side = Direction.NorthEast;
						}
						break;
					}
				}
			}
		
			// reset locator to update layout
			Locator = l;
		}
		#endregion

		#region Serialization
		/// <summary>
		/// Read this PBoundsHandle and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">The StreamingContext of this serialization operation.</param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PBoundsHandle(SerializationInfo info, StreamingContext context) :
			base(info, context) {
		}
		#endregion
	}
}
