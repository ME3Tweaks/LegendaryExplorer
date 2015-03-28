using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Specifier : ASTNode
    {
        public String Value;
        public Specifier(String value) : base(ASTNodeType.Specifier) 
        {
            Value = value;
        }
    }
}
