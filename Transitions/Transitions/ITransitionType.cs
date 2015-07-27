using System;
using System.Collections.Generic;
using System.Text;

namespace Transitions
{
    public interface ITransitionType
    {
		/// <summary>
		/// Called by the Transition framework when its timer ticks to pass in the
		/// time (in ms) since the transition started. 
		/// 
		/// You should return (in an out parameter) the percentage movement towards 
		/// the destination value for the time passed in. Note: this does not need to
		/// be a smooth transition from 0% to 100%. You can overshoot with values
		/// greater than 100% or undershoot if you need to (for example, to have some
		/// form of "elasticity").
		/// 
		/// The percentage should be returned as (for example) 0.1 for 10%.
		/// 
		/// You should return (in an out parameter) whether the transition has completed.
		/// (This may not be at the same time as the percentage has moved to 100%.)
		/// </summary>
		void onTimer(int iTime, out double dPercentage, out bool bCompleted);
    }
}
