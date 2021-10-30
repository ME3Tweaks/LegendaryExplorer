using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public static class GlobalLists
    {
        public static readonly List<SymbolMatcher> DelimitersAndOperators;
        public static List<TokenType> ValidOperatorSymbols;
        private static readonly bool[] DelimiterChars;
        public static bool IsDelimiterChar(char c) => c < 128 && DelimiterChars[c];

        static GlobalLists()
        {
            DelimitersAndOperators = new List<SymbolMatcher>
            {
                new SymbolMatcher("{", TokenType.LeftBracket),
                new SymbolMatcher("}", TokenType.RightBracket),
                new SymbolMatcher("[", TokenType.LeftSqrBracket),
                new SymbolMatcher("]", TokenType.RightSqrBracket),
                new SymbolMatcher("(", TokenType.LeftParenth),
                new SymbolMatcher(")", TokenType.RightParenth),
                new SymbolMatcher("==", TokenType.Equals),
                new SymbolMatcher("+=", TokenType.AddAssign),   
                new SymbolMatcher("-=", TokenType.SubAssign),   
                new SymbolMatcher("*=", TokenType.MulAssign),   
                new SymbolMatcher("/=", TokenType.DivAssign),      
                new SymbolMatcher("!=", TokenType.NotEquals),  
                new SymbolMatcher("~=", TokenType.ApproxEquals), 
                new SymbolMatcher(">>>", TokenType.VectorTransform),
                //new SymbolMatcher(">>", TokenType.RightShift),    //will have to be matched manually in the parser. conflicts with arrays of delegates: array<delegate<somefunc>>
                new SymbolMatcher("<<", TokenType.LeftShift),
                new SymbolMatcher("<=", TokenType.LessOrEquals),
                new SymbolMatcher(">=", TokenType.GreaterOrEquals),
                new SymbolMatcher("**", TokenType.Power), 
                new SymbolMatcher("&&", TokenType.And),   
                new SymbolMatcher("||", TokenType.Or),         
                new SymbolMatcher("^^", TokenType.Xor),
                new SymbolMatcher("<", TokenType.LeftArrow),    
                new SymbolMatcher(">", TokenType.RightArrow),         
                new SymbolMatcher("%", TokenType.Modulo),
                new SymbolMatcher("$=", TokenType.StrConcatAssign),
                new SymbolMatcher("$", TokenType.DollarSign),
                new SymbolMatcher("@=", TokenType.StrConcAssSpace),
                new SymbolMatcher("@", TokenType.AtSign),
                new SymbolMatcher("--", TokenType.Decrement),
                new SymbolMatcher("++", TokenType.Increment),
                new SymbolMatcher("-", TokenType.MinusSign),      
                new SymbolMatcher("+", TokenType.PlusSign),        
                new SymbolMatcher("*", TokenType.StarSign),   
                new SymbolMatcher("/", TokenType.Slash),
                new SymbolMatcher("=", TokenType.Assign),  
                new SymbolMatcher("~", TokenType.Complement), 
                new SymbolMatcher("&", TokenType.BinaryAnd),    
                new SymbolMatcher("|", TokenType.BinaryOr),     
                new SymbolMatcher("^", TokenType.BinaryXor),     
                new SymbolMatcher("?", TokenType.QuestionMark),   
                new SymbolMatcher(":", TokenType.Colon),
                new SymbolMatcher(";", TokenType.SemiColon),
                new SymbolMatcher(",", TokenType.Comma),
                new SymbolMatcher(".", TokenType.Dot),
                new SymbolMatcher("!", TokenType.ExclamationMark),
                new SymbolMatcher("#", TokenType.Hash)
            };

            ValidOperatorSymbols = new List<TokenType>
            {
                TokenType.Equals,    
                TokenType.AddAssign,   
                TokenType.SubAssign,   
                TokenType.MulAssign,   
                TokenType.DivAssign,      
                TokenType.NotEquals,  
                TokenType.ApproxEquals, 
                //TokenType.RightShift,    
                TokenType.LeftShift,
                TokenType.LessOrEquals,
                TokenType.GreaterOrEquals,
                TokenType.Power, 
                TokenType.And,   
                TokenType.Or,         
                TokenType.Xor,
                TokenType.LeftArrow,    
                TokenType.RightArrow,         
                TokenType.Modulo,
                TokenType.StrConcatAssign,
                TokenType.DollarSign,
                TokenType.StrConcAssSpace,
                TokenType.AtSign,
                TokenType.MinusSign,      
                TokenType.PlusSign,        
                TokenType.StarSign,   
                TokenType.Slash,
                TokenType.Complement, 
                TokenType.BinaryAnd,    
                TokenType.BinaryOr,     
                TokenType.BinaryXor,     
                TokenType.QuestionMark,   
                //TokenType.Colon,
                //TokenType.SemiColon,
                //TokenType.Comma,
                //TokenType.Dot,
                TokenType.ExclamationMark,
                TokenType.Hash,
                TokenType.VectorTransform
            };

            //will break if DelimitersAndOperators contains non-ascii chars. DelimitersAndOperators should never be changed though, so... shouldn't be a problem
            DelimiterChars = new bool[128];
            foreach (SymbolMatcher symbolMatcher in DelimitersAndOperators)
            {
                DelimiterChars[symbolMatcher.Keyword[0]] = true;
            }
        }

        public static List<ASTNodeType> SemicolonExceptions = new()
        {   // TODO: check this for other types
            ASTNodeType.ForLoop,
            ASTNodeType.WhileLoop,
            ASTNodeType.IfStatement,
            ASTNodeType.VariableDeclaration,
            ASTNodeType.SwitchStatement,
            ASTNodeType.CaseStatement,
            ASTNodeType.DefaultStatement,
            ASTNodeType.StateLabel,
            ASTNodeType.ForEachLoop
        };

        #region Specifier Categories
        //public static List<TokenType> VariableSpecifiers = new List<TokenType>
        //{
        //    TokenType.ConfigSpecifier,
        //    TokenType.GlobalConfigSpecifier,
        //    TokenType.LocalizedSpecifier,
        //    TokenType.Constant,
        //    TokenType.PrivateSpecifier,
        //    TokenType.ProtectedSpecifier,
        //    TokenType.PrivateWriteSpecifier,
        //    TokenType.ProtectedWriteSpecifier,
        //    TokenType.RepNotifySpecifier,
        //    TokenType.DeprecatedSpecifier,
        //    TokenType.InstancedSpecifier,
        //    TokenType.DatabindingSpecifier,
        //    TokenType.EditorOnlySpecifier,
        //    TokenType.NotForConsoleSpecifier,
        //    TokenType.EditConstSpecifier,
        //    TokenType.EditFixedSizeSpecifier,
        //    TokenType.EditInlineSpecifier,
        //    TokenType.EditInlineUseSpecifier,
        //    TokenType.NoClearSpecifier,
        //    TokenType.InterpSpecifier,
        //    TokenType.InputSpecifier,
        //    TokenType.TransientSpecifier,
        //    TokenType.DuplicateTransientSpecifier,
        //    TokenType.NoImportSpecifier,
        //    TokenType.NativeSpecifier,
        //    TokenType.ExportSpecifier,
        //    TokenType.NoExportSpecifier,
        //    TokenType.NonTransactionalSpecifier,
        //    TokenType.PointerSpecifier,
        //    TokenType.RepRetrySpecifier,
        //    TokenType.AllowAbstractSpecifier
        //};

        //public static List<TokenType> ClassSpecifiers = new List<TokenType>
        //{
        //    TokenType.AbstractSpecifier,
        //    TokenType.ConfigSpecifier,
        //    TokenType.DependsOnSpecifier,
        //    TokenType.ImplementsSpecifier,
        //    TokenType.InstancedSpecifier,
        //    TokenType.ParseConfigSpecifier,
        //    TokenType.PerObjectConfigSpecifier,
        //    TokenType.PerObjectLocalizedSpecifier,
        //    TokenType.TransientSpecifier,
        //    TokenType.NonTransientSpecifier,
        //    TokenType.DeprecatedSpecifier,
        //    TokenType.PlaceableSpecifier,
        //    TokenType.NativeReplicationSpecifier
        //};

        //public static List<TokenType> StructSpecifiers = new List<TokenType>
        //{
        //    TokenType.ImmutableSpecifier,
        //    TokenType.ImmutableWhenCookedSpecifier,
        //    TokenType.AtomicSpecifier,
        //    TokenType.AtomicWhenCookedSpecifier,
        //    TokenType.StrictConfigSpecifier,
        //    TokenType.TransientSpecifier,
        //    TokenType.NativeSpecifier
        //};

        //public static List<TokenType> FunctionSpecifiers = new List<TokenType>
        //{
        //    TokenType.PrivateSpecifier,
        //    TokenType.ProtectedSpecifier,
        //    TokenType.PublicSpecifier,
        //    TokenType.StaticSpecifier,
        //    TokenType.FinalSpecifier,
        //    TokenType.ExecSpecifier,
        //    TokenType.K2CallSpecifier,
        //    TokenType.K2OverrideSpecifier,
        //    TokenType.K2PureSpecifier,
        //    TokenType.SimulatedSpecifier,
        //    TokenType.SingularSpecifier,
        //    TokenType.ClientSpecifier,
        //    TokenType.DemoRecordingSpecifier,
        //    TokenType.ReliableSpecifier,
        //    TokenType.ServerSpecifier,
        //    TokenType.UnreliableSpecifier,
        //    TokenType.Constant,
        //    TokenType.IteratorSpecifier,
        //    TokenType.LatentSpecifier,
        //    TokenType.NativeSpecifier,
        //    TokenType.NoExportSpecifier
        //};

        //public static List<TokenType> ParameterSpecifiers = new List<TokenType>
        //{
        //    TokenType.CoerceSpecifier,
        //    TokenType.Constant,
        //    TokenType.OptionalSpecifier,
        //    TokenType.OutSpecifier,
        //    TokenType.SkipSpecifier
        //};

        //public static List<TokenType> StateSpecifiers = new List<TokenType>
        //{
        //    TokenType.AutoSpecifier,
        //    TokenType.SimulatedSpecifier
        //};

        #endregion

    }
}
