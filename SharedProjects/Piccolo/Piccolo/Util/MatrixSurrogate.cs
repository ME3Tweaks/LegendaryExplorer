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
using System.Drawing.Drawing2D;
using System.Runtime.Serialization;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>MatrixSurrogate</b> is a serialization surrogate that defines how to read and write
	/// a <see cref="Matrix"/> object.
	/// </summary>
	public class MatrixSurrogate : ISerializationSurrogate {
		#region Write
		/// <summary>
		/// Write this Matrix to the given SerializationInfo.
		/// </summary>
		/// <param name="obj">The object to serialize.</param>
		/// <param name="info">The SerializationInfo to write to.</param>
		/// <param name="context">The streaming context of this serialization operation.</param>
		public void GetObjectData(Object obj, SerializationInfo info, StreamingContext context) {
			Matrix matrix = (Matrix)obj;
			info.AddValue("elements", matrix.Elements);
		}
		#endregion

		#region Read
		/// <summary>
		/// Read this Matrix from the given SerializationInfo.
		/// </summary>
		/// <param name="obj">The object to populate.</param>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">The StreamingContext of this serialization operation.</param>
		/// <param name="selector">
		/// The surrogate selector where the search for a compatible surrogate begins.
		/// </param>
		/// <returns>The populated deserialized object.</returns>
		public Object SetObjectData(Object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector) {
			float[] elements = (float[])info.GetValue("elements", typeof(Array));
			return new Matrix(elements[0], elements[1], elements[2], elements[3], elements[4], elements[5]);
		}
		#endregion
	}
}