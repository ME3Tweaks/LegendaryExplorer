using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class PrimitiveCast : CastExpression
    {
        public ECast Cast;

        public PrimitiveCast(ECast cast, VariableType castType, Expression expr, SourcePosition start, SourcePosition end) : base(castType, expr, start, end)
        {
            Cast = cast;
        }
    }
}
