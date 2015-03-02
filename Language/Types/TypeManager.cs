using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Types
{
    public class TypeManager
    {
        private static List<AbstractType> GlobalNamespace = null;
        private List<AbstractType> CurrentNamespace;

        public TypeManager()
        {
            CurrentNamespace = new List<AbstractType>();
            CurrentNamespace.AddRange(GlobalNamespace);
        }

        public static void InitializeGlobalNamespace(List<AbstractType> symbols)
        {
            if (GlobalNamespace == null)
            {
                GlobalNamespace = new List<AbstractType>();
                GlobalNamespace.AddRange(symbols);
            }
        }

        public bool TryRegisterType(String name, AbstractSyntaxTree tree)
        {
            if (SymbolExists(name))
                return false;

            AbstractType symbol = new UserDefinedType(name, tree);
            CurrentNamespace.Add(symbol);
            return true;
        }

        public bool SymbolExists(String name)
        {
            return CurrentNamespace.Find(s => s.Name.ToLower() == name.ToLower()) != null ? true : false;
        }
    }
}
