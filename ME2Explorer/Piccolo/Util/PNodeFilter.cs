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

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PNodeFilter</b> is a interface that filters (accepts or rejects) nodes.
	/// </summary>
	/// <remarks>
	/// The main use of this class is to retrieve all the children of a node the meet
	/// some criteria by using the method <see cref="PNode.GetAllNodes">
	/// PNode.GetAllNodes</see>.
	/// </remarks>
	public interface PNodeFilter {
		/// <summary>
		/// Returns true if the filter should accept the given node.
		/// </summary>
		/// <param name="aNode">The node to accept or reject.</param>
		/// <returns>
		/// True if the filter should accept the give node; otherwise, false.
		/// </returns>
		bool Accept(PNode aNode);

		/// <summary>
		/// Returns true if the filter should test the children of the given node for
		/// acceptance.
		/// </summary>
		/// <param name="aNode">The node whose children we must decide whether to test.</param>
		/// <returns>
		/// True if the filter should test the children of the given node; otherwise, false.
		/// </returns>
		bool AcceptChildrenOf(PNode aNode);
	}
}
