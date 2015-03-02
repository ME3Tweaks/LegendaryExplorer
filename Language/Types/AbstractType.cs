using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Types
{
    public abstract class AbstractType
    {
        public String Name { get; private set; }

        public AbstractType(String name)
        {
            Name = name;
        }
    }
}
