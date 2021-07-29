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
using System.Collections;

using UMD.HCIL.Piccolo;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PCameraList</b> is a typesafe list of PCamera objects.
	/// </summary>
	/// <remarks>
	/// This class is used by <see cref="PLayer"/> to maintain a list of the
	/// cameras viewing that layer.  See <see cref="PLayer.CamerasReference">
	/// PLayer.CamerasReference</see>.
	/// </remarks>
	[Serializable]
	public class PCameraList : CollectionBase {

		/// <summary>
		/// Constructs a new PCameraList.
		/// </summary>
		public PCameraList() {
		}

		/// <summary>
		/// Initializes a new instance of the PCameraList class that contains cameras copied
		/// from the specified list and that has the same initial capacity as the number
		/// of cameras copied.
		/// </summary>
		/// <param name="list">The list whose cameras are copied to the new list.</param>
		public PCameraList(PCameraList list) {
			foreach(PCamera camera in list) {
				List.Add(camera);
			}
		}

		/// <summary>
		/// Determines whether the list contains a specific camera.
		/// </summary>
		/// <param name="camera">The camera to locate in the list.</param>
		/// <returns>
		/// True if the camera is found in the list; otherwise, false.
		/// </returns>
		public bool Contains(PCamera camera) {
			return List.Contains(camera);
		}

		/// <summary>
		/// Determines the index of a specific camera in the list.
		/// </summary>
		/// <param name="camera">The camera to locate in the list.</param>
		/// <returns>
		/// The index of the camera if found in the list; otherwise, -1.
		/// </returns>
		public int IndexOf(PCamera camera) {
			return List.IndexOf(camera);
		}

		/// <summary>
		/// Adds a camera to the list.
		/// </summary>
		/// <param name="camera">The camera to add.</param>
		/// <returns>The position into which the new camera was inserted.</returns>
		public int Add(PCamera camera) {
			return List.Add(camera);
		}

		/// <summary>
		/// Adds the cameras of the given list to the end of this list.
		/// </summary>
		/// <param name="list">
		/// The list whose cameras should be added to the end of this list.
		/// </param>
		public void AddRange(PCameraList list) {
			InnerList.AddRange(list);
		}

		/// <summary>
		/// Removes a range of cameras from the list.
		/// </summary>
		/// <param name="index">
		/// The zero-based starting index of the range of cameras to remove.
		/// </param>
		/// <param name="count">
		/// The number of cameras to remove.
		/// </param>
		public void RemoveRange(int index, int count) {
			InnerList.RemoveRange(index, count);
		}

		/// <summary>
		/// Removes the first occurrence of a specific camera from the list.
		/// </summary>
		/// <param name="camera">The camera to remove from the list.</param>
		public void Remove(PCamera camera) {
			List.Remove(camera);
		}

		/// <summary>
		/// Inserts a camera to the list at the specified position.
		/// </summary>
		/// <param name="index">
		/// The zero-based index at which the camera should be inserted.
		/// </param>
		/// <param name="camera">The camera to insert into the list.</param>
		public void Insert(int index, PCamera camera) {
			List.Insert(index, camera);
		}

		/// <summary>
		/// Sorts the cameras in the entire list using the specified comparer.
		/// </summary>
		/// <param name="comparer">
		/// The IComparer implementation to use when comparing elements,
		/// or a null reference to use the IComparable implementation of
		/// each camera.
		/// </param>
		public void Sort(IComparer comparer) {
			InnerList.Sort(comparer);
		}

		/// <summary>
		/// Allows a PCameraList to be indexed directly to access it's children.
		/// </summary>
		public PCamera this[int index] {
			get { return (PCamera)List[index]; }
			set { List[index] = value; }
		}
	}
}