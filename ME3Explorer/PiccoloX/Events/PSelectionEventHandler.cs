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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.PiccoloX.Handles;

namespace UMD.HCIL.PiccoloX.Events {
	/// <summary>
	/// <b>PSelectionEventHandler</b> provides standard interaction for selection.
	/// </summary>
	/// <remarks>
	/// Clicking selects the object under the cursor.  Shift-clicking allows multiple
	/// objects to be selected.  Dragging offers marquee selection.  And, by default,
	/// pressing the delete key deletes the selection.
	/// </remarks>
	public class PSelectionEventHandler : PDragSequenceEventHandler {
		#region Fields
		/// <summary>
		/// The name for a selection changed notificaton.
		/// </summary>
		public static readonly String SELECTION_CHANGED_NOTIFICATION = "SELECTION_CHANGED_NOTIFICATION";
		
		private readonly static int DASH_WIDTH = 5;
		private readonly static int NUM_PENS = 10;
		private static ArrayList TEMP_LIST = new ArrayList();
	
		private Hashtable selection = null;		 // The current selection
		private PNodeList selectableParents = null;  // List of nodes whose children can be selected
		private PPath marquee = null;
		private PNode marqueeParent = null; 	 // Node that marquee is added to as a child
		private PointF presspt = PointF.Empty;
		private PointF canvasPressPt = PointF.Empty;
		private float penNum = 0;
		private Pen[] pens = null;
		private Hashtable allItems = null;		// Used within drag handler temporarily
		private PNodeList unselectList = null;	// Used within drag handler temporarily
		private Hashtable marqueeMap = null;
		private PNode pressNode = null; 		// Node pressed on (or null if none)
		private bool deleteKeyActive = true;	// True if DELETE key should delete selection
		private Brush marqueeBrush;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PSelectionEventHandler that will handle selection for the
		/// children of the given selectable parent node.
		/// </summary>
		/// <param name="marqueeParent">
		/// The node to which the event handler dynamically adds a marquee (temporarily)
		/// to represent the area being selected.
		/// </param>
		/// <param name="selectableParent">
		/// The node whose children will be selected by this event handler.
		/// </param>
		public PSelectionEventHandler(PNode marqueeParent, PNode selectableParent) {
			this.marqueeParent = marqueeParent;
			selectableParents = new PNodeList();
			selectableParents.Add(selectableParent);
			Init();
		}

		/// <summary>
		/// Constructs a new PSelectionEventHandler that will handle selection for the
		/// children of the given list of selectable parent nodes.
		/// </summary>
		/// <param name="marqueeParent">
		/// The node to which the event handler dynamically adds a marquee (temporarily)
		/// to represent the area being selected.
		/// </param>
		/// <param name="selectableParents">
		/// A list of nodes whose children will be selected by this event handler.
		/// </param>
		public PSelectionEventHandler(PNode marqueeParent, PNodeList selectableParents) {
			this.marqueeParent = marqueeParent;
			this.selectableParents = selectableParents;
			Init();
		}

		/// <summary>
		/// Called by the constructors to perform some common initialization tasks.
		/// </summary>
		protected virtual void Init() {
			float[] dash = { DASH_WIDTH, DASH_WIDTH };
			pens = new Pen[NUM_PENS];
			for (int i = 0; i < NUM_PENS; i++) {
				pens[i] = new Pen(Color.Black, 1);
				pens[i].DashPattern = dash;
				pens[i].DashOffset = i;
			}
		
			selection = new Hashtable();
			allItems = new Hashtable();
			unselectList = new PNodeList();
			marqueeMap = new Hashtable();
		}
		#endregion

		#region Selection Management
		//****************************************************************
		// Selection Management - Methods for manipulating the selection.
		//****************************************************************

		/// <summary>
		/// Selects each node in the list, if the node is not already selected.
		/// </summary>
		/// <param name="items">The list of items to select.</param>
		/// <remarks>
		/// This method will decorate the nodes in the list with handles and
		/// post a SELECTION_CHANGED_NOTIFICATION if the selection has changed.
		/// </remarks>
		public virtual void Select(PNodeList items) {
			Select((ICollection)items);
		}

		/// <summary>
		/// Selects each node in the collection, if the node is not already selected.
		/// </summary>
		/// <param name="items">The collection of items to select.</param>
		/// <remarks>
		/// This method will decorate the nodes in the collection with handles and
		/// post a SELECTION_CHANGED_NOTIFICATION if the selection has changed.
		/// <para>
		/// Note that the collection must only contain objects of type
		/// <see cref="PNode"/>.
		/// </para>
		/// </remarks>
		protected virtual void Select(ICollection items) {
			bool changes = false;
			foreach (PNode each in items) {
				changes |= InternalSelect(each);
			}
			if (changes) {
				PostSelectionChanged();
			}
		}

		/// <summary>
		/// Selects each node used as a key in the dictionary, if the node is not
		/// already selected.
		/// </summary>
		/// <param name="items">The dictionary whose keys will be selected.</param>
		/// <remarks>
		/// This method will decorate the nodes used as keys with handles and post a
		/// SELECTION_CHANGED_NOTIFICATION if the selection has changed.
		/// <para>
		/// Note that the keys in the dictionary must be objects of type
		/// <see cref="PNode"/>.
		/// </para>
		/// </remarks>
		protected virtual void Select(IDictionary items) {
			Select(items.Keys);
		}

		/// <summary>
		/// Selects the given node, if it is not already selected.
		/// </summary>
		/// <param name="node">The node to select.</param>
		/// <returns>True if the node was selected; otherwise, false.</returns>
		/// <remarks>
		/// The node will be decorated with handles if it is selected.
		/// </remarks>
		protected virtual bool InternalSelect(PNode node) {
			if (IsSelected(node)) {
				return false;
			}

			selection.Add(node, true);
			DecorateSelectedNode(node);
			return true;
		}

		/// <summary>
		/// Posts a SELECTION_CHANGED_NOTIFICATION to indicate that the current
		/// selection has changed.
		/// </summary>
		private void PostSelectionChanged() {
			PNotificationCenter.DefaultCenter.PostNotification(SELECTION_CHANGED_NOTIFICATION, this);
		}

		/// <summary>
		/// Selects the given node, if it is not already selected, and posts a
		/// SELECTION_CHANGED_NOTIFICATION if the current selection has changed.
		/// </summary>
		/// <param name="node">The node to select.</param>
		/// <remarks>
		/// The node will be decorated with handles if it is selected.
		/// </remarks>
		public virtual void Select(PNode node) {
			if (InternalSelect(node)) {
				PostSelectionChanged();
			}
		}

		/// <summary> 
		/// Adds bounds handles to the given node.
		/// </summary>
		/// <param name="node">The node to decorate.</param>
		public virtual void DecorateSelectedNode(PNode node) {
			PBoundsHandle.AddBoundsHandlesTo(node);
		}


		/// <summary>
		/// Unselects each node in the list, if the node is currently selected.
		/// </summary>
		/// <param name="items">The list of items to unselect.</param>
		/// <remarks>
		/// This method will remove the handles from the selected nodes in the
		/// list and post a SELECTION_CHANGED_NOTIFICATION if the selection has
		/// changed.
		/// </remarks>
		public virtual void Unselect(PNodeList items) {
			Unselect((ICollection)items);
		}

		/// <summary>
		/// Unselects each node in the collection, if the node is currently selected.
		/// </summary>
		/// <param name="items">The collection of items to unselect.</param>
		/// <remarks>
		/// This method will remove the handles from the selected nodes in the
		/// collection and post a SELECTION_CHANGED_NOTIFICATION if the selection has
		/// changed.
		/// <para>
		/// Note that the collection must only contain objects of type
		/// <see cref="PNode"/>.
		/// </para>
		/// </remarks>
		protected virtual void Unselect(ICollection items) {
			bool changes = false;
			foreach (PNode each in items) {
				changes |= InternalUnselect(each);
			}
			if (changes) {
				PostSelectionChanged();
			}
		}

		/// <summary>
		/// Unselects the given node, if it is currently selected.
		/// </summary>
		/// <param name="node">The node to unselect.</param>
		/// <returns>True if the node was unselected; otherwise, false.</returns>
		/// <remarks>
		/// The handles will be removed from the node if it is unselected.
		/// </remarks>
		protected virtual bool InternalUnselect(PNode node) {
			if (!IsSelected(node)) {
				return false;
			}
		
			UndecorateSelectedNode(node);
			selection.Remove(node);
			return true;
		}

		/// <summary>
		/// Unselects the given node, if it is currently selected, and posts a
		/// SELECTION_CHANGED_NOTIFICATION if the current selection has changed.
		/// </summary>
		/// <param name="node">The node to unselect.</param>
		/// <remarks>
		/// The handles will be removed from the node if it is unselected.
		/// </remarks>
		public virtual void Unselect(PNode node) {
			if (InternalUnselect(node)) {
				PostSelectionChanged();
			}
		}

		/// <summary> 
		/// Removes bounds handles from the given node.
		/// </summary>
		/// <param name="node">The node to undecorate.</param>
		public virtual void UndecorateSelectedNode(PNode node) {
			PBoundsHandle.RemoveBoundsHandlesFrom(node);
		}

		/// <summary>
		/// Unselects all the nodes that are currently selected, and posts a
		/// SELECTION_CHANGED_NOTIFICATION if the current selection has changed.
		/// </summary>
		/// <remarks>
		/// This method will remove the handles from all of the selected nodes.
		/// </remarks>
		public virtual void UnselectAll() {
			//  Because unselect() removes from selection, we need to
			//  take a copy of it first so it isn't changed while we're iterating
			ArrayList sel = new ArrayList(selection.Keys);
			Unselect(sel);
		}

		/// <summary>
		/// Returns true if the specified node is currently selected and false
		/// otherwise.
		/// </summary>
		/// <param name="node">The node to test.</param>
		/// <returns>True if the node is selected; otherwise, false.</returns>
		public virtual bool IsSelected(PNode node) {
			if ((node != null) && (selection.ContainsKey(node))) {
				return true;
			} else {
				return false;
			}
		}

		/// <summary>
		/// Gets a copy of the currently selected nodes.
		/// </summary>
		public virtual PNodeList Selection {
			get {
				PNodeList selCopy = new PNodeList();

				ICollection sel = selection.Keys;
				foreach (PNode each in sel) {
					selCopy.Add(each);
				}
				return selCopy;
			}
		}

		/// <summary>
		/// Gets a reference to the currently selected nodes.  You should not
		/// modify or store this collection.
		/// </summary>
		public virtual ICollection SelectionReference {
			get {
				return selection.Keys;
			}
		}

		/// <summary>
		/// Determines if the specified node is selectable (i.e., if it is a child
		/// of a node in the list of selectable parents).
		/// </summary>
		/// <param name="node">The node to test.</param>
		/// <returns>True if the node is selectable; otherwise, false.</returns>
		protected virtual bool IsSelectable(PNode node) {
			bool selectable = false;

			foreach (PNode parent in selectableParents) {
				if (parent.ChildrenReference.Contains(node)) {
					selectable = true;
					break;
				}
				else if (parent is PCamera) {
					PCamera cameraParent = (PCamera)parent;
					for(int i=0; i<cameraParent.LayerCount; i++) {
						PLayer layer = cameraParent.GetLayer(i);	
						if (layer.ChildrenReference.Contains(node)) {
							selectable = true;
							break;	
						}
					}
				}
			}
		
			return selectable;
		}
		#endregion

		#region Selectable Parents
		//****************************************************************
		// Selectable Parents - Methods for modifying the set of
		// selectable parents
		//****************************************************************

		/// <summary>
		/// Adds the specified node to the list of selectable parents.
		/// </summary>
		/// <param name="node">The node to add.</param>
		/// <remarks>
		/// Only nodes whose parents are added to the selectable parents list will
		/// be selectable.
		/// </remarks>
		public virtual void AddSelectableParent(PNode node) {
			selectableParents.Add(node);
		}

		/// <summary>
		/// Removes the specified node from the list of selectable parents.
		/// </summary>
		/// <param name="node">The node to remove.</param>
		public virtual void RemoveSelectableParent(PNode node) {
			selectableParents.Remove(node);
		}

		/// <summary>
		/// Clears the list of selectable parents and sets the given node as the only
		/// selectable parent.
		/// </summary>
		/// <value>The node whose children will be selectable.</value>
		public virtual PNode SelectableParent {
			set {
				selectableParents.Clear();
				selectableParents.Add(value);
			}
		}

		/// <summary>
		/// Gets or sets the list of selectable parents.
		/// </summary>
		/// <value>The list of selectable parents.</value>
		/// <remarks>
		/// Only nodes whose parents are added in the selectable parents list will
		/// be selectable.
		/// </remarks>
		public virtual PNodeList SelectableParents {
			set {
				selectableParents.Clear();

				if (value != null) {
					foreach(PNode each in value) {
						selectableParents.Add(each);
					}
				}
			}
			get {
				return new PNodeList(selectableParents);
			}
		}
		#endregion

