using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Class : ASTNode
    {
        public String Name;
        public Class Parent;
        public Class OuterClass;
        public List<Specifier> Specifiers;
        public List<VariableDeclaration> Variables;
        public List<Function> Functions;
        public List<State> States;


    }
}
