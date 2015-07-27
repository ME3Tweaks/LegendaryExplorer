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
using System.Runtime.Serialization;

namespace UMD.HCIL.PiccoloX.Util {
	/// <summary>
	/// <b>PLocator</b> provides an abstraction for locating points.
	/// </summary>
	/// <remarks>
	/// <b>Notes to Inheritors:</b>  Subclasses such as <see cref="PNodeLocator"/> and
	/// <see cref="PBoundsLocator"/> specialize this behavior by locating points on nodes,
	/// or on the bounds of nodes.
	/// </remarks>
	[Serializable]
	public abstract class PLocator {
		/// <summary>
		/// Constructs a new PLocator.
		/// </summary>
		public PLocator() {
		}

		/// <summary>
		/// Gets the located point.
		/// </summary>
		/// <value>The located point.</value>
		public PointF LocatePoint {
			get { return new PointF(LocateX, LocateY); }
		}

		/// <summary>
		/// Gets the x coordinate of the located point.
		/// </summary>
		public abstract float LocateX {
			get;
		}

		/// <summary>
		/// Gets the y coordinate of the located point.
		/// </summary>
		public abstract float LocateY {
			get;
		}
	}
}
