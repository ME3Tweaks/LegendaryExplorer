using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Types
{
    public class UserDefinedType : AbstractType
    {
        public AbstractSyntaxTree Declaration { get; private set; }

        public UserDefinedType(String name, AbstractSyntaxTree decl) : base(name)
        {
            Declaration = decl;
        }
    }
}
