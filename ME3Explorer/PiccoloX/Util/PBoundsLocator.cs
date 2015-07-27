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
using System.Drawing;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Util {
  	#region Enums
	/// <summary>
	/// This enumeration represents both compass directions (e.g. North and South) as well as
	/// directions in the zooming space (e.g. In and Out).
	/// </summary>
	public enum Direction {
		/// <summary>
		/// The East compass direction.
		/// </summary>
		East,

		/// <summary>
		/// The North East compass direction.
		/// </summary>
		NorthEast,

		/// <summary>
		/// The North West compass direction.
		/// </summary>
		NorthWest,

		/// <summary>
		/// The North compass direction.
		/// </summary>
		North,

		/// <summary>
		/// The South compass direction.
		/// </summary>
		South,

		/// <summary>
		/// The West compass direction.
		/// </summary>
		West,

		/// <summary>
		/// The South West compass direction.
		/// </summary>
		SouthWest,

		/// <summary>
		/// The South East compass direction.
		/// </summary>
		SouthEast,

		/// <summary>
		/// Inward zoom direction.
		/// </summary>
		In,

		/// <summary>
		/// Outward zoom direction.
		/// </summary>
		Out
	}
	#endregion

	/// <summary>
	/// <b>PBoundsLocator</b> is a locator that locates points on the bounds of a node.
	/// </summary>
	[Serializable]
	public class PBoundsLocator : PNodeLocator {
		#region Fields
		private Direction side;
		#endregion

		#region Create Locators
		/// <summary>
		/// Creates a locator that will locate points in the East (right) side of the
		/// node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the East side of the node's bounds.
		/// </returns>
		public static PBoundsLocator CreateEastLocator(PNode node) {
			return new PBoundsLocator(node, Direction.East);
		}

		/// <summary>
		/// Creates a locator that will locate points in the North East (upper right)
		/// corner of the node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the North East corner of the node's
		/// bounds.
		/// </returns>
		public static PBoundsLocator CreateNorthEastLocator(PNode node) {
			return new PBoundsLocator(node, Direction.NorthEast);
		}

		/// <summary>
		/// Creates a locator that will locate points in the North West (upper left)
		/// corner of the node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the North West corner of the node's
		/// bounds.
		/// </returns>
		public static PBoundsLocator CreateNorthWestLocator(PNode node) {
			return new PBoundsLocator(node, Direction.NorthWest);
		}

		/// <summary>
		/// Creates a locator that will locate points in the North (top) side of the
		/// node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the North side of the node's bounds.
		/// </returns>
		public static PBoundsLocator CreateNorthLocator(PNode node) {
			return new PBoundsLocator(node, Direction.North);
		}

		/// <summary>
		/// Creates a locator that will locate points in the South (bottom) side of the
		/// node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the South side of the node's bounds.
		/// </returns>
		public static PBoundsLocator CreateSouthLocator(PNode node) {
			return new PBoundsLocator(node, Direction.South);
		}

		/// <summary>
		/// Creates a locator that will locate points in the West (left) side of the
		/// node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the West side of the node's bounds.
		/// </returns>
		public static PBoundsLocator CreateWestLocator(PNode node) {
			return new PBoundsLocator(node, Direction.West);
		}

		/// <summary>
		/// Creates a locator that will locate points in the South West (lower left)
		/// corner of the node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the South West corner of the node's
		/// bounds.
		/// </returns>
		public static PBoundsLocator CreateSouthWestLocator(PNode node) {
			return new PBoundsLocator(node, Direction.SouthWest);
		}

		/// <summary>
		/// Creates a locator that will locate points in the South East (lower right)
		/// corner of the node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate the point.</param>
		/// <returns>
		/// A locator that will locate points on the South East corner of the node's
		/// bounds.
		/// </returns>
		public static PBoundsLocator CreateSouthEastLocator(PNode node) {
			return new PBoundsLocator(node, Direction.SouthEast);
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Creates a new PBoundsLocator that will locate points in the given direction
		/// specified node's bounds.
		/// </summary>
		/// <param name="node">The node on which to locate points.</param>
		/// <param name="aSide">The direction in which to locate points.</param>
		public PBoundsLocator(PNode node, Direction aSide) : base(node) {
			side = aSide;
		}
		#endregion

		#region Direction
		/// <summary>
		/// Gets the direction in which this locator will locate points on the node's
		/// bounds.
		/// </summary>
		/// <value>The direction in which this locator will locate points.</value>
		public Direction Side {
			get { return side; }
			set { side = value; }
		}
		#endregion

		#region Locate Point
		/// <summary>
		/// Overridden.  Gets the x coordinate of the point located on the node's bounds
		/// in the specified direction.
		/// </summary>
		/// <value>The x coordinate of the located point.</value>
		public override float LocateX {
			get {
				RectangleF aBounds = node.Bounds;

				switch (side) {
					case Direction.NorthWest :
					case Direction.SouthWest :
					case Direction.West :
						return aBounds.X;

					case Direction.NorthEast :
					case Direction.SouthEast :
					case Direction.East :
						return aBounds.X + aBounds.Width;

					case Direction.North :
					case Direction.South :
						return aBounds.X + (aBounds.Width / 2);
				}
				return -1;
			}
		}

		/// <summary>
		/// Overridden.  Gets the y coordinate of the point located on the node's bounds
		/// in the specified direction.
		/// </summary>
		/// <value>The y coordinate of the located point.</value>
		public override float LocateY {
			get {
				RectangleF aBounds = node.Bounds;

				switch (side) {
					case Direction.East:
					case Direction.West:
						return aBounds.Y + (aBounds.Height / 2);

					case Direction.South:
					case Direction.SouthWest:
					case Direction.SouthEast:
						return aBounds.Y + aBounds.Height;

					case Direction.NorthWest:
					case Direction.NorthEast:
					case Direction.North:
						return aBounds.Y;
				}
				return -1;
			}
		}
		#endregion
	}
}
