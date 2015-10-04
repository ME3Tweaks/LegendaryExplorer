/* 
 * Copyright (C) 2002-2004 by University of Maryland, College Park, MD 20742, USA 
 * All rights reserved. 
 * 
 * Piccolo was written at the Human-Computer Interaction Laboratory 
 * www.cs.umd.edu/hcil by Jesse Grosjean and ported to C# by Aaron Clamage
 * under the supervision of Ben Bederson.  The Piccolo website is
 * www.cs.umd.edu/hcil/piccolo 
 */

using System;
using System.Runtime.Serialization;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Util {
	/// <summary>
	/// <b>PNodeLocator</b> provides an abstraction for locating points on a node.
	/// </summary>
	/// <remarks>
	/// Points are located in the local corrdinate system of the node.  The default
	/// behavior is to locate the center point of the node's bounds.  The node where
	/// the point is located is stored internal to this locator (as an instance
	/// variable).  If you want to use the same locator to locate center points on
	/// many different nodes, you will need to set the <see cref="PNodeLocator.Node">
	/// Node</see> property before asking for each location.
	/// </remarks>
	[Serializable]
	public class PNodeLocator : PLocator {
		#region Fields
		/// <summary>
		/// The node on which points are located.
		/// </summary>
		protected PNode node;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PNodeLocator that locates points on the given node.
		/// </summary>
		/// <param name="node">The node on which the points are located.</param>
		public PNodeLocator(PNode node) {
			Node = node;
		}
		#endregion

		#region Node
		/// <summary>
		/// Gets or sets the node on which points are located.
		/// </summary>
		/// <value>The node on which points are located.</value>
		public PNode Node {
			get { return node; }
			set { node = value; }
		}
		#endregion

		#region Locate Point
		/// <summary>
		/// Overridden.  Gets the x coordinate of the point located on the node.
		/// </summary>
		/// <value>The x coordinate of the located point.</value>
		public override float LocateX {
			get { return PUtil.CenterOfRectangle(node.Bounds).X; }
		}

		/// <summary>
		/// Overridden.  Gets the y coordinate of the point located on the node.
		/// </summary>
		/// <value>The y coordinate of the located point.</value>
		public override float LocateY {
			get { return PUtil.CenterOfRectangle(node.Bounds).Y; }
		}
		#endregion
	}
}