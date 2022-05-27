using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public static class SingleLineCommentMatcher
    {
        public static ScriptToken MatchComment(CharDataStream data, ref SourcePosition streamPos)
        {
            if (data.CurrentItem == '/')
            {
                data.Advance();
                if (data.CurrentItem == '/')
                {
                    string comment = "";
                    data.Advance();
                    while (!data.AtEnd())
                    {
                        if (data.CurrentItem == '\n')
                        {
                            break;
                        }

                        comment += data.CurrentItem;
                        data.Advance();
                    }

                    var start = new SourcePosition(streamPos);
                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                    var end = new SourcePosition(streamPos);
                    return new ScriptToken(TokenType.SingleLineComment, comment, start, end) { SyntaxType = EF.Comment };
                }
            }

            return null;
        }
    }
}
