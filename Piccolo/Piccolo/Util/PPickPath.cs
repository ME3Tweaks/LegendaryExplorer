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
using System.ComponentModel;
using System.Drawing;
using System.Collections;

using UMD.HCIL.Piccolo.Event;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PPickPath</b> represents an ordered list of nodes that have been picked.
	/// </summary>
	/// <remarks>
	/// The topmost ancestor node is the last node in the list (and should be a camera), 
	/// the bottommost child node is at the front of the list.  It is this bottom node that 
	/// is given first chance to handle events, and that any active event handlers usually
	/// manipulate.
	/// <para>
	/// Note that because of layers (which can be picked by multiple camera's) the ordered
	/// list of nodes in a pick path do not all share a parent child relationship with the
	/// nodes in the list next to them.  This means that the normal LocalToGlobal methods don't
	/// work when trying to transform geometry up and down the pick path, instead you should
	/// use the pick path's CanvasToLocal methods to get the mouse event points into your local
	/// coord system.
	/// </para>
	/// <para>
	/// Note that <see cref="PInputEventArgs"/> wraps most of the useful PPickPath methods, so
	/// often you can use a <see cref="PInputEventArgs"/> directly instead of accessing its
	/// pick path.
	/// </para>
	/// </remarks>
	public class PPickPath {
		#region Fields
		/// <summary>
		/// The current PPickPath.
		/// </summary>
		public static PPickPath CURRENT_PICK_PATH;

		private Stack nodeStack;
		private Stack matrixStack;
		private Stack pickBoundsStack;
		private PCamera topCamera;
		private PCamera bottomCamera;
		private Hashtable excludedNodes;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPickPath.
		/// </summary>
		/// <param name="aCamera">The camera that originated the pick action.</param>
		/// <param name="aScreenPickBounds">The bounds being picked.</param>
		public PPickPath(PCamera aCamera, RectangleF aScreenPickBounds) {
			pickBoundsStack = new Stack();
			bottomCamera = null;
			topCamera = aCamera;
			nodeStack = new Stack();
			matrixStack = new Stack();
			pickBoundsStack.Push(aScreenPickBounds);

			CURRENT_PICK_PATH = this;
		}
		#endregion

		#region Picked Nodes
		//****************************************************************
		// Picked Nodes - Methods for accessing the nodes in the pick path.
		//****************************************************************

		/// <summary>
		/// Gets the current pick bounds.
		/// </summary>
		/// <value>The current pick bounds.</value>
		public virtual RectangleF PickBounds {
			get { return (RectangleF)pickBoundsStack.Peek(); }
		}

		/// <summary>
		/// Determines whether or not the specified node will be included when picking.
		/// </summary>
		/// <param name="node">The node to exclude or include.</param>
		/// <returns>True if the node should be included; otherwise, false.</returns>
		public virtual bool AcceptsNode(PNode node) {
			if (excludedNodes != null) {
				return !excludedNodes.ContainsKey(node);
			}
			return true;
		}

		/// <summary>
		/// Pushes the given node onto the node stack.
		/// </summary>
		/// <param name="aNode">The node to push.</param>
		public virtual void PushNode(PNode aNode) {
			nodeStack.Push(aNode);
		}

		/// <summary>
		/// Pops a node from the node stack.
		/// </summary>
		/// <param name="aNode">The node to pop.</param>
		public virtual void PopNode(PNode aNode) {
			nodeStack.Pop();
		}

		/// <summary>
		/// Gets the bottom-most node on the pick path node stack.  That is the last node to
		/// be picked.
		/// </summary>
		/// <value>The bottom-most node on the pick path node stack.</value>
		/// <remarks>This is the node that will have the first chance to handle events.</remarks>
		public virtual PNode PickedNode {
			get { return (PNode)nodeStack.Peek(); }
		}

		/// <summary>
		/// Gets the next node that will be picked after the current picked node.
		/// </summary>
		/// <remarks>
		/// If you have two overlaping children nodes then the topmost child will
		/// always be picked first, use this method to find the covered child.
		/// </remarks>
		/// <value>
		/// The next node that will be picked after the current node, or the camera when
		/// no more visual nodes will be picked.
		/// </value>
		public virtual PNode NextPickedNode {
			get {
				PNode picked = PickedNode;
		
				if (picked == topCamera) return null;		
				if (excludedNodes == null) excludedNodes = new Hashtable();
		
				// exclude current picked node
				excludedNodes.Add(picked, picked);

				Object screenPickBounds = null;
				IEnumerator enumerator = pickBoundsStack.GetEnumerator();
				while (enumerator.MoveNext()) {
					screenPickBounds = enumerator.Current;
				}
		
				// reset path state
				pickBoundsStack = new Stack();
				nodeStack = new Stack();
				matrixStack = new Stack();
				pickBoundsStack = new Stack();
		
				pickBoundsStack.Push(screenPickBounds);

				// pick again
				topCamera.FullPick(this);
		
				// make sure top camera is pushed.
				if (NodeStackReference.Count == 0) {
					PushNode(topCamera);
					PushMatrix(topCamera.MatrixReference);
				}

				return PickedNode;
			}
		}

		/// <summary>
		/// Gets the top-most camera on the pick path.  This is the camera that originated the
		/// pick action.
		/// </summary>
		/// <value>The top-most camera on the pick path.</value>
		public virtual PCamera TopCamera {
			get { return topCamera; }
		}

		/// <summary>
		/// Gets the bottom-most camera on the pick path.  This may be different then the top camera
		/// if internal cameras are in use.
		/// </summary>
		/// <value>The bottom-most camera on the pick path.</value>
		public virtual PCamera BottomCamera {
			get {
				if (bottomCamera == null) {
					object[] nodes = nodeStack.ToArray();
					for (int i = 0; i < nodes.Length; i++) {
						if (nodes[i] is PCamera) {
							return (PCamera)nodes[i];
						}
					}			
				}
				return bottomCamera;
			}
		}
		
		/// <summary>
		/// Gets a reference to the pick path node stack.
		/// </summary>
		/// <value>A reference ot the pick path node stack.</value>
		public virtual Stack NodeStackReference {
			get { return nodeStack; }
		}
		#endregion

		#region Path Matrix
		//****************************************************************
		// Path Transform - Methods for accessing/manipulating the
		// matrices on the matrix stack.
		//****************************************************************

		/// <summary>
		/// Gets the total combined scale of the pick path matrices.
		/// </summary>
		/// <value>The total combined scale of the pick path matrices.</value>
		public virtual float Scale {
			get {		
				PointF p1 = new PointF(0, 0);
				PointF p2 = new PointF(1, 0);

				object[] matrices = matrixStack.ToArray();
				int count = matrices.Length;
				for (int i = count-1; i >= 0; i--) {
					PMatrix each = ((PTuple)matrices[i]).matrix;
					if (each != null) {
						p1 = each.Transform(p1);
						p2 = each.Transform(p2);
					}
				}

				return PUtil.DistanceBetweenPoints(p1, p2);
			}
		}

		/// <summary>
		/// Pushes the given matrix onto the matrix stack.
		/// </summary>
		/// <param name="aMatrix">The matrix to push.</param>
		public virtual void PushMatrix(PMatrix aMatrix) {
			matrixStack.Push(new PTuple(PickedNode, aMatrix));
			if (aMatrix != null) {
				RectangleF newPickBounds = aMatrix.InverseTransform(PickBounds);
				pickBoundsStack.Push(newPickBounds);
			}
		}

		/// <summary>
		/// Pops a matrix from the matrix stack.
		/// </summary>
		/// <param name="aMatrix">The matrix to pop.</param>
		public virtual void PopMatrix(PMatrix aMatrix) {
			matrixStack.Pop();
			if (aMatrix != null) {
				pickBoundsStack.Pop();
			}
		}

		/// <summary>
		/// Returns the product of the matrices from the top-most ancestor node (the last node
		/// in the list) to the given node.
		/// </summary>
		/// <param name="nodeOnPath">
		/// The bottom-most node in the path for which the matrix product will be computed.
		/// </param>
		/// <returns>
		/// The product of the matrices from the top-most node to the given node.
		/// </returns>
		public virtual PMatrix GetPathTransformTo(PNode nodeOnPath) {
			PMatrix aMatrix = new PMatrix();

			object[] matrices = matrixStack.ToArray();
			int count = matrices.Length;
			for (int i = count - 1; i >= 0; i--) {
				PTuple each = (PTuple) matrices[i];
				if (each.matrix != null) aMatrix.Multiply(each.matrix);
				if (nodeOnPath == each.node) {
					return aMatrix;
				}
			}
		
			return aMatrix;
		}
		#endregion

		#region ProcessEvents
		//****************************************************************
		// Process Events - Methods for handling events dispatched to the
		// pick path.
		//****************************************************************

		/// <summary>
		/// Gives each node in the pick path, starting with the bottom-most one, a chance to
		/// handle the event.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public virtual void ProcessEvent(PInputEventArgs e) {
			e.Path = this;
		
			object[] pushedNodes = nodeStack.ToArray();
			for (int i = 0; i < pushedNodes.Length; i++) {
				PNode each = (PNode) pushedNodes[i];
				EventHandlerList handlers = each.HandlerList;

				if (handlers != null) e.DispatchTo(each);
			}	
		}
		#endregion

		#region Transforming Geometry
		//****************************************************************
		// Transforming Geometry - Methods to transform geometry through
		// this path. 
		//
		// Note that this is different that just using the
		// PNode.LocalToGlobal (an other coord system transform methods). 
		// The PNode coord system transform methods always go directly up
		// through their parents. The PPickPath coord system transform
		// methods go up through the list of picked nodes instead. And since
		// cameras can pick their layers in addition to their children these
		// two paths may be different.
		//****************************************************************

		/// <summary>
		/// Convert the given point from canvas coordinates, down the pick path (and through any
		/// camera view transforms applied to the path) to the local coordinates of the given node.
		/// </summary>
		/// <param name="canvasPoint">The point in canvas coordinates.</param>
		/// <param name="nodeOnPath">
		/// The node for which the local coordinates will be computed.
		/// </param>
		/// <returns>The point in the local coordinates of the given node.</returns>
		public virtual PointF CanvasToLocal(PointF canvasPoint, PNode nodeOnPath) {
			return GetPathTransformTo(nodeOnPath).InverseTransform(canvasPoint);		
		}

		/// <summary>
		/// Convert the given size from canvas coordinates, down the pick path (and through any
		/// camera view transforms applied to the path) to the local coordinates of the given node.
		/// </summary>
		/// <param name="canvasSize">The size in canvas coordinates.</param>
		/// <param name="nodeOnPath">
		/// The node for which the local coordinates will be computed.
		/// </param>
		/// <returns>The size in the local coordinates of the given node.</returns>
		public virtual SizeF CanvasToLocal(SizeF canvasSize, PNode nodeOnPath) {
			return GetPathTransformTo(nodeOnPath).InverseTransform(canvasSize);
		}

		/// <summary>
		/// Convert the given rectangle from canvas coordinates, down the pick path (and through
		/// any camera view transforms applied to the path) to the local coordinates of the given
		/// node.
		/// </summary>
		/// <param name="canvasRectangle">The rectangle in canvas coordinates.</param>
		/// <param name="nodeOnPath">
		/// The node for which the local coordinates will be computed.
		/// </param>
		/// <returns>The rectangle in the local coordinates of the given node.</returns>
		public virtual RectangleF CanvasToLocal(RectangleF canvasRectangle, PNode nodeOnPath) {
			return GetPathTransformTo(nodeOnPath).InverseTransform(canvasRectangle);
		}
		#endregion
	}

	#region Node/Matrix Association
	/// <summary>
	/// <b>PTuple</b> is used to associate a node with a matrix.
	/// </summary>
	class PTuple {
		/// <summary>
		/// The node to associate with the matrix.
		/// </summary>
		public PNode node;

		/// <summary>
		/// The matrix to associate with the node.
		/// </summary>
		public PMatrix matrix;

		/// <summary>
		/// Creates a new PTuple that associates the given node witht he given matrix.
		/// </summary>
		/// <param name="n">The node to associate with the matrix.</param>
		/// <param name="m">The matrix to associate with the node.</param>
		public PTuple(PNode n, PMatrix m) {
			node = n;
			matrix = m;
		}
	}
	#endregion
}