		#region Dragging
		//****************************************************************
		// Dragging - Overridden methods from PDragSequenceEventHandler
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnStartDrag">
		/// PDragSequenceEventHandler.OnStartDrag</see>.
		/// </summary>
		protected override void OnStartDrag(object sender, PInputEventArgs e) {
			base.OnStartDrag (sender, e);

			InitializeSelection(e); 			

			if (IsMarqueeSelection(e)) {
				InitializeMarquee(e);

				if (!IsOptionSelection(e)) {
					StartMarqueeSelection(e);
				}
				else {
					StartOptionMarqueeSelection(e);
				}
			}
			else {					
				if (!IsOptionSelection(e)) {
					StartStandardSelection(e);
				} else {
					StartStandardOptionSelection(e);
				}
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnDrag">
		/// PDragSequenceEventHandler.OnDrag</see>.
		/// </summary>
		protected override void OnDrag(object sender, PInputEventArgs e) {
			base.OnDrag (sender, e);

			if (IsMarqueeSelection(e)) {
				UpdateMarquee(e);	

				if (!IsOptionSelection(e)) {
					ComputeMarqueeSelection(e);
				}
				else {
					ComputeOptionMarqueeSelection(e);
				}
			} else {
				DragStandardSelection(e);
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="PDragSequenceEventHandler.OnEndDrag">
		/// PDragSequenceEventHandler.OnEndDrag</see>.
		/// </summary>
		protected override void OnEndDrag(object sender, PInputEventArgs e) {
			base.OnEndDrag (sender, e);

			if (IsMarqueeSelection(e)) {
				EndMarqueeSelection(e); 
			}
			else {
				EndStandardSelection(e);
			}
		}
		#endregion

		#region Additional Methods
		//****************************************************************
		// Additional Methods - Other miscellaneous methods.
		//****************************************************************

		/// <summary>
		/// Returns a value indicating whether or not the input event was modified
		/// with a <c>Shift</c> key, in which case multiple selections will be
		/// allowed.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the input event was modified with a shift key; otherwise false.
		/// </returns>
		public virtual bool IsOptionSelection(PInputEventArgs e) {
			return (e.Modifiers & Keys.Shift) == Keys.Shift;
		}

		/// <summary>
		/// Returns a value indicating whether the event represents a marquee
		/// selection or a normal selection.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the input event represents a marquee selection; otherwise, false.
		/// </returns>
		public virtual bool IsMarqueeSelection(PInputEventArgs e) {
			return (pressNode == null);
		}

		/// <summary>
		/// Sets the initial press point and press node for the selection.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void InitializeSelection(PInputEventArgs e) {
			canvasPressPt = e.CanvasPosition;
			presspt = e.Position;
			pressNode = e.Path.PickedNode;
			if (pressNode is PCamera) {
				pressNode = null;
			}		
		}

		/// <summary>
		/// Sets some initial values for the marquee including it's brush and pen.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void InitializeMarquee(PInputEventArgs e) {
			marquee = PPath.CreateRectangle(presspt.X, presspt.Y, 0, 0);
			marquee.Brush = marqueeBrush;
			marquee.Pen = pens[0];
			marqueeParent.AddChild(marquee);

			marqueeMap.Clear();
		}

		/// <summary>
		/// Starts an option marquee selection sequence (i.e. a marquee selection
		/// sequence where the <c>Shift</c> key was pressed).
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified at
		/// the beginning of an option marquee selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.StartOptionMarqueeSelection()</c> for
		/// correct behavior.
		/// </para>
		/// </remarks>
		protected virtual void StartOptionMarqueeSelection(PInputEventArgs e) {
		}

		/// <summary>
		/// Starts a marquee selection sequence.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified
		/// at the beginning of a marquee selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.StartMarqueeSelection()</c> for correct
		/// behavior.
		/// </para>
		/// </remarks>
		protected virtual void StartMarqueeSelection(PInputEventArgs e) {
			UnselectAll();
		}

		/// <summary>
		/// Starts a standard selection sequence.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified at
		/// the beginning of a standard selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.StartStandardSelection()</c> for correct
		/// behavior.
		/// </para>
		/// </remarks>
		protected virtual void StartStandardSelection(PInputEventArgs e) {
			// Option indicator not down - clear selection, and start fresh
			if (!IsSelected(pressNode)) {
				UnselectAll();
			
				if (IsSelectable(pressNode)) {
					Select(pressNode);
				}
			}
		}

		/// <summary>
		/// Starts an option selection sequence (i.e. a selection sequence where the
		/// <c>Shift</c> key was pressed).
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified at
		/// the beginning of an option selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.StartStandardOptionSelection()</c> for
		/// correct behavior.
		/// </para>
		/// </remarks>
		protected virtual void StartStandardOptionSelection(PInputEventArgs e) {
			// Option indicator is down, toggle selection
			if (IsSelectable(pressNode)) {
				if (IsSelected(pressNode)) {
					Unselect(pressNode);
				} else {
					Select(pressNode);
				}
			}
		}

		/// <summary>
		/// Update the marquee bounds based on the given event data.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void UpdateMarquee(PInputEventArgs e) {
			RectangleF r = RectangleF.Empty;

			if (marqueeParent is PCamera) {
				r = PUtil.AddPointToRect(r, canvasPressPt);
				r = PUtil.AddPointToRect(r, e.CanvasPosition);
			}
			else {
				r = PUtil.AddPointToRect(r, presspt);
				r = PUtil.AddPointToRect(r, e.Position);
			}

			r = marquee.GlobalToLocal(r);
			marquee.Reset();
			SetSafeMarqueePen(r.Width, r.Height);
			marquee.AddRectangle(r.X, r.Y, r.Width, r.Height);

			r = RectangleF.Empty;
			r = PUtil.AddPointToRect(r, presspt);
			r = PUtil.AddPointToRect(r, e.Position);

			allItems.Clear();
			PNodeFilter filter = CreateNodeFilter(r);
			foreach (PNode parent in selectableParents) {			
				PNodeList items;
				if (parent is PCamera) {
					items = new PNodeList();
					PCamera cameraParent = (PCamera)parent;
					for(int i=0; i<cameraParent.LayerCount; i++) {
						cameraParent.GetLayer(i).GetAllNodes(filter,items);	
					}
				}
				else {
					items = parent.GetAllNodes(filter, null);
				}
			
				foreach (PNode node in items) {
					allItems.Add(node, true);
				}
			}
		}

		/// <summary>
		/// When the width and the height are too small to render the path using the current
		/// pen, this method sets the marquee to a pen that can render the path safely.
		/// </summary>
		/// <param name="width">The width of the marquee.</param>
		/// <param name="height">The height of the marquee.</param>
		/// <returns>True if the marquee pen was changed; otherwise, false.</returns>
		/// <remarks>
		/// This method is necessary to handle the case where the perimeter of the marquee
		/// is less than the DashWidth.  In that case, if the path is rendered with a pen that
		/// has a DashOffset greater than or equal to the DashWidth, .NET will throw a
		/// <see cref="System.OutOfMemoryException">System.OutOfMemeoryException</see>.
		/// </remarks>
		protected virtual bool SetSafeMarqueePen(float width, float height) {
			if (DASH_WIDTH >= (width * 2 + height * 2)) {
				marquee.Pen = pens[Math.Min((int)penNum, DASH_WIDTH-1)];
				return true;
			}
			return false;
		}

		/// <summary>
		/// Select the selectable nodes whose bounds intersect the marquee, unselecting
		/// previously selected nodes.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void ComputeMarqueeSelection(PInputEventArgs e) {
			unselectList.Clear();
			// Make just the items in the list selected
			// Do this efficiently by first unselecting things not in the list
			ICollection sel = selection.Keys;
			foreach (PNode node in sel) {
				if (!allItems.ContainsKey(node)) {
					unselectList.Add(node);
				}
			}
			Unselect(unselectList);

			// Then select the rest
			TEMP_LIST.Clear();
			TEMP_LIST.AddRange(allItems.Keys);
			
			foreach (PNode node in TEMP_LIST) {
				if (!selection.ContainsKey(node) && !marqueeMap.ContainsKey(node) && IsSelectable(node)) {
					marqueeMap.Add(node,true);
				}
				else if (!IsSelectable(node)) {
					allItems.Remove(node);
				}
			}
		
			Select(allItems);			
		}

