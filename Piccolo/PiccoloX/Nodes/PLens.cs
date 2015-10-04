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
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// <b>PLens</b> is a simple default lens implementation for Piccolo.
	/// </summary>
	/// <remarks>
	/// Lenses are often application specific.  It may be easiest to study this code, and
	/// then implement a custom lens using the general principles illustrated here.
	/// See <c>UMD.HCIL.PiccoloExample.LensExample</c> for one possible use of this lens. 
	/// <para>
	/// The basic design here is to add a PCamera as the child of a PNode (the lens node).
	/// The camera is the viewing part of the lens, and the node is the title bar that can
	/// be used to move the lens around.  Users of this lens will probably want to set up
	/// a lens specific event handler and attach it to the camera.
	/// </para>
	/// <para>
	/// A lens also needs a layer that it will look at (it should not be the same as the
	/// layer that it’s added to because then it will draw itself in a recursive loop).
	/// Last of all, PLens will need to be added to the PCanvas layer (so that it can be
	/// seen  by the main camera).
	/// </para>
	/// </remarks>
	[Serializable]
	public class PLens : PNode {
		#region Fields
		/// <summary>
		/// The height of the lens's dragbar.
		/// </summary>
		public static float LENS_DRAGBAR_HEIGHT = 20;

		/// <summary>
		/// The default brush used to render the lens's dragbar.
		/// </summary>
		public static Brush DEFAULT_DRAGBAR_BRUSH = Brushes.DarkGray;

		/// <summary>
		/// The default brush used to render the lens's camera.
		/// </summary>
		public static Brush DEFAULT_LENS_BRUSH = Brushes.LightGray;
		
		private PPath dragBar;
		private PCamera camera;
		private PDragEventHandler lensDragger;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PLens.
		/// </summary>
		public PLens() {
			dragBar = PPath.CreateRectangle(0, 0, 100, 100);  // Drag bar gets resized to fit the available space, so any rectangle will do here
			dragBar.Brush = DEFAULT_DRAGBAR_BRUSH;
			dragBar.Pickable = false;  // This forces drag events to percolate up to PLens object
			AddChild(dragBar);
		
			camera = new PCamera();
			camera.Brush = DEFAULT_LENS_BRUSH;
			AddChild(camera);
		
			// Create an event handler to drag the lens around. Note that this event
			// handler consumes events in case another conflicting event handler has been
			// installed higher up in the heirarchy.
			lensDragger = new LensDragHandler();
			AddInputEventListener(lensDragger);

			// When this PLens is dragged around adjust the camera's view transform. 
			TransformChanged += new PPropertyEventHandler(PLens_TransformChanged);
		}

		/// <summary>
		/// Constructs a new PLens whose camera views the specified layer.
		/// </summary>
		/// <param name="layer">The layer this lens will view.</param>
		public PLens(PLayer layer) : this() {
			AddLayer(0, layer);
		}
		#endregion

		#region Basics
		/// <summary>
		/// Gets the camera child of this lens.
		/// </summary>
		/// <value>The camera child of this lens.</value>
		public virtual PCamera Camera {
			get { return camera; }
		}
	
		/// <summary>
		/// Gets the dragbar for this lens.
		/// </summary>
		/// <value>The dragbar for this lens.</value>
		public virtual PPath DragBar {
			get { return dragBar; }
		}

		/// <summary>
		/// Gets the drag event handler responsible for dragging this lens.
		/// </summary>
		/// <value>The drag event handler responsible for dragging this lens.</value>
		public virtual PDragEventHandler LensDraggerHandler {
			get { return lensDragger; }
		}
	
		/// <summary>
		/// Add the layer at the given index in the list of layers managed by the camera child
		/// of this lens.
		/// </summary>
		/// <param name="index">The index at which to add the layer.</param>
		/// <param name="layer">The layer to add to the camera child of this lens.</param>
		public virtual void AddLayer(int index, PLayer layer) {
			camera.AddLayer(index, layer);
		}
	
		/// <summary>
		/// Remove the given layer from the list of layers managed by the camera child of
		/// this lens.
		/// </summary>
		/// <param name="layer">The layer to remove.</param>
		public virtual void RemoveLayer(PLayer layer) {
			camera.RemoveLayer(layer);
		}
		#endregion

		#region Dragging
		/// <summary>
		/// <b>LensDragHandler</b> is the event handler responsible for dragging the lens.
		/// </summary>
		/// <remarks>
		/// This event handler consumes events in case another conflicting event handler has
		/// been installed higher up in the heirarchy.
		/// </remarks>
		class LensDragHandler : PDragEventHandler {
			/// <summary>
			/// Overridden.  <see cref="PBasicInputEventHandler.DoesAcceptEvent">
			/// PBasicInputEventHandler.DoesAcceptEvent</see>
			/// </summary>
			public override bool DoesAcceptEvent(PInputEventArgs e) {
				if (base.DoesAcceptEvent (e)) {
					e.Handled = true;
					return true;
				}
				return false;
			}
		}


		/// <summary>
		/// When this PLens is dragged around, adjust the camera's view transform so that the
		/// squiggles remain fixed at their original locations.
		/// </summary>
		/// <param name="sender">The source of this TransformChanged event.</param>
		/// <param name="e">A PPropertyEventArgs that contains the event data.</param>
		protected virtual void PLens_TransformChanged(object sender, PPropertyEventArgs e) {
			camera.ViewMatrix = InverseMatrix;
		}
		#endregion

		#region Layout
		/// <summary>
		/// Overridden.  When the lens is resized this method gives us a chance to layout the
		/// lens's camera child appropriately.
		/// </summary>
		public override void LayoutChildren() {
			dragBar.Reset();
			dragBar.AddRectangle((float)X, (float)Y, (float)Width, (float)LENS_DRAGBAR_HEIGHT);
			camera.SetBounds(X, Y + LENS_DRAGBAR_HEIGHT, Width, Height - LENS_DRAGBAR_HEIGHT);
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
		/// Read this PLens and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">The StreamingContext of this serialization operation.</param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PLens(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
		#endregion
	}
}