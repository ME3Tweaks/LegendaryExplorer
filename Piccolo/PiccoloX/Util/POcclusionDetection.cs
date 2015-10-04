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
	/// <summary>
	/// <b>POcclusionDetection</b> is an experimental class for detecting occlusions.
	/// </summary>
	public class POcclusionDetection {

		/// <summary>
		/// Traverse from the bottom right of the scene graph (top visible node)
		/// up the tree determining which parent nodes are occluded by their children
		/// nodes.
		/// </summary>
		/// <param name="n">The node to find occlusions for.</param>
		/// <param name="bounds">The bounds of <c>n</c> in parent coordinates.</param>
		/// <remarks>
		/// Note that this is only detecting a subset of occlusions (parent, child),
		/// others such as overlapping siblings or cousins are not detected.
		/// </remarks>
		public void DetectOccusions(PNode n, RectangleF bounds) {
			DetectOcclusions(n, new PPickPath(null, bounds));
		}

		/// <summary>
		/// Traverse from the bottom right of the scene graph (top visible node)
		/// up the tree determining which parent nodes are occluded by their children
		/// nodes.
		/// </summary>
		/// <param name="n">The node to find occlusions for.</param>
		/// <param name="pickPath">
		/// A pick path representing the bounds of <c>n</c> in parent coordinates.
		/// </param>
		/// <remarks>
		/// Note that this is only detecting a subset of occlusions (parent, child),
		/// others such as overlapping siblings or cousins are not detected.
		/// </remarks>
		public void DetectOcclusions(PNode n, PPickPath pickPath) {
			if (n.FullIntersects(pickPath.PickBounds)) {
				pickPath.PushMatrix(n.MatrixReference);
		
				int count = n.ChildrenCount;
				for (int i = count - 1; i >= 0; i--) {
					PNode each = n[i];
					if (n.Occluded) {
						// if n has been occluded by a previous decendent then
						// this child must also be occluded
						each.Occluded = true;
					} else {
						// see if child each occludes n
						DetectOcclusions(each, pickPath);
					}
				}

				// see if n occludes it's parents		
				if (!n.Occluded) {
					if (n.Intersects(pickPath.PickBounds)) {
						if (n.IsOpaque(pickPath.PickBounds)) {
							PNode p = n.Parent;
							while (p != null && !p.Occluded) {
								p.Occluded = true;
							}
						}
					}
				}
	
				pickPath.PopMatrix(n.MatrixReference);
			}				
		}
	}
}