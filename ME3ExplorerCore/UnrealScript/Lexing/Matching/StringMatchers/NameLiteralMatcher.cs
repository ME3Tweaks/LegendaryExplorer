using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Utilities;
using Unrealscript.Compiling.Errors;

namespace Unrealscript.Lexing.Matching.StringMatchers
{
    public class NameLiteralMatcher : TokenMatcherBase<string>
    {
        private const string Delimiter = "'";
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);
            string value = null;
            if (data.CurrentItem == Delimiter)
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
                            case "\\":
                            case Delimiter:
                                value += data.CurrentItem;
                                continue;
                            default:
                                log.LogError(@$"Unrecognized escape sequence: '\{data.CurrentItem}'", new SourcePosition(streamPos));
                                return null;
                        }
                    }

                    if (data.CurrentItem == "\\")
                    {
                        inEscape = true;
                        continue;
                    }
                    if (data.CurrentItem == Delimiter)
                    {
                        break;
                    }
                    if (data.CurrentItem == "\n")
                    {
                        streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                        log.LogError("Name Literals can not contain line breaks!", start, new SourcePosition(streamPos));
                        return null;
                    }
                    value += data.CurrentItem;
                }

                if (data.CurrentItem == Delimiter)
                {
                    data.Advance();
                    value ??= "None"; //empty name literals should be interpreted as 'None'
                }
                else
                {
                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                    log.LogError("Name Literal was not terminated properly!", start, new SourcePosition(streamPos));
                    return null;
                }
            }

            if (value != null)
            {
                streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                SourcePosition end = new SourcePosition(streamPos);
                return new Token<string>(TokenType.NameLiteral, value, start, end) {SyntaxType = EF.Name};
            }
            return null;
        }
    }
}
