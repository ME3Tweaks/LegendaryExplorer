using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public sealed class NameLiteralMatcher : TokenMatcherBase
    {
        private const char DELIMITER = '\'';
        public override ScriptToken Match(CharDataStream data, ref SourcePosition streamPos, MessageLog log)
        {
            return MatchName(data, ref streamPos, log);
        }

        public static ScriptToken MatchName(CharDataStream data, ref SourcePosition streamPos, MessageLog log)
        {
            string value = null;
            if (data.CurrentItem == DELIMITER)
            {
                data.Advance();
                bool inEscape = false;
                for (; !data.AtEnd(); data.Advance())
                {
                    if (inEscape)
                    {
                        inEscape = false;
                        switch (data.CurrentItem)
                        {
                            case '\\':
                            case DELIMITER:
                                value += data.CurrentItem;
                                continue;
                            default:
                                log.LogError(@$"Unrecognized escape sequence: '\{data.CurrentItem}'", new SourcePosition(streamPos));
                                return null;
                        }
                    }

                    if (data.CurrentItem == '\\')
                    {
                        inEscape = true;
                        continue;
                    }
                    if (data.CurrentItem == DELIMITER)
                    {
                        break;
                    }
                    if (data.CurrentItem == '\n')
                    {
                        streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - streamPos.CharIndex, data.CurrentIndex - streamPos.CharIndex);
                        log.LogError("Name Literals can not contain line breaks!", new SourcePosition(streamPos), new SourcePosition(streamPos));
                        return null;
                    }
                    value += data.CurrentItem;
                }

                if (data.CurrentItem == DELIMITER)
                {
                    data.Advance();
                    value ??= "None"; //empty name literals should be interpreted as 'None'
                }
                else
                {
                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - streamPos.CharIndex, data.CurrentIndex - streamPos.CharIndex);
                    log.LogError("Name Literal was not terminated properly!", new SourcePosition(streamPos), new SourcePosition(streamPos));
                    return null;
                }
            }

            if (value != null)
            {
                var start = new SourcePosition(streamPos);
                streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                var end = new SourcePosition(streamPos);
                return new ScriptToken(TokenType.NameLiteral, value, start, end) { SyntaxType = EF.Name };
            }
            return null;
        }
    }
}
