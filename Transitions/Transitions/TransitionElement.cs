using System;
using System.Collections.Generic;
using System.Text;

namespace Transitions
{
    public enum InterpolationMethod
    {
        Linear,
        Accleration,
        Deceleration,
        EaseInEaseOut
    }

    public class TransitionElement
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public TransitionElement(double endTime, double endValue, InterpolationMethod interpolationMethod)
        {
            EndTime = endTime;
            EndValue = endValue;
            InterpolationMethod = interpolationMethod;
        }

        /// <summary>
        /// The percentage of elapsed time, expressed as (for example) 75 for 75%.
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// The value of the animated properties at the EndTime. This is the percentage 
        /// movement of the properties between their start and end values. This should
        /// be expressed as (for example) 75 for 75%.
        /// </summary>
        public double EndValue { get; set; }

        /// <summary>
        /// The interpolation method to use when moving between the previous value
        /// and the current one.
        /// </summary>
        public InterpolationMethod InterpolationMethod { get; set; }
    }
}
