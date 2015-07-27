using System;
using System.Collections.Generic;
using System.Text;

namespace Transitions
{
    /// <summary>
    /// This transition bounces the property to a destination value and back to the
    /// original value. It is accelerated to the destination and then decelerated back
    /// as if being dropped with gravity and bouncing back against gravity.
    /// </summary>
    public class TransitionType_Bounce : TransitionType_UserDefined
    {
        #region Public methods

        /// <summary>
        /// Constructor. You pass in the total time taken for the bounce.
        /// </summary>
        public TransitionType_Bounce(int iTransitionTime)
        {
            // We create a custom "user-defined" transition to do the work...
            IList<TransitionElement> elements = new List<TransitionElement>();
            elements.Add(new TransitionElement(50, 100, InterpolationMethod.Accleration));
            elements.Add(new TransitionElement(100, 0, InterpolationMethod.Deceleration));
            base.setup(elements, iTransitionTime);
        }

        #endregion
    }
}
