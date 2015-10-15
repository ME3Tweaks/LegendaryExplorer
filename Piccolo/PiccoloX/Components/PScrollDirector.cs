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

using UMD.HCIL.Piccolo;

namespace UMD.HCIL.PiccoloX.Components {
	/// <summary>
	/// <b>PScrollDirector</b> is the interface an application can implement to control
	/// the scrolling of a <see cref="PCanvas"/> in a <see cref="PScrollableControl"/>.
	/// </summary>
	public interface PScrollDirector {
		/// <summary>
		/// Installs the scroll director.
		/// </summary>
		/// <param name="scrollableControl">
		/// The scrollable control that signals this director.
		/// </param>
		/// <param name="view">The PCanvas that the scrollable control scrolls.</param>
		void Install(PScrollableControl scrollableControl, PCanvas view);

		/// <summary>
		/// Uninstalls the scroll director.
		/// </summary>
		void UnInstall();

		/// <summary>
		/// Gets the view position given the specified camera bounds.
		/// </summary>
		/// <param name="bounds">
		/// The bounds for which the view position will be computed.
		/// </param>
		/// <returns>The view position.</returns>
		Point GetViewPosition(RectangleF bounds);

		/// <summary>
		/// Sets the view position.
		/// </summary>
		/// <param name="x">The x coordinate of the new position.</param>
		/// <param name="y">The y coordinate of the new position.</param>
		void SetViewPosition(float x, float y);

		/// <summary>
		/// Gets the size of the view based on the specific camera bounds.
		/// </summary>
		/// <param name="bounds">
		/// The view bounds for which the view size will be computed.
		/// </param>
		/// <returns>The view size.</returns>
		Size GetViewSize(RectangleF bounds);
	}
}