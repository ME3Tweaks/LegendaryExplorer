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
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.PiccoloX.Handles;

namespace UMD.HCIL.PiccoloX.Nodes {
	/// <summary>
	/// <b>PComposite</b> is a simple node that makes a group of nodes appear to
	/// be a single node when picking and interacting.
	/// </summary>
	[Serializable]
	public class PComposite : PNode {
		#region Constructors
		/// <summary>
		/// Constructs a new PComposite.
		/// </summary>
		public PComposite() {
			// Necessary because of the deserialization constructor.
		}
		#endregion

		#region Picking
		/// <summary>
		/// Overridden.  Returns true if this node or any pickable descendends are picked.
		/// </summary>
		/// <remarks>
		/// If a pick occurs the pickPath is modified so that this node is always returned as
		/// the picked node, even if it was a decendent node that initialy reported the pick.
		/// </remarks>
		/// <param name="pickPath"></param>
		/// <returns>True if this node or any descendents are picked; false, otherwise.</returns>
		public override bool FullPick(PPickPath pickPath) {
			if (base.FullPick(pickPath)) {
				PNode picked = pickPath.PickedNode;
			
				// this code won't work with internal cameras, because it doesn't pop
				// the cameras view transform.
				while (picked != this) {
					pickPath.PopMatrix(picked.MatrixReference);
					pickPath.PopNode(picked);
					picked = pickPath.PickedNode;
				}
			
				return true;
			}
			return false;
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
		/// Read this PComposite and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">
		/// The StreamingContext of this serialization operation.
		/// </param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PComposite(SerializationInfo info, StreamingContext context)
			: base(info, context) {
		}
		#endregion
	}
}