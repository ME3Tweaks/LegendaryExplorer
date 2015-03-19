using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class BinaryOperation : Expression
    {
        public BinaryOperation() : base(ASTNodeType.BinaryOperation) { }
    }
}
