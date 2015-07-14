using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {
        public Expression DecompileDynArrLength()
        {
            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;

            StartPositions.Pop();
            // TODO: ugly solution, should be reworked once dynarrays are in the AST.
            return new CompositeSymbolRef(arr, new SymbolReference(null, null, null, "Length"), null, null);
        }
    }
}
