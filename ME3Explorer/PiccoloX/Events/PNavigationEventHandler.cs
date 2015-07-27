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
using System.Collections;
using System.Windows.Forms;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Activities;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX.Util;

namespace UMD.HCIL.PiccoloX.Events {
	/// <summary>
	/// <b>PNavigationEventHandler</b> implements simple focus based navigation.
	/// </summary>
	/// <remarks>
	/// This event handler uses mouse button one or the arrow keys to set a new focus.
	/// It then animates the canvas view to keep the focus node on the screen and at
	/// 100 percent scale with minimal view movement.
	/// </remarks>
	public class PNavigationEventHandler : PBasicInputEventHandler {
		#region Fields
		private static Hashtable NODE_TO_GLOBAL_NODE_CENTER_MAPPING = new Hashtable();
		private const int DEFAULT_ZOOM_TO_FOCUS_DURATION = 500;

		private int zoomToFocusDuration = DEFAULT_ZOOM_TO_FOCUS_DURATION;
		private PNode focusNode;
		private PActivity navigationActivity;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PNavigationEventHandler.
		/// </summary>
		public PNavigationEventHandler() {
		}
		#endregion

		#region Event Filter
		/// <summary>
		/// Overridden.  Accepts left mouse button events and non-mouse events.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the event is a left mouse button event or non-mouse event; otherwise,
		/// false.
		/// </returns>
		public override bool DoesAcceptEvent(PInputEventArgs e) {
			if (base.DoesAcceptEvent (e)) {
				if (e.IsMouseEvent) {
					if (e.Button == MouseButtons.Left) {
						return true;
					}
				}
				else return true;
			}
			return false;
		}
		#endregion

		#region Focus Change Events
		//****************************************************************
		// Focus Change Events - Methods for handling events that change
		// the focused node.
		//****************************************************************

		/// <summary>
		/// Overridden.  Handles key events that move the focus.
		/// </summary>
		/// <param name="sender">The source of the PInputEvent.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// If an arrow key was pressed, this method moves the focus to a node in the
		/// appropriate direction.  The Page Up and Page Down keys act just like the Up
		/// and Down arrow keys respectively.  However when the Alt modifier is pressed,
		/// Page Up will move the focus to the parent of the current focus and Page Down
		/// will move the focus to the nearest child.
		/// </remarks>
		public override void OnKeyDown(object sender, PInputEventArgs e) {
			base.OnKeyDown(sender, e);
			PNode oldLocation = focusNode;

			switch (e.KeyCode) {
				case Keys.Left:
					MoveFocusLeft(e);
					break;

				case Keys.Right:
					MoveFocusRight(e);
					break;

				case Keys.Up:
				case Keys.PageUp:
					if ((e.Modifiers & Keys.Alt) == Keys.Alt) {
						MoveFocusOut(e);
					} else {
						MoveFocusUp(e);
					}
					break;

				case Keys.Down:
				case Keys.PageDown:
					if ((e.Modifiers & Keys.Alt) == Keys.Alt) {
						MoveFocusIn(e);
					} else {
						MoveFocusDown(e);
					}
					break;
			}

			if (focusNode != null && oldLocation != focusNode) {
				DirectCameraViewToFocus(e.Camera, focusNode, 500);
			}
		}

		/// <summary>
		/// Overridden.  Moves the focus to the node under the cursor.
		/// </summary>
		/// <param name="sender">The source of the PInputEvent.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public override void OnMouseDown(object sender, PInputEventArgs e) {
			base.OnMouseDown(sender, e);
			MoveFocusToMouseOver(e);
		
			if (focusNode != null) {
				DirectCameraViewToFocus(e.Camera, focusNode, zoomToFocusDuration);
				e.InputManager.KeyboardFocus = e.Path;
			}
		}
		#endregion

		#region Focus Movement
		//****************************************************************
		// Focus Movement - Moves the focus in the specified direction.  Left,
		// right, up, and down mean move the focus to the closest sibling of the 
		// current focus node that exists in that direction.  Move in means
		// move the focus to a child of the current focus.  And, move out means
		// move the focus to the parent of the current focus.
		//****************************************************************

		/// <summary>
		/// Moves the focus to the nearest node below this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusDown(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.South);

