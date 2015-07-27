using System;
using System.Collections.Generic;
using System.Text;

namespace Transitions
{
    /// <summary>
    /// This transition bounces the property to a destination value and back to the
    /// original value. It is decelerated to the destination and then acclerated back
    /// as if being thrown against gravity and then descending back with gravity.
    /// </summary>
    public class TransitionType_ThrowAndCatch : TransitionType_UserDefined
    {
        #region Public methods

        /// <summary>
        /// Constructor. You pass in the total time taken for the bounce.
        /// </summary>
        public TransitionType_ThrowAndCatch(int iTransitionTime)
        {
            // We create a custom "user-defined" transition to do the work...
            IList<TransitionElement> elements = new List<TransitionElement>();
            elements.Add(new TransitionElement(50, 100, InterpolationMethod.Deceleration));
            elements.Add(new TransitionElement(100, 0, InterpolationMethod.Accleration));
            base.setup(elements, iTransitionTime);
        }

        #endregion
    }
}
