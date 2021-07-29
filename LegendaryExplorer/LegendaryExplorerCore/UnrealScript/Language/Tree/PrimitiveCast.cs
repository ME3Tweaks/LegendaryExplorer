using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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