		/// <summary>
		/// Select the selectable nodes whose bounds intersect the marquee, without
		/// unselecting previously selected nodes.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void ComputeOptionMarqueeSelection(PInputEventArgs e) {
			unselectList.Clear();
			// Make just the items in the list selected
			// Do this efficiently by first unselecting things not in the list
			ICollection sel = selection.Keys;
			foreach (PNode node in sel) {
				if (!allItems.ContainsKey(node) && marqueeMap.ContainsKey(node)) {
					marqueeMap.Remove(node);
					unselectList.Add(node);
				}
			}
			Unselect(unselectList);
		
			// Then select the rest
			TEMP_LIST.Clear();
			TEMP_LIST.AddRange(allItems.Keys);

			foreach (PNode node in TEMP_LIST) {
				if (!selection.ContainsKey(node) && !marqueeMap.ContainsKey(node) && IsSelectable(node)) {
					marqueeMap.Add(node,true);
				}
				else if (!IsSelectable(node)) {
					allItems.Remove(node);
				}
			}

			Select(allItems);
		}

		/// <summary>
		/// Creates a new <see cref="BoundsFilter"/> with the given bounds.
		/// </summary>
		/// <param name="bounds">The bounds for the <see cref="BoundsFilter"/>.</param>
		/// <returns>Returns a new <see cref="BoundsFilter"/>.</returns>
		protected virtual PNodeFilter CreateNodeFilter(RectangleF bounds) {
			return new BoundsFilter(bounds, this);
		}