			if (n != null) {
				focusNode = n;
			}
		}
	
		/// <summary>
		/// Moves the focus to the nearest child of this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusIn(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.In);

			if (n != null) {
				focusNode = n;
			}		
		}
	
		/// <summary>
		/// Moves the focus to the nearest node left of this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusLeft(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.West);

			if (n != null) {
				focusNode = n;
			}
		}
	
		/// <summary>
		/// Moves the focus to the nearest ancestor of this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusOut(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.Out);

			if (n != null) {
				focusNode = n;
			}		
		}
	
		/// <summary>
		/// Moves the focus to the nearest node right of this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusRight(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.East);

			if (n != null) {
				focusNode = n;
			}		
		}
		
		/// <summary>
		/// Moves the focus to the nearest node above this node.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusUp(PInputEventArgs e) {
			PNode n = GetNeighborInDirection(Direction.North);

			if (n != null) {
				focusNode = n;
			}
		}

		/// <summary>
		/// Moves the focus to the node under the cursor.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void MoveFocusToMouseOver(PInputEventArgs e) {
			PNode focus = e.PickedNode;
			if (!(focus is PCamera)) {
				focusNode = focus;
			}
		}

		/// <summary>
		/// Gets the nearest neighbor in the specified direction.
		/// </summary>
		/// <param name="aDirection">
		/// The direction in which to find the nearest neighbor.
		/// </param>
		/// <returns>The nearest neighbor in the specified direction.</returns>
		public virtual PNode GetNeighborInDirection(Direction aDirection) {
			if (focusNode == null) return null;

			NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Clear();

			PointF highlightCenter = PUtil.CenterOfRectangle(focusNode.GlobalFullBounds);
			NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Add(focusNode, highlightCenter);

			PNodeList l = GetNeighbors();
			SortNodesByDistanceFromPoint(l, highlightCenter);

			foreach (PNode each in l) {
				if (NodeIsNeighborInDirection(each, aDirection)) {
					return each;
				}
			}

			return null;
		}

		/// <summary>
		/// Get a list of all neighbors (parent, siblings and children).
		/// </summary>
		/// <returns>A list of all neighbors.</returns>
		public virtual PNodeList GetNeighbors() {
			PNodeList result = new PNodeList();
		
			if (focusNode == null) return result;
			if (focusNode.Parent == null) return result;

			PNode focusParent = focusNode.Parent;

			PNodeList focusParentChildren = focusParent.ChildrenReference;
			foreach (PNode each in focusParentChildren) {
				if (each != focusNode && each.Pickable) {
					result.Add(each);
				}
			}

			result.Add(focusParent);

			PNodeList focusChildren = focusNode.ChildrenReference;
			foreach(PNode each in focusChildren) {
				result.Add(each);
			}

			return result;
		}

		/// <summary>
		/// Returns true if the given node is a neighbor of the current focus in the
		/// specified direction.
		/// </summary>
		/// <param name="aNode">The node to test.</param>
		/// <param name="aDirection">The direction in which to perform the test.</param>
		/// <returns>
		/// True if the given node is a neighbor in the specified direction; otherwise,
		/// false.
		/// </returns>
		public virtual bool NodeIsNeighborInDirection(PNode aNode, Direction aDirection) {
			switch (aDirection) {
				case Direction.In: {
					return aNode.IsDescendentOf(focusNode);
				}

				case Direction.Out: {
					return aNode.IsAncestorOf(focusNode);
				}
			
				default: {
					if (aNode.IsAncestorOf(focusNode) || aNode.IsDescendentOf(focusNode)) {
						return false;
					}
					break;
				}
			}

			PointF highlightCenter = (PointF) NODE_TO_GLOBAL_NODE_CENTER_MAPPING[focusNode];
			PointF nodeCenter = (PointF) NODE_TO_GLOBAL_NODE_CENTER_MAPPING[aNode];

			float ytest1 = nodeCenter.X - highlightCenter.X + highlightCenter.Y;
			float ytest2 = -nodeCenter.X + highlightCenter.X + highlightCenter.Y;

			switch (aDirection) {
				case Direction.North: {
					if (nodeCenter.Y < highlightCenter.Y) {
						if (nodeCenter.Y < ytest1 && nodeCenter.Y < ytest2) {
							return true;
						}
					}
					break;
				}

				case Direction.East: {
					if (nodeCenter.X > highlightCenter.X) {
						if (nodeCenter.Y < ytest1 && nodeCenter.Y > ytest2) {
							return true;
						}
					}
					break;
				}

				case Direction.South: {
					if (nodeCenter.Y > highlightCenter.Y) {
						if (nodeCenter.Y > ytest1 && nodeCenter.Y > ytest2) {
							return true;
						}
					}
					break;
				}
				case Direction.West: {
					if (nodeCenter.X < highlightCenter.X) {
						if (nodeCenter.Y > ytest1 && nodeCenter.Y < ytest2) {
							return true;
						}
					}
					break;
				}
			}
			return false;
		}

		/// <summary>
		/// Sorts the given list of nodes by their distance from the given point.  Nodes
		/// closest to the point will be placed first in the list.
		/// </summary>
		/// <param name="aNodesList">The list to sort.</param>
		/// <param name="aPoint">The point to use for the comparison.</param>
		public virtual void SortNodesByDistanceFromPoint(PNodeList aNodesList, PointF aPoint) {
			aNodesList.Sort(new DistanceFromPointComparer(aPoint));
		}

		/// <summary>
		/// A comparer class used by <see cref="SortNodesByDistanceFromPoint"/>.
		/// </summary>
		/// <remarks>
		/// This comparer is used to sort nodes by their distance from the specified point.
		/// </remarks>
		class DistanceFromPointComparer : IComparer {
			PointF point;

			/// <summary>
			/// Constructs a new DistanceFromPointComparer with the given point.
			/// </summary>
			/// <param name="aPoint">The point to use for the comparison.</param>
			public DistanceFromPointComparer(PointF aPoint) {
				point = aPoint;
			}

			/// <summary>
			/// Compares two nodes and returns a value indicating whether the first node's
			/// distance to the point is less than, equal to or greater than the second
			/// node's distance to the specified point.
			/// </summary>
			/// <param name="o1">The first node to compare.</param>
			/// <param name="o2">The second node to compare.</param>
			/// <returns>
			/// Less than 0, if o1's distance to the point is less than o2's distance to the
			/// point; 0 if o1's distance to the point equals 02's distance to the point;
			/// greater than 0 if o1's distance to the point is greater than o2's distance to
			/// the point.
			/// </returns>
			public int Compare(object o1, object o2) {
				PNode each1 = (PNode) o1;
				PNode each2 = (PNode) o2;
				PointF each1Center = PUtil.CenterOfRectangle(each1.GlobalFullBounds);
				PointF each2Center = PUtil.CenterOfRectangle(each2.GlobalFullBounds);

				if (!NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Contains(each1))
					NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Add(each1, each1Center);
				if (!NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Contains(each2))
					NODE_TO_GLOBAL_NODE_CENTER_MAPPING.Add(each2, each2Center);

				float distance1 = PUtil.DistanceBetweenPoints(point, each1Center);
				float distance2 = PUtil.DistanceBetweenPoints(point, each2Center);

				if (distance1 < distance2) {
					return -1;
				} else if (distance1 == distance2) {
					return 0;
				} else {
					return 1;
				}
			}
		}
		#endregion

		#region Canvas Movement
		//****************************************************************
		// Canvas Movement - The canvas view is updated so that the current
		// focus remains visible on the screen at 100 percent scale.
		//****************************************************************

		/// <summary>
		/// Gets or sets the duration for animating to a new focus node.
		/// </summary>
		/// <value>The duration for animating to a new focus node.</value>
		public virtual int ZoomToFocusDuration {
			get { return zoomToFocusDuration; }
			set { zoomToFocusDuration = value; }
		}

		/// <summary>
		/// Animate the camera's view matrix from its current value when the activity
		/// starts to the new destination matrix value.
		/// </summary>
		/// <param name="aCamera">The camera whose view matrix will be animated.</param>
		/// <param name="aMatrix">The final matrix value.</param>
		/// <param name="duration">
		/// The amount of time that the animation should take.
		/// </param>
		/// <returns>
		/// The newly scheduled activity, if the duration is greater than 0; else null.
		/// </returns>
		protected virtual PActivity AnimateCameraViewMatrixTo(PCamera aCamera, PMatrix aMatrix, int duration) {
			bool wasOldAnimation = false;
		
			// first stop any old animations.
			if (navigationActivity != null) {
				navigationActivity.Terminate();
				wasOldAnimation = true;
			}
			
			if (duration == 0) {
				aCamera.ViewMatrix = aMatrix;
				return null;
			}

			PMatrix source = aCamera.ViewMatrixReference;

			if (!source.MatrixReference.Equals(aMatrix.MatrixReference)) {
				navigationActivity = aCamera.AnimateViewToMatrix(aMatrix, duration);
				((PTransformActivity)navigationActivity).SlowInSlowOut = !wasOldAnimation;
				return navigationActivity;			
			}
		
			return null;
		}

		/// <summary>
		/// Animates the camera's view to keep the focus node on the screen and at 100
		/// percent scale with minimal view movement.
		/// </summary>
		/// <param name="aCamera">The camera whose view will be animated.</param>
		/// <param name="aFocusNode">The focus node to animate to.</param>
		/// <param name="duration">The length of the animation.</param>
		/// <returns>
		/// The activity that animates the camera's view to the focus node.
		/// </returns>
		public virtual PActivity DirectCameraViewToFocus(PCamera aCamera, PNode aFocusNode, int duration) {
			PMatrix originalViewMatrix = aCamera.ViewMatrix;

			// Scale the canvas to include
			SizeF s = new SizeF(1, 0);
			s = focusNode.GlobalToLocal(s);
		
			float scaleFactor = s.Width / aCamera.ViewScale;
			PointF scalePoint = PUtil.CenterOfRectangle(focusNode.GlobalFullBounds);
			if (scaleFactor != 1) {
				aCamera.ScaleViewBy(scaleFactor, scalePoint.X, scalePoint.Y);
			}
		
			// Pan the canvas to include the view bounds with minimal canvas
			// movement.
			aCamera.AnimateViewToPanToBounds(focusNode.GlobalFullBounds, 0);

			// Get rid of any white space. The canvas may be panned and
			// zoomed in to do this. But make sure not stay constrained by max
			// magnification.
			//FillViewWhiteSpace(aCamera);

			PMatrix resultingMatrix = aCamera.ViewMatrix;
			aCamera.ViewMatrix = originalViewMatrix;

			// Animate the canvas so that it ends up with the given
			// view transform.
			return AnimateCameraViewMatrixTo(aCamera, resultingMatrix, duration);
		}

		/// <summary>
		/// Removes any white space.  The canvas may be panned and zoomed in to do this.
		/// </summary>
		/// <param name="aCamera">The camera whose view will be adjusted.</param>
		protected virtual void FillViewWhiteSpace(PCamera aCamera) {
			RectangleF rootBounds = aCamera.Root.FullBounds;
			RectangleF viewBounds = aCamera.ViewBounds;

			if (!rootBounds.Contains(aCamera.ViewBounds)) {
				aCamera.AnimateViewToPanToBounds(rootBounds, 0);
				aCamera.AnimateViewToPanToBounds(focusNode.GlobalFullBounds, 0);

				// center content.
				float dx = 0;
				float dy = 0;
				viewBounds = aCamera.ViewBounds;

				if (viewBounds.Width > rootBounds.Width) {   // then center along x axis.
					float rootBoundsMinX = Math.Min(rootBounds.X, rootBounds.Right);
					float viewBoundsMinX = Math.Min(viewBounds.X, viewBounds.Right);
					float boundsCenterX = rootBoundsMinX + (rootBounds.Width / 2);
					float viewBoundsCenterX = viewBoundsMinX + (viewBounds.Width / 2);
					dx = viewBoundsCenterX - boundsCenterX;
				}

				if (viewBounds.Height > rootBounds.Height) { // then center along y axis.
					float rootBoundsMinY = Math.Min(rootBounds.Y, rootBounds.Right);
					float viewBoundsMinY = Math.Min(viewBounds.Y, viewBounds.Right);
					float boundsCenterY = rootBoundsMinY + (rootBounds.Height / 2);
					float viewBoundsCenterY = viewBoundsMinY + (viewBounds.Height / 2);
					dy = viewBoundsCenterY - boundsCenterY;
				}
				aCamera.TranslateViewBy(dx, dy);
			}
		}
		#endregion
	}
}