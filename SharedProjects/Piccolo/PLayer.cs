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

using System.Drawing;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo
{
	/// <summary>
	/// <b>PLayer</b> is a node that can be viewed directly by multiple camera nodes.
	/// </summary>
	/// <remarks>
	/// Generally child nodes are added to a layer to give the viewing cameras 
	/// something to look at.
	/// <para>
	/// A single layer node may be viewed through multiple cameras with each camera
	/// using its own view matrix. This means that any node (since layers can have
	/// children) may be visible through multiple cameras at the same time.
	/// </para>
	/// </remarks>
	public sealed class PLayer : PNode {
		#region fields
		/// <summary>
		/// The key that identifies a change in the set of this layer's cameras.
		/// </summary>
		/// <remarks>
		/// In a property change event the new value will be a reference to the list of this
		/// nodes cameras, but old value will always be null.
		/// </remarks>
        private static readonly object PROPERTY_KEY_CAMERAS = new();

		/// <summary>
		/// A bit field that identifies a <see cref="CamerasChanged">CamerasChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether CamerasChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_CAMERAS = 1 << 12;

		private PCameraList cameras;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PLayer.
		/// </summary>
		public PLayer() {
			cameras = new PCameraList();
		}
		#endregion

		#region Cameras
		//****************************************************************
		// Cameras - Maintain the list of cameras that are viewing this
		// layer.
		//****************************************************************

		/// <summary>
		/// Occurs when there is a change in the set of this layer's cameras.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the CamerasChanged Event as in
		/// CamerasChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_CAMERAS in the Events list).
		/// When a user removes an event handler from the CamerasChanged event as in 
		/// CamerasChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_CAMERAS in the Events list).
		/// </remarks>
		public event PPropertyEventHandler CamerasChanged {
			add => HandlerList.AddHandler(PROPERTY_KEY_CAMERAS, value);
            remove => HandlerList.RemoveHandler(PROPERTY_KEY_CAMERAS, value);
        }

		/// <summary>
		/// Gets the list of cameras viewing this layer.
		/// </summary>
		/// <value>The list of cameras viewing this layer.</value>
		public PCameraList CamerasReference => cameras;

        /// <summary>
		/// Get the number of cameras viewing this layer.
		/// </summary>
		/// <value>The number of cameras viewing this layer.</value>
		public int CameraCount {
			get {
				if (cameras == null) {
					return 0;
				}
				return cameras.Count;
			}
		}

		/// <summary>
		/// Get the camera in this layer's camera list at the specified index. 
		/// </summary>
		/// <param name="index">The index of the desired camera.</param>
		/// <returns>The camera at the specified index.</returns>
		public PCamera GetCamera(int index) {
			return cameras[index];
		}
		
		/// <summary>
		/// Add a camera to this layer's camera list.
		/// </summary>
		/// <param name="camera">The new camera to add.</param>
		/// <remarks>
		/// This method it called automatically when a layer is added to a camera.
		/// </remarks>
		public void AddCamera(PCamera camera) {
			AddCamera(CameraCount, camera);
		}
		
		/// <summary>
		/// Add a camera to this layer's camera list at the specified index.
		/// </summary>
		/// <param name="index">The index at which to add the new layer.</param>
		/// <param name="camera">The new camera to add.</param>
		/// <remarks>
		/// This method it called automatically when a layer is added to a camera.
		/// </remarks>
		public void AddCamera(int index, PCamera camera) {
			cameras.Insert(index, camera);
			InvalidatePaint();
			FirePropertyChangedEvent(PROPERTY_KEY_CAMERAS, PROPERTY_CODE_CAMERAS, null, cameras);
		}
		
		/// <summary>
		/// Remove the camera from this layer's camera list.
		/// </summary>
		/// <param name="camera">The camera to remove.</param>
		/// <returns>The removed camera.</returns>
		public PCamera RemoveCamera(PCamera camera) {
			return RemoveCamera(cameras.IndexOf(camera));
		}
		
		/// <summary>
		/// Remove the camera at the given index from this layer's camera list.
		/// </summary>
		/// <param name="index">The index of the camera to remove.</param>
		/// <returns>The removed camera.</returns>
		public PCamera RemoveCamera(int index) {
			PCamera camera = cameras[index];
			cameras.RemoveAt(index);
			InvalidatePaint();
			FirePropertyChangedEvent(PROPERTY_KEY_CAMERAS, PROPERTY_CODE_CAMERAS, null, cameras);
			return camera;
		}
		#endregion

		#region Camera Repaint Notifications
		//****************************************************************
		// Camera Repaint Notifications - Layer nodes must forward their
		// repaints to each camera that is viewing them so that the camera
		// views will also get repainted.
		//****************************************************************

		/// <summary>
		/// Overridden.  Forward repaints to the cameras that are viewing this layer.
		/// </summary>
		/// <param name="bounds">
		/// The bounds to repaint, specified in the local coordinate system.
		/// </param>
		/// <param name="childOrThis">
		/// If childOrThis does not equal this then this layer's matrix will be
		/// applied to the bounds paramater.
		/// </param>
		public override void RepaintFrom(RectangleF bounds, PNode childOrThis) {						
			if (childOrThis != this) {
				bounds = LocalToParent(bounds);
			}
			
			NotifyCameras(bounds);

            Parent?.RepaintFrom(bounds, childOrThis);
        }	

		/// <summary>
		/// Notify the cameras looking at this layer to paint their views.
		/// </summary>
		/// <param name="bounds">
		/// The bounds to repaint, specified in the view coordinate system.
		/// </param>
        private void NotifyCameras(RectangleF bounds) {
			foreach (PCamera each in cameras) {
				each.RepaintFromLayer(bounds, this);
			}
		}
		#endregion
	}
}