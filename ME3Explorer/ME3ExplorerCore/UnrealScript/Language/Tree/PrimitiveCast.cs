using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
