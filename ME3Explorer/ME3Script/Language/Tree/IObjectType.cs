using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public interface IObjectType
    {
        public List<VariableDeclaration> VariableDeclarations { get; }
        public List<VariableType> TypeDeclarations { get; }
        public DefaultPropertiesBlock DefaultProperties { get; }

    }
}
