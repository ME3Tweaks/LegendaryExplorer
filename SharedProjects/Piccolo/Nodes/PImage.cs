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
using System.IO;
using System.Net;
using System.Text;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo.Nodes {
	/// <summary>
	/// <b>PImage</b> is a wrapper around a <see cref="System.Drawing.Image">
	/// System.Drawing.Image</see>.
	/// </summary>
	public class PImage : PNode {
		#region Fields
		/// <summary>
		/// The key that identifies a change in this node's <see cref="Image">Image</see>.
		/// </summary>
		/// <remarks>
		/// In a property change event both the old and new value will be set correctly
		/// to Image objects.
		/// </remarks>
		protected static readonly object PROPERTY_KEY_IMAGE = new();

		/// <summary>
		/// A bit field that identifies a <see cref="ImageChanged">ImageChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether ImageChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_IMAGE = 1 << 14;

		/// <summary>
		/// The underlying image object.
		/// </summary>
		protected Image image;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PImage with a <c>null</c> image.
		/// </summary>
		public PImage() {
		}

		/// <summary>
		/// Constructs a new PImage wrapping the given <see cref="System.Drawing.Image">
		/// System.Drawing.Image</see>.
		/// </summary>
		/// <param name="newImage">The image to wrap.</param>
		public PImage(Image newImage) {
			Image = newImage;
		}

		/// <summary>
		/// Constructs a new PImage by loading the given file and wrapping the
		/// resulting <see cref="System.Drawing.Image">System.Drawing.Image</see>.
		/// </summary>
		/// <param name="fileName">The filename of the image to load.</param>
		public PImage(string fileName) : this(new Bitmap(fileName)) {
		}
		#endregion

		#region Basic
		//****************************************************************
		// Basic - Methods for manipulating the underlying image.
		//****************************************************************

		/// <summary>
		/// Occurs when there is a change in this node's
		/// <see cref="Image">Image</see>.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the ImageChanged Event as in
		/// ImageChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_IMAGE in the Events list).
		/// When a user removes an event handler from the ImageChanged event as in 
		/// ImageChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_IMAGE in the Events list).
		/// </remarks>
		public virtual event PPropertyEventHandler ImageChanged {
			add => HandlerList.AddHandler(PROPERTY_KEY_IMAGE, value);
            remove => HandlerList.RemoveHandler(PROPERTY_KEY_IMAGE, value);
        }

		/// <summary>
		/// Gets or sets the image shown by this node.
		/// </summary>
		/// <value>The image shown by this node.</value>
		public virtual Image Image {
			get => image;
            set { 
				Image old = image;
				image = value;
				if (image == null) {
					SetBounds(0, 0, 0, 0);
				} else {
					SetBounds(0, 0, image.Width, image.Height);
				}
				InvalidatePaint();
				FirePropertyChangedEvent(PROPERTY_KEY_IMAGE, PROPERTY_CODE_IMAGE, old, image);
			}
		}
		#endregion

		#region Painting
		//****************************************************************
		// Painting - Methods for painting a PImage.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PNode.Paint">PNode.Paint</see>.
		/// </summary>
		protected override void Paint(PPaintContext paintContext) {
			if (Image != null) {
				RectangleF b = Bounds;
				Graphics g = paintContext.Graphics;

				g.DrawImage(image, b);
			}
		}
		#endregion

        #region Debugging
		//****************************************************************
		// Debugging -Methods for debugging.
		//****************************************************************

		/// <summary>
		/// Overridden.  Gets a string representing the state of this node.
		/// </summary>
		/// <value>A string representation of this node's state.</value>
		/// <remarks>
		/// This property is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override string ParamString {
			get {
				var result = new StringBuilder();

				result.Append("image=" + (image == null ? "null" : image.ToString()));
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}