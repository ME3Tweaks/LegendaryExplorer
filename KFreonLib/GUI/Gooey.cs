using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KFreonLib.GUI
{
    /// <summary>
    /// Allows some control over changing the state (enabled/visible) of a set of controls.
    /// Features:
    ///     Exclude controls from state changes.
    ///     Switch state changes between enabling and changing visibility.
    ///     A function can replace state changes. e.g. Instead of enabling, set some text or whatever.
    /// </summary>
    public class Gooey
    {
        // KFreon: Base control for invoking on correct thread for controls
        Control BaseControl;

        // KFreon: Lists and dictionarys of controls and events etc
        List<object> Controls = new List<object>();
        Dictionary<string, bool> StateEffectModifiers = new Dictionary<string, bool>();
        Dictionary<string, object> Doers = new Dictionary<string, object>();
        Dictionary<string, bool> TypeOfChange = new Dictionary<string, bool>();
        static readonly object locker = new object();
        //List<bool> StateChangeTimings = new List<bool>();

        public Gooey(Control baseControl)
        {
            BaseControl = baseControl;
        }


        /// <summary>
        /// Changes state of all listed controls.
        /// </summary>
        /// <param name="state">If true, enables/makes visible. Functions run regardless and take state as parameter.</param>
        public void ChangeState(bool state)
        {
            lock (locker)
            {
                PerformStateChange(state);
            }
        }

        private void PerformStateChange(bool state)
        {
            // KFreon: Wait for controls to exist
            if (!BaseControl.Created)
            {
                while (!BaseControl.Created)
                    System.Threading.Thread.Sleep(50);
            }


            // KFreon: Change state of all controls
            foreach (string key in StateEffectModifiers.Keys)
            {
                foreach (Object control in Controls)
                {
                    // KFreon: Annoying change to correct control type
                    if (control.GetType().ToString().Contains("ToolStrip"))
                    {
                        ToolStripItem item = control as ToolStripItem;

                        // KFreon: Change state if item is affected by state changes
                        if (item.Name.Contains(key) && StateEffectModifiers[key])
                        {
                            string type = Doers[key].GetType().ToString();

                            /* KFreon: Decide what to do.
                                * If doer is a boolean, true means state is just state, but if false, state becomes !state
                                * If doer is an action, Run function.
                                */
                            string thiasafs = item.Name;
                            string kjhsdlihg = control.GetType().ToString();
                            if (BaseControl.InvokeRequired)
                                BaseControl.Invoke(new Action(() =>
                                {
                                    ControlChange(type, key, state, item);
                                }));
                            else
                                ControlChange(type, key, state, item);
                        }
                    }
                    else
                    {
                        Control item = control as Control;

                        // KFreon: Change state if item is affected by state changes
                        if (item.Name.Contains(key) && StateEffectModifiers[key])
                        {
                            string type = Doers[key].GetType().ToString();
                            bool newState = state;

                            /* KFreon: Decide what to do.
                                * If doer is a boolean, true means state is just state, but if false, state becomes !state
                                * If doer is an action, Run function.
                                */
                            if (BaseControl.InvokeRequired)
                                BaseControl.Invoke(new Action(() =>
                                {
                                    ControlChange(type, key, state, item);
                                }));
                            else
                                ControlChange(type, key, state, item);
                        }
                    }
                }
            }
        }

        private void ControlChange(string type, string key, bool state, ToolStripItem item)
        {
            if (type.Contains("Action"))
                ((Action<bool>)Doers[key])(state);
            else
            {
                bool newState = !(state ^ (bool)Doers[key]);

                // KFreon: Decide what property of control to change
                if (TypeOfChange[key])
                    item.Enabled = newState;
                else
                    item.Visible = newState;
            }
        }

        private void ControlChange(string type, string key, bool state, Control item)
        {
            if (type.Contains("Action"))
                ((Action<bool>)Doers[key])(state);
            else
            {
                bool newState = !(state ^ (bool)Doers[key]);

                // KFreon: Decide what property of control to change
                if (TypeOfChange[key])
                    item.Enabled = newState;
                else
                    item.Visible = newState;
            }
        }

        /// <summary>
        /// Adds given control to list of controls to be maintained by this class.
        /// </summary>
        /// <param name="control">Control to be added (ToolStripItem or Form Control)</param>
        /// <param name="key">Key to identify control. MUST be a unique part of the Control name.</param>
        /// <param name="stateChangeEffect">Effect to be run on state change. Bool or Action.</param>
        /// <param name="affectedByStateChange">Optional. If true, this control is affected by state changes.</param>
        /// <param name="trueIsEnabledProperty">Optional. If true, property affected by state changes is the Enabled property, otherwise uses the visible property.</param>
        /// <returns></returns>
        public bool AddControl(object control, string key, object stateChangeEffect, bool affectedByStateChange = true, bool trueIsEnabledProperty = true)
        {
            lock (locker)
            {
                // KFreon: Disallow duplicates
                if (StateEffectModifiers.ContainsKey(key))
                    return false;

                // KFreon: Add things to things
                Controls.Add(control);
                Doers.Add(key, stateChangeEffect);
                StateEffectModifiers.Add(key, affectedByStateChange);
                TypeOfChange.Add(key, trueIsEnabledProperty);
            }
            return true;
        }


        /// <summary>
        /// Modifies control specified by key, changes whether control is affected by state changes.
        /// </summary>
        /// <param name="key">Key of control to change.</param>
        /// <param name="AffectedByStateChange">If true, control is affected by state changes.</param>
        public void ModifyControl(string key, bool AffectedByStateChange)
        {
            lock (locker)
            {
                // KFreon: Ensure key is valid
                if (StateEffectModifiers.ContainsKey(key))
                    StateEffectModifiers[key] = AffectedByStateChange;
                else
                    throw new Exception("Key not found: " + key);
            }
        }


        /// <summary>
        /// Gets whether control specified by key is affected by state changes.
        /// </summary>
        /// <param name="key">Key of control to look for.</param>
        /// <returns>True if control is affected by state changes, else false.</returns>
        public bool GetControlAffectedState(string key)
        {
            lock (locker)
                return StateEffectModifiers[key];
        }
    }
}