		/// <summary>
		/// Gets the bounds of the marquee, if the marquee exists.
		/// </summary>
		/// <value>
		/// The bounds of the marquee, if it exists; otherwise,
		/// <see cref="RectangleF.Empty"/>.
		/// </value>
		protected virtual RectangleF MarqueeBounds {
			get {
				if (marquee != null) {
					return marquee.Bounds;
				}	
				return RectangleF.Empty;
			}
		}

		/// <summary>
		/// Drags the nodes in a standard selection.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified
		/// while a standard selection is being dragged.
		/// <para>
		/// Overriding methods must still call <c>base.DragStandardSelection()</c> for correct
		/// behavior.
		/// </para>
		/// </remarks>
		protected virtual void DragStandardSelection(PInputEventArgs e) {
			// There was a press node, so drag selection
			SizeF s = e.CanvasDelta;
			s = e.TopCamera.LocalToView(s);

			ICollection sel = selection.Keys;
			foreach (PNode node in sel) {
				s = node.Parent.GlobalToLocal(s);
				node.OffsetBy(s.Width, s.Height);
			}
		}

		/// <summary>
		/// Ends a marquee selection sequence.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified
		/// at the end of a marquee selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.EndMarqueeSelection()</c> for correct
		/// behavior.
		/// </para>
		/// </remarks>
		protected virtual void EndMarqueeSelection(PInputEventArgs e) {
			// Remove marquee
			marquee.RemoveFromParent();
			marquee = null;
		}

		/// <summary>
		/// Ends a standard selection sequence.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <remarks>
		/// <b>Notes to Inheritors:</b>  Subclasses can override this method to be notified
		/// at the end of a standard selection sequence.
		/// <para>
		/// Overriding methods must still call <c>base.EndStandardSelection()</c> for correct
		/// behavior.
		/// </para>
		/// </remarks>
		protected virtual void EndStandardSelection(PInputEventArgs e) {
			pressNode = null;
		}

