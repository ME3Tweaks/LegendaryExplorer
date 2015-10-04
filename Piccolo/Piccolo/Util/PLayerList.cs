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
	/// <b>PLayerList</b> is a typesafe list of PLayer objects.
	/// </summary>
	/// <remarks>
	/// This class is used by <see cref="PCamera"/> to maintain a list of the
	/// layers that camera is viewing.  See <see cref="PCamera.LayersReference">
	/// PCamera.LayersReference</see>.
	/// </remarks>
	[Serializable]
	public class PLayerList : CollectionBase {

		/// <summary>
		/// Constructs a new PLayerList.
		/// </summary>
		public PLayerList() {
		}

		/// <summary>
		/// Initializes a new instance of the PLayerList class that contains layers copied
		/// from the specified list and that has the same initial capacity as the number
		/// of layers copied.
		/// </summary>
		/// <param name="list">The list whose layers are copied to the new list.</param>
		public PLayerList(PLayerList list) {
			foreach(PLayer layer in list) {
				List.Add(layer);
			}
		}

		/// <summary>
		/// Determines whether the list contains a specific layer.
		/// </summary>
		/// <param name="layer">The layer to locate in the list.</param>
		/// <returns>
		/// True if the layer is found in the list; otherwise, false.
		/// </returns>
		public bool Contains(PLayer layer) {
			return List.Contains(layer);
		}

		/// <summary>
		/// Determines the index of a specific layer in the list.
		/// </summary>
		/// <param name="layer">The layer to locate in the list.</param>
		/// <returns>
		/// The index of the layer if found in the list; otherwise, -1.
		/// </returns>
		public int IndexOf(PLayer layer) {
			return List.IndexOf(layer);
		}

		/// <summary>
		/// Adds a layer to the list.
		/// </summary>
		/// <param name="layer">The layer to add.</param>
		/// <returns>The position into which the new layer was inserted.</returns>
		public int Add(PLayer layer) {
			return List.Add(layer);
		}

		/// <summary>
		/// Adds the layers of the given list to the end of this list.
		/// </summary>
		/// <param name="list">
		/// The list whose layers should be added to the end of this list.
		/// </param>
		public void AddRange(PLayerList list) {
			InnerList.AddRange(list);
		}

		/// <summary>
		/// Removes a range of layers from the list.
		/// </summary>
		/// <param name="index">
		/// The zero-based starting index of the range of layers to remove.
		/// </param>
		/// <param name="count">
		/// The number of layers to remove.
		/// </param>
		public void RemoveRange(int index, int count) {
			InnerList.RemoveRange(index, count);
		}

		/// <summary>
		/// Removes the first occurrence of a specific layer from the list.
		/// </summary>
		/// <param name="layer">The layer to remove from the list.</param>
		public void Remove(PLayer layer) {
			List.Remove(layer);
		}

		/// <summary>
		/// Inserts a layer to the list at the specified position.
		/// </summary>
		/// <param name="index">
		/// The zero-based index at which the layer should be inserted.
		/// </param>
		/// <param name="layer">The layer to insert into the list.</param>
		public void Insert(int index, PLayer layer) {
			List.Insert(index, layer);
		}

		/// <summary>
		/// Sorts the layers in the entire list using the specified comparer.
		/// </summary>
		/// <param name="comparer">
		/// The IComparer implementation to use when comparing elements,
		/// or a null reference to use the IComparable implementation of
		/// each layer.
		/// </param>
		public void Sort(IComparer comparer) {
			InnerList.Sort(comparer);
		}

		/// <summary>
		/// Allows a PLayerList to be indexed directly to access it's children.
		/// </summary>
		public PLayer this[int index] {
			get { return (PLayer)List[index]; }
			set { List[index] = value; }
		}
	}
}