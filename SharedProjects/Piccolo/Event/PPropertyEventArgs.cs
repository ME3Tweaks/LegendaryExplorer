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
using System.Text;

namespace Piccolo.Event {
	/// <summary>
	/// <b>PPropertyEventArgs</b> is used to pass data for a property changed event to a
	/// <see cref="PPropertyEventHandler">PPropertyEventHandler</see>.
	/// </summary>
	public sealed class PPropertyEventArgs : EventArgs {
		#region Fields
		/// <summary>
		/// The old value of the property that changed.
		/// </summary>
        private object oldValue;

		/// <summary>
		/// The new value of the property that changed.
		/// </summary>
        private object newValue;

        #endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPropertyEventArgs.
		/// </summary>
		/// <param name="oldValue">The old value of the property that changed.</param>
		/// <param name="newValue">The new value of the property that changed.</param>
		public PPropertyEventArgs(object oldValue, object newValue) {
			this.oldValue = oldValue;
			this.newValue = newValue;
		}
		#endregion

		#region Value
		/// <summary>
		/// Gets the old value of the property that changed.
		/// </summary>
		/// <value>The old value of the property that changed.</value>
		public object OldValue => oldValue;

        /// <summary>
		/// Gets the new value of the property that changed.
		/// </summary>
		/// <value>The new value of the property that changed.</value>
		public object NewValue => newValue;

        #endregion

		#region Debugging
		/// <summary>
		/// Overridden.  Returns a string representation of this object for debugging purposes.
		/// </summary>
		/// <returns>A string representation of this object.</returns>
		public override string ToString() {
			StringBuilder result = new StringBuilder();

			result.Append(base.ToString());
			result.Append('[');
			result.Append("oldValue=" + (oldValue == null ? "null" : oldValue.ToString()));
			result.Append(",newValue=" + (newValue == null ? "null" : newValue.ToString()));
			result.Append(']');

			return result.ToString();
		}
		#endregion
	}
}