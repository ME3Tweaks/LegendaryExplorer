using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode
{
    public class FlagSet
    {
        private readonly Dictionary<string, int> _masks = new Dictionary<string, int>();

        internal FlagSet(params string[] flags)
        {
            Flags = flags;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] != null)
                {
                    _masks[flags[i]] = 1 << i;
                }
            }
        }

        internal bool HasFlag(int flags, string name)
        {
            return (flags & _masks[name]) != 0;
        }

        public int GetMask(string name)
        {
            return _masks[name];
        }

        internal string[] Flags { get; private set; }
    }

    public class FlagValues
    {
        private readonly int _flags;
        private readonly FlagSet _set;

        public FlagValues(int flags, FlagSet set)
        {
            _flags = flags;
            _set = set;
        }

        public bool HasFlag(string name)
        {
            return _set.HasFlag(_flags, name);
        }

        public FlagValues Except(params string[] exceptNames)
        {
            int newFlags = _flags;
            foreach (var name in exceptNames)
            {
                newFlags &= ~_set.GetMask(name);
            }
            return new FlagValues(newFlags, _set);
        }

        public void Each(Action<string> f)
        {
            for (int i = 0; i < _set.Flags.Length; i++)
            {
                var mask = (_flags & (1 << i));
                if (mask != 0)
                {
                    if (_set.Flags[i] != null)
                    {
                        f(_set.Flags[i]);
                    }
                    else
                    {
                        f("/* " + mask.ToString("X8") + " */");
                    }
                }
            }
        }

        public string GetFlagsString()
        {
            string result = "";
            for (int i = 0; i < _set.Flags.Length; i++)
            {
                var mask = (_flags & (1 << i));
                if (mask != 0)
                {
                    if (_set.Flags[i] != null)
                    {
                        result += _set.Flags[i].ToString() + " ";
                    }
                }
            }

            return result;
        }
    }
}
