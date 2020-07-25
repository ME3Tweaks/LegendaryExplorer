using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class ConfigSpecifier : Specifier
    {
        public string Category;

        public ConfigSpecifier(string category, SourcePosition start = null, SourcePosition end = null) : base("config", start, end)
        {
            Category = category;
        }
    }
}
