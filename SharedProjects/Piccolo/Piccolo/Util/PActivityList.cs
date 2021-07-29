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

using UMD.HCIL.Piccolo.Activities;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PActivityList</b> is a typesafe list of PActivity objects.
	/// </summary>
	/// <remarks>
	/// This class is used by <see cref="PActivityScheduler"/> to maintain a list activities
	/// currently being processed.  See <see cref="PActivityScheduler.ActivitiesReference">
	/// PActivityScheduler.ActivitiesReference</see>.
	/// </remarks>
	[Serializable]
	public class PActivityList : CollectionBase {

		/// <summary>
		/// Constructs a new PActivityList.
		/// </summary>
		public PActivityList() {
		}

		/// <summary>
		/// Initializes a new instance of the PActivityList class that contains activities copied
		/// from the specified list and that has the same initial capacity as the number
		/// of activities copied.
		/// </summary>
		/// <param name="list">The list whose activities are copied to the new list.</param>
		public PActivityList(PActivityList list) {
			foreach(PActivity activity in list) {
				List.Add(activity);
			}
		}

		/// <summary>
		/// Determines whether the list contains a specific activity.
		/// </summary>
		/// <param name="activity">The activity to locate in the list.</param>
		/// <returns>
		/// True if the activity is found in the list; otherwise, false.
		/// </returns>
		public bool Contains(PActivity activity) {
			return List.Contains(activity);
		}

		/// <summary>
		/// Determines the index of a specific activity in the list.
		/// </summary>
		/// <param name="activity">The activity to locate in the list.</param>
		/// <returns>
		/// The index of the activity if found in the list; otherwise, -1.
		/// </returns>
		public int IndexOf(PActivity activity) {
			return List.IndexOf(activity);
		}

		/// <summary>
		/// Adds a activity to the list.
		/// </summary>
		/// <param name="activity">The activity to add.</param>
		/// <returns>The position into which the new activity was inserted.</returns>
		public int Add(PActivity activity) {
			return List.Add(activity);
		}

		/// <summary>
		/// Adds the activities of the given list to the end of this list.
		/// </summary>
		/// <param name="list">
		/// The list whose activities should be added to the end of this list.
		/// </param>
		public void AddRange(PActivityList list) {
			InnerList.AddRange(list);
		}

		/// <summary>
		/// Removes a range of activities from the list.
		/// </summary>
		/// <param name="index">
		/// The zero-based starting index of the range of activities to remove.
		/// </param>
		/// <param name="count">
		/// The number of activities to remove.
		/// </param>
		public void RemoveRange(int index, int count) {
			InnerList.RemoveRange(index, count);
		}

		/// <summary>
		/// Removes the first occurrence of a specific activity from the list.
		/// </summary>
		/// <param name="activity">The activity to remove from the list.</param>
		public void Remove(PActivity activity) {
			List.Remove(activity);
		}

		/// <summary>
		/// Inserts a activity to the list at the specified position.
		/// </summary>
		/// <param name="index">
		/// The zero-based index at which the activity should be inserted.
		/// </param>
		/// <param name="activity">The activity to insert into the list.</param>
		public void Insert(int index, PActivity activity) {
			List.Insert(index, activity);
		}

		/// <summary>
		/// Sorts the activities in the entire list using the specified comparer.
		/// </summary>
		/// <param name="comparer">
		/// The IComparer implementation to use when comparing elements,
		/// or a null reference to use the IComparable implementation of
		/// each activity.
		/// </param>
		public void Sort(IComparer comparer) {
			InnerList.Sort(comparer);
		}

		/// <summary>
		/// Allows a PActivityList to be indexed directly to access it's children.
		/// </summary>
		public PActivity this[int index] {
			get { return (PActivity)List[index]; }
			set { List[index] = value; }
		}
	}
}