using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace Transitions
{
    /// <summary>
    /// This class is responsible for running transitions. It holds the timer that
    /// triggers transaction animation. 
    /// </summary><remarks>
    /// This class is a singleton.
    /// 
    /// We manage the transaction timer here so that we can have a single timer
    /// across all transactions. If each transaction has its own timer, this creates
    /// one thread for each transaction, and this can lead to too many threads in
    /// an application.
    /// 
    /// This class essentially just manages the timer for the transitions. It calls 
    /// back into the running transitions, which do the actual work of the transition.
    /// 
    /// </remarks>
    internal class TransitionManager
    {
        #region Public methods

        /// <summary>
        /// Singleton's getInstance method.
        /// </summary>
        public static TransitionManager getInstance()
        {
            if (m_Instance == null)
            {
                m_Instance = new TransitionManager();
            }
            return m_Instance;
        }

        /// <summary>
        /// You register a transition with the manager here. This will start to run
        /// the transition as the manager's timer ticks.
        /// </summary>
        public void register(Transition transition)
        {
            lock (m_Lock)
            {
                // We check to see if the properties of this transition
                // are already being animated by any existing transitions...
                removeDuplicates(transition);

                // We add the transition to the collection we manage, and 
                // observe it so that we know when it has completed...
                m_Transitions[transition] = true;
                transition.TransitionCompletedEvent += onTransitionCompleted;
            }
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Checks if any existing transitions are acting on the same properties as the
        /// transition passed in. If so, we remove the duplicated properties from the 
        /// older transitions.
        /// </summary>
        private void removeDuplicates(Transition transition)
        {
            // We look through the set of transitions we're currently managing...
            foreach (KeyValuePair<Transition, bool> pair in m_Transitions)
            {
                removeDuplicates(transition, pair.Key);
            }
        }

        /// <summary>
        /// Finds any properties in the old-transition that are also in the new one,
        /// and removes them from the old one.
        /// </summary>
        private void removeDuplicates(Transition newTransition, Transition oldTransition)
        {
            // Note: This checking might be a bit more efficient if it did the checking
            //       with a set rather than looking through lists. That said, it is only done 
            //       when transitions are added (which isn't very often) rather than on the
            //       timer, so I don't think this matters too much.

            // We get the list of properties for the old and new transitions...
            IList<Transition.TransitionedPropertyInfo> newProperties = newTransition.TransitionedProperties;
            IList<Transition.TransitionedPropertyInfo> oldProperties = oldTransition.TransitionedProperties;

            // We loop through the old properties backwards (as we may be removing 
            // items from the list if we find a match)...
            for (int i = oldProperties.Count - 1; i >= 0; i--)
            {
                // We get one of the properties from the old transition...
                Transition.TransitionedPropertyInfo oldProperty = oldProperties[i];

                // Is this property part of the new transition?
                foreach (Transition.TransitionedPropertyInfo newProperty in newProperties)
                {
                    if (oldProperty.target == newProperty.target
                        &&
                        oldProperty.propertyInfo == newProperty.propertyInfo)
                    {
                        // The old transition contains the same property as the new one,
                        // so we remove it from the old transition...
                        oldTransition.removeProperty(oldProperty);
                    }
                }
            }
        }

        /// <summary>
        /// Private constructor (for singleton).
        /// </summary>
        private TransitionManager()
        {
            m_Timer = new Timer(15);
            m_Timer.Elapsed += onTimerElapsed;
            m_Timer.Enabled = true;
        }

        /// <summary>
        /// Called when the timer ticks.
        /// </summary>
        private void onTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // We turn the timer off while we process the tick, in case the
            // actions take longer than the tick itself...
            if (m_Timer == null)
            {
                return;
            }
            try
            {
                m_Timer.Enabled = false;
            }
            catch
            {
                return;
            }
            

            IList<Transition> listTransitions;
            lock (m_Lock)
            {
                // We take a copy of the collection of transitions as elements 
                // might be removed as we iterate through it...
                listTransitions = new List<Transition>();
                foreach (KeyValuePair<Transition, bool> pair in m_Transitions)
                {
                    listTransitions.Add(pair.Key);
                }
            }

            // We tick the timer for each transition we're managing...
            foreach (Transition transition in listTransitions)
            {
                transition.onTimer();
            }

            // We restart the timer...
            m_Timer.Enabled = true;
        }

        /// <summary>
        /// Called when a transition has completed. 
        /// </summary>
        private void onTransitionCompleted(object sender, Transition.Args e)
        {
            // We stop observing the transition...
            Transition transition = (Transition)sender;
            transition.TransitionCompletedEvent -= onTransitionCompleted;

            // We remove the transition from the collection we're managing...
            lock (m_Lock)
            {
                m_Transitions.Remove(transition);
            }
        }

        #endregion

        #region Private data

        // The singleton instance...
        private static TransitionManager m_Instance = null;

        // The collection of transitions we're managing. (This should really be a set.)
        private IDictionary<Transition, bool> m_Transitions = new Dictionary<Transition, bool>();

        // The timer that controls the transition animation...
        private Timer m_Timer = null;

        // An object to lock on. This class can be accessed by multiple threads: the 
        // user thread can add new transitions; and the timerr thread can be animating 
        // them. As they access the same collections, the methods need to be protected 
        // by a lock...
        private object m_Lock = new object();

        #endregion
    }
}