		/// <summary>
		/// Overridden.  This gets called continuously during the drag, and is used
		/// to animate the marquee.
		/// </summary>
		/// <param name="sender">The source of the PInputEvent.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected override void OnDragActivityStep(object sender, PInputEventArgs e) {
			base.OnDragActivityStep (sender, e);
			if (marquee != null) {
				float origPenNum = penNum;
				penNum = (penNum + 0.5f) % NUM_PENS;	// Increment by partial steps to slow down animation
				if ((int)penNum != (int)origPenNum && !SetSafeMarqueePen(marquee.Width, marquee.Height)) {
					marquee.Pen = pens[(int)penNum];
				}
			}
		}

		/// <summary>
		/// Overridden.  Deletes selection when delete key is pressed (if enabled).
		/// </summary>
		/// <param name="sender">The source of the PInputEvent.</param>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		public override void OnKeyDown(object sender, PInputEventArgs e) {
			base.OnKeyDown (sender, e);
			switch (e.KeyCode) {
				case Keys.Delete:
					if (deleteKeyActive) {
						ICollection sel = selection.Keys;
						foreach (PNode node in sel) {
							node.RemoveFromParent();
						}
						selection.Clear();
					}
					break;
			}
		}

		/* Ask Jesse why this is here.
		public virtual bool SupportDeleteKey {
			get { return deleteKeyActive; }
		}
		*/

		/// <summary>
		/// Gets or sets a value that indicates whether or not the <c>Delete</c> key
		/// should delete the selection.
		/// </summary>
		public virtual bool DeleteKeyActive {
			get { return deleteKeyActive; }
			set { deleteKeyActive = value; }
		}
		#endregion

		#region Inner Classes
		//****************************************************************
		// Inner Classes - Miscellaneous classes used by
		// PSelectionEventHandler.
		//****************************************************************

		/// <summary>
		/// A node filter that accepts nodes whose bounds intersect the specified
		/// bounds.
		/// </summary>
		/// <remarks>
		/// This class is used during a marquee selection to retrieve nodes that
		/// intersect the bounds of the marquee.
		/// </remarks>
		protected class BoundsFilter : PNodeFilter {
			/// <summary>
			/// The current selection handler.
			/// </summary>
			protected PSelectionEventHandler selectionHandler;

			/// <summary>
			/// The bounds to use when deciding whether to accept a node.
			/// </summary>
			protected RectangleF bounds;

			/// <summary>
			/// The bounds stored in the local coordinates of the node being checked.
			/// </summary>
			protected RectangleF localBounds = RectangleF.Empty;
		
			/// <summary>
			/// Constructs a new BoundsFilter with the given bounds and selectionHandler.
			/// </summary>
			/// <param name="bounds">
			/// The bounds to use when deciding whether to accept a node.
			/// </param>
			/// <param name="selectionHandler">
			/// The selection event handler to which this BoundsFilter will apply.
			/// </param>  
			public BoundsFilter(RectangleF bounds, PSelectionEventHandler selectionHandler) {
				this.bounds = bounds;
				this.selectionHandler = selectionHandler;
			}

			/// <summary>
			/// Returns true if the node's bounds intersects the bounds specified by the
			/// BoundsFilter.
			/// </summary>
			/// <remarks>
			/// For a node to be accepted, it must also satisfy the following conditions:
			/// it must be pickable, it must not be the marquee node, it must not be a
			/// selectable parent, and the it must not be a layer that is viewed by a camera
			/// that is a selectable parent
			/// </remarks>
			/// <param name="node">The node to test.</param>
			/// <returns>True if the node is accepted; otherwise, false.</returns>
			public virtual bool Accept(PNode node) {
				localBounds = bounds;
				localBounds = node.GlobalToLocal(localBounds);

				bool boundsIntersects = node.Intersects(localBounds);
				bool isMarquee = (node == selectionHandler.marquee);
				return (node.Pickable && boundsIntersects && !isMarquee &&
					!selectionHandler.selectableParents.Contains(node) && !IsCameraLayer(node));
			}

			/// <summary>
			/// Returns true if the node is a selectable parent or a layer that is viewed
			/// by a camera that is a selectable parent.
			/// </summary>
			/// <param name="node">The node to test.</param>
			/// <returns>
			/// True if the node's children should be accepted; otherwise, false.
			/// </returns>
			public virtual bool AcceptChildrenOf(PNode node) {
				return selectionHandler.selectableParents.Contains(node) || IsCameraLayer(node);
			}

			/// <summary>
			/// Returns true if the node a layer that is viewed by a camera that is a
			/// selectable parent.
			/// </summary>
			/// <param name="node">The node to test.</param>
			/// <returns>
			/// True if the node is a layer that is viewed by a camera that is a selectable
			/// parent; otherwise, false.
			/// </returns>
			public virtual bool IsCameraLayer(PNode node) {
				if (node is PLayer) {
					foreach (PNode parent in selectionHandler.selectableParents) {
						if (parent is PCamera) {
							if (((PCamera)parent).IndexOfLayer((PLayer)node) != -1) {
								return true;
							}
						}
					}	
				}
				return false;
			}
		}
		#endregion

		#region Marquee Appearance
		//****************************************************************
		// Marquee Appearance - Methods for manipulating the appearance of
		// the marquee.
		//****************************************************************

		/// <summary>
		/// Gets or sets the brush used to fill the interior of the marquee.
		/// </summary>
		/// <value>The brush used to paint the marquee.</value>
		public virtual Brush MarqueeBrush {
			get { return marqueeBrush; }
			set { this.marqueeBrush = value; }
		}
		#endregion
	}
}