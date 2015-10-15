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

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PNodeList</b> is a typesafe list of PNode objects.
	/// </summary>
	/// <remarks>
	/// This class is used by <see cref="PNode"/> to maintain a list of it's
	/// children.  See <see cref="PNode.ChildrenReference">
	/// PNode.ChildrenReference</see>.
	/// </remarks>
	[Serializable]
	public class PNodeList : CollectionBase {

		/// <summary>
		/// Constructs a new PNodeList.
		/// </summary>
		public PNodeList() {
		}

		/// <summary>
		/// Initializes a new instance of the PNodeList class that contains nodes copied
		/// from the specified list and that has the same initial capacity as the number
		/// of nodes copied.
		/// </summary>
		/// <param name="list">The list whose nodes are copied to the new list.</param>
		public PNodeList(PNodeList list) {
			foreach(PNode node in list) {
				List.Add(node);
			}
		}

		/// <summary>
		/// Determines whether the list contains a specific node.
		/// </summary>
		/// <param name="node">The node to locate in the list.</param>
		/// <returns>
		/// True if the node is found in the list; otherwise, false.
		/// </returns>
		public bool Contains(PNode node) {
			return List.Contains(node);
		}

		/// <summary>
		/// Determines the index of a specific node in the list.
		/// </summary>
		/// <param name="node">The node to locate in the list.</param>
		/// <returns>
		/// The index of the node if found in the list; otherwise, -1.
		/// </returns>
		public int IndexOf(PNode node) {
			return List.IndexOf(node);
		}

		/// <summary>
		/// Adds a node to the list.
		/// </summary>
		/// <param name="node">The node to add.</param>
		/// <returns>The position into which the new node was inserted.</returns>
		public int Add(PNode node) {
			return List.Add(node);
		}

		/// <summary>
		/// Adds the nodes of the given list to the end of this list.
		/// </summary>
		/// <param name="list">
		/// The list whose nodes should be added to the end of this list.
		/// </param>
		public void AddRange(PNodeList list) {
			InnerList.AddRange(list);
		}

		/// <summary>
		/// Removes a range of nodes from the list.
		/// </summary>
		/// <param name="index">
		/// The zero-based starting index of the range of nodes to remove.
		/// </param>
		/// <param name="count">
		/// The number of nodes to remove.
		/// </param>
		public void RemoveRange(int index, int count) {
			InnerList.RemoveRange(index, count);
		}

		/// <summary>
		/// Removes the first occurrence of a specific node from the list.
		/// </summary>
		/// <param name="node">The node to remove from the list.</param>
		public void Remove(PNode node) {
			List.Remove(node);
		}

		/// <summary>
		/// Inserts a node to the list at the specified position.
		/// </summary>
		/// <param name="index">
		/// The zero-based index at which the node should be inserted.
		/// </param>
		/// <param name="node">The node to insert into the list.</param>
		public void Insert(int index, PNode node) {
			List.Insert(index, node);
		}

		/// <summary>
		/// Sorts the nodes in the entire list using the specified comparer.
		/// </summary>
		/// <param name="comparer">
		/// The IComparer implementation to use when comparing elements,
		/// or a null reference to use the IComparable implementation of
		/// each node.
		/// </param>
		public void Sort(IComparer comparer) {
			InnerList.Sort(comparer);
		}

		/// <summary>
		/// Allows a PNodeList to be indexed directly to access it's children.
		/// </summary>
		public PNode this[int index] {
			get { return (PNode)List[index]; }
			set { List[index] = value; }
		}
	}
}