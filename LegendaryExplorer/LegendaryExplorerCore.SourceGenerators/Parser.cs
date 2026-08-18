using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LegendaryExplorerCore.SourceGenerators
{
    internal static class Parser
    {
        // UIndexRefVerification.cs is compiled into this project (see the csproj), so everything the generator
        // needs to know about the attributes comes from the attributes themselves.
        public static readonly string UIndexRefAttributeName = typeof(UIndexRefAttribute).FullName;
        public static readonly string UIndexRefContainerAttributeName = typeof(UIndexRefContainerAttribute).FullName;
        public static readonly string ManualAttributeName = typeof(ManualUIndexRefVerificationAttribute).FullName;
        public static readonly string VerifierFqn = "global::" + typeof(IUIndexRefVerifier).FullName;

        /// <summary>Name of the settable counterpart to the attribute's flags parameter.</summary>
        public const string FlagsPropertyName = nameof(UIndexRefAttribute.Flags);

        private const int FlagKey = (int)UIndexRefFlags.Key;
        private const int FlagValue = (int)UIndexRefFlags.Value;

        // These types can't move out of LegendaryExplorerCore, so they stay as names. All three are used in code
        // the generator emits, or guard emission, so a stale one shows up as a failed build rather than silence.
        public const string ObjectBinaryFqn = "global::LegendaryExplorerCore.Unreal.BinaryConverters.ObjectBinary";
        public const string MEGameFqn = "global::LegendaryExplorerCore.Packages.MEGame";
        private const string CollectionsNamespace = "LegendaryExplorerCore.Unreal.Collections";

        /// <summary>
        /// The namespace LEX0001 and LEX0002 look in for fields that still need annotating. Stated here rather
        /// than taken from the attributes' own namespace, which it currently coincides with: the two answer
        /// different questions, and reading it off the attributes would mean that moving UIndexRefVerification.cs
        /// silently switched both diagnostics off instead of failing the build.
        /// </summary>
        public const string ScannedNamespace = "LegendaryExplorerCore.Unreal.BinaryConverters";

        /// <summary>
        /// The UIndexRefFlags members that name a game, paired with the bit that selects them. The member names
        /// are used verbatim as MEGame members in the generated code, so the two enums have to agree on spelling;
        /// if they stop agreeing, the generated switch doesn't compile.
        /// </summary>
        private static readonly (int bit, string gameName)[] GameBits = BuildGameBits();

        private static readonly int AllGameBits = GameBits.Aggregate(0, (mask, game) => mask | game.bit);

        private static (int bit, string gameName)[] BuildGameBits()
        {
            var bits = new List<(int bit, string gameName)>();
            foreach (string flagName in Enum.GetNames(typeof(UIndexRefFlags)))
            {
                if (flagName is nameof(UIndexRefFlags.AllGames) or nameof(UIndexRefFlags.Key) or nameof(UIndexRefFlags.Value))
                {
                    continue;
                }
                bits.Add(((int)(UIndexRefFlags)Enum.Parse(typeof(UIndexRefFlags), flagName), flagName));
            }
            bits.Sort((a, b) => a.bit.CompareTo(b.bit));
            return bits.ToArray();
        }

        private static readonly SymbolDisplayFormat FqnFormat = SymbolDisplayFormat.FullyQualifiedFormat;

        /// <summary>Name plus any containing types, without the namespace.</summary>
        private static readonly SymbolDisplayFormat ShortNameFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

        /// <summary>
        /// How a type is named in the diagnostics that talk about one of its fields. Keeps the containing types,
        /// which a bare Name would drop, so that a nested type like BioTlkFileSet.BioTlkSet is unambiguous.
        /// </summary>
        public static string ShortNameOf(INamedTypeSymbol type) => type.ToDisplayString(ShortNameFormat);

        /// <summary>
        /// Builds the model for a field annotated with [UIndexRef]. Returns null if the field should be ignored.
        /// </summary>
        public static AnnotatedField ParseUIndexRefField(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not IFieldSymbol field || field.ContainingType is not INamedTypeSymbol containingType)
            {
                return null;
            }
            if (HasManualAttribute(containingType))
            {
                return null;
            }

            var diagnostics = new List<DiagnosticInfo>();
            LocationInfo fieldLocation = LocationInfo.From(ctx.TargetNode);
            var visits = new List<FieldVisit>();

            ITypeSymbol fieldType = field.Type;
            string typeDisplay = fieldType.ToDisplayString();

            List<ParsedAttribute> attributes = ParseAttributes(ctx.Attributes);

            if (IsInt(fieldType))
            {
                AddVisit(VisitKind.Single, attributes);
            }
            else if (fieldType is IArrayTypeSymbol { Rank: 1 } arrayType && IsInt(arrayType.ElementType))
            {
                AddVisit(VisitKind.Array, attributes);
            }
            else if (IsListOfInt(fieldType))
            {
                AddVisit(VisitKind.List, attributes);
            }
            else if (TryGetMapTypeArguments(fieldType, out ITypeSymbol keyType, out ITypeSymbol valueType))
            {
                ParseMapField(ctx, field, attributes, keyType, valueType, fieldLocation, visits, diagnostics);
            }
            else
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                    ShortNameOf(containingType), field.Name, typeDisplay,
                    "[UIndexRef] can only be applied to a UIndex, a UIndex array or List, or a map with UIndex keys or values."));
            }

            return BuildModel(containingType, visits, diagnostics);

            void AddVisit(VisitKind kind, List<ParsedAttribute> applicable)
            {
                visits.Add(new FieldVisit(kind, field.Name, BuildClasses(applicable, field, fieldLocation, diagnostics)));
            }
        }

        /// <summary>
        /// Builds the model for a field annotated with [UIndexRefContainer].
        /// </summary>
        public static AnnotatedField ParseContainerField(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not IFieldSymbol field || field.ContainingType is not INamedTypeSymbol containingType)
            {
                return null;
            }
            if (HasManualAttribute(containingType))
            {
                return null;
            }

            var diagnostics = new List<DiagnosticInfo>();
            LocationInfo fieldLocation = LocationInfo.From(ctx.TargetNode);
            var visits = new List<FieldVisit>();

            ITypeSymbol fieldType = field.Type;

            // A map can hold an annotated type on either side. Both are offered here; the emitter keeps only
            // the ones that turned out to have generated methods.
            if (TryGetMapTypeArguments(fieldType, out ITypeSymbol mapKey, out ITypeSymbol mapValue))
            {
                AddMapHalf(VisitKind.ContainerMapKey, mapKey);
                AddMapHalf(VisitKind.ContainerMapValue, mapValue);
                if (visits.Count == 0)
                {
                    diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                        ShortNameOf(containingType), field.Name, fieldType.ToDisplayString(),
                        "Neither the keys nor the values of this map are a class or struct with [UIndexRef] fields."));
                }
                return BuildModel(containingType, visits, diagnostics);

                void AddMapHalf(VisitKind kind, ITypeSymbol half)
                {
                    if (half is INamedTypeSymbol { SpecialType: SpecialType.None, TypeKind: TypeKind.Class or TypeKind.Struct } named)
                    {
                        visits.Add(new FieldVisit(kind, field.Name, EquatableArray<ClassForGames>.Empty,
                            named.IsValueType, named.ToDisplayString(FqnFormat), fieldLocation, isSpeculative: true));
                    }
                }
            }

            VisitKind kind;
            ITypeSymbol containedType;
            if (fieldType is IArrayTypeSymbol { Rank: 1 } arrayType)
            {
                kind = VisitKind.ContainerArray;
                containedType = arrayType.ElementType;
            }
            else if (TryGetListElementType(fieldType, out ITypeSymbol listElement))
            {
                kind = VisitKind.ContainerList;
                containedType = listElement;
            }
            else
            {
                kind = VisitKind.ContainerSingle;
                containedType = fieldType;
            }

            if (containedType is not INamedTypeSymbol namedContained
                || containedType.TypeKind is not (TypeKind.Class or TypeKind.Struct)
                || containedType.SpecialType is not SpecialType.None)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                    ShortNameOf(containingType), field.Name, fieldType.ToDisplayString(),
                    "[UIndexRefContainer] can only be applied to a field whose type, array element type, or List element type is a class or struct with its own [UIndexRef] fields."));
            }
            else
            {
                visits.Add(new FieldVisit(kind, field.Name, EquatableArray<ClassForGames>.Empty,
                    namedContained.IsValueType, namedContained.ToDisplayString(FqnFormat), fieldLocation));
            }

            return BuildModel(containingType, visits, diagnostics);
        }

        private static void ParseMapField(GeneratorAttributeSyntaxContext ctx, IFieldSymbol field,
            List<ParsedAttribute> attributes, ITypeSymbol keyType, ITypeSymbol valueType, LocationInfo fieldLocation,
            List<FieldVisit> visits, List<DiagnosticInfo> diagnostics)
        {
            INamedTypeSymbol containingType = field.ContainingType;
            bool keyIsInt = IsInt(keyType);
            bool valueIsInt = IsInt(valueType);
            if (!keyIsInt && !valueIsInt)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                    ShortNameOf(containingType), field.Name, field.Type.ToDisplayString(),
                    "Neither the keys nor the values of this map are UIndexes."));
                return;
            }

            // Which side is a UIndex? When both sides are ints, only the 'using UIndex = System.Int32;' alias in
            // the source distinguishes a reference from an ordinary number. Attributes that name a side directly
            // are taken at their word.
            bool anyKeyFlag = attributes.Any(a => a.IsKey);
            bool anyValueFlag = attributes.Any(a => a.IsValue);
            // A flag is only taken at its word for a side that is an int at all. Believing it about a side that
            // isn't would emit a Verify call the compiler can't accept, burying the LEX0004 below under it.
            (bool keyIsUIndex, bool valueIsUIndex) = InferMapUIndexSides(ctx, keyIsInt, valueIsInt);
            keyIsUIndex |= anyKeyFlag && keyIsInt;
            valueIsUIndex |= anyValueFlag && valueIsInt;
            if (!keyIsUIndex && !valueIsUIndex)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.UnsupportedFieldShape, fieldLocation,
                    ShortNameOf(containingType), field.Name, field.Type.ToDisplayString()));
                return;
            }

            // An attribute that names neither side applies to whichever sides are UIndexes.
            List<ParsedAttribute> keyAttributes = attributes.Where(a => a.IsKey || (!a.IsValue && keyIsUIndex)).ToList();
            List<ParsedAttribute> valueAttributes = attributes.Where(a => a.IsValue || (!a.IsKey && valueIsUIndex)).ToList();

            if (keyIsUIndex && keyAttributes.Count > 0)
            {
                visits.Add(new FieldVisit(VisitKind.MapKey, field.Name,
                    BuildClasses(keyAttributes, field, fieldLocation, diagnostics)));
            }
            if (valueIsUIndex && valueAttributes.Count > 0)
            {
                visits.Add(new FieldVisit(VisitKind.MapValue, field.Name,
                    BuildClasses(valueAttributes, field, fieldLocation, diagnostics)));
            }

            if (anyKeyFlag && !keyIsInt)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                    ShortNameOf(containingType), field.Name, field.Type.ToDisplayString(),
                    "UIndexRefFlags.Key was specified, but the keys of this map are not UIndexes."));
            }
            if (anyValueFlag && !valueIsInt)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, fieldLocation,
                    ShortNameOf(containingType), field.Name, field.Type.ToDisplayString(),
                    "UIndexRefFlags.Value was specified, but the values of this map are not UIndexes."));
            }
        }

        /// <summary>
        /// Works out which halves of a map field are UIndexes rather than plain ints, by looking for the
        /// UIndex alias in the source. Falls back to the symbols when only one side is an int at all.
        /// </summary>
        private static (bool key, bool value) InferMapUIndexSides(GeneratorAttributeSyntaxContext ctx, bool keyIsInt, bool valueIsInt)
        {
            if (keyIsInt != valueIsInt)
            {
                return (keyIsInt, valueIsInt);
            }
            TypeSyntax typeSyntax = GetFieldTypeSyntax(ctx.TargetNode);
            if (typeSyntax is not null && FindGenericName(typeSyntax) is GenericNameSyntax generic
                && generic.TypeArgumentList.Arguments.Count == 2)
            {
                bool key = IsUIndexAlias(ctx.SemanticModel, generic.TypeArgumentList.Arguments[0]);
                bool value = IsUIndexAlias(ctx.SemanticModel, generic.TypeArgumentList.Arguments[1]);
                if (key || value)
                {
                    return (key, value);
                }
            }
            // Both sides are plain ints with no alias to tell them apart, so we can't guess.
            return (false, false);
        }

        private static bool IsUIndexAlias(SemanticModel semanticModel, TypeSyntax typeSyntax)
        {
            if (typeSyntax is not IdentifierNameSyntax { Identifier.ValueText: "UIndex" })
            {
                return false;
            }
            IAliasSymbol alias = semanticModel.GetAliasInfo(typeSyntax);
            return alias is not null && alias.Target is ITypeSymbol { SpecialType: SpecialType.System_Int32 };
        }

        private static GenericNameSyntax FindGenericName(TypeSyntax typeSyntax) => typeSyntax switch
        {
            GenericNameSyntax generic => generic,
            QualifiedNameSyntax qualified => FindGenericName(qualified.Right),
            AliasQualifiedNameSyntax aliasQualified => FindGenericName(aliasQualified.Name),
            _ => null
        };

        /// <summary>
        /// The written type of an annotated field. ForAttributeWithMetadataName targets the declarator, so that
        /// is the only shape this sees.
        /// </summary>
        public static TypeSyntax GetFieldTypeSyntax(SyntaxNode targetNode) => targetNode switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => declaration.Type,
            _ => null
        };

        /// <summary>One [UIndexRef] as written in the source.</summary>
        private readonly struct ParsedAttribute
        {
            public readonly string AcceptedClass;
            public readonly int Flags;

            public ParsedAttribute(string acceptedClass, int flags)
            {
                AcceptedClass = acceptedClass;
                Flags = flags;
            }

            public bool IsKey => (Flags & FlagKey) != 0;
            public bool IsValue => (Flags & FlagValue) != 0;
            public int GameMask => Flags & AllGameBits;
        }

        private static List<ParsedAttribute> ParseAttributes(ImmutableArray<AttributeData> attributes)
        {
            var parsed = new List<ParsedAttribute>(attributes.Length);
            foreach (AttributeData attr in attributes)
            {
                string acceptedClass = null;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s && s.Length > 0)
                {
                    acceptedClass = s;
                }
                int flags = 0;
                if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is int ctorFlags)
                {
                    flags = ctorFlags;
                }
                // The Flags property is also settable, and runs after the constructor, so it wins.
                foreach (KeyValuePair<string, TypedConstant> named in attr.NamedArguments)
                {
                    if (named.Key == FlagsPropertyName && named.Value.Value is int namedFlags)
                    {
                        flags = namedFlags;
                    }
                }
                parsed.Add(new ParsedAttribute(acceptedClass, flags));
            }
            return parsed;
        }

        /// <summary>
        /// Turns the attributes that apply to one reference into the per-game class table the emitter needs.
        /// An attribute with no game flags covers every game the others don't claim.
        /// </summary>
        private static EquatableArray<ClassForGames> BuildClasses(List<ParsedAttribute> attributes,
            IFieldSymbol field, LocationInfo location, List<DiagnosticInfo> diagnostics)
        {
            var entries = new List<(int sortKey, ClassForGames entry)>();
            var claimedGames = new HashSet<string>(StringComparer.Ordinal);
            bool sawFallback = false;
            bool conflict = false;

            foreach (ParsedAttribute attr in attributes)
            {
                int gameMask = attr.GameMask;
                if (gameMask == 0)
                {
                    if (sawFallback)
                    {
                        conflict = true;
                        continue;
                    }
                    sawFallback = true;
                    entries.Add((int.MaxValue, new ClassForGames(attr.AcceptedClass, EquatableArray<string>.Empty)));
                    continue;
                }

                var games = new List<string>();
                int sortKey = int.MaxValue;
                foreach ((int bit, string gameName) in GameBits)
                {
                    if ((gameMask & bit) == 0)
                    {
                        continue;
                    }
                    if (!claimedGames.Add(gameName))
                    {
                        conflict = true;
                        continue;
                    }
                    games.Add(gameName);
                    sortKey = Math.Min(sortKey, bit);
                }
                if (games.Count > 0)
                {
                    entries.Add((sortKey, new ClassForGames(attr.AcceptedClass, new EquatableArray<string>(games))));
                }
            }

            if (conflict)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.ConflictingAttributes, location,
                    ShortNameOf(field.ContainingType), field.Name));
            }

            entries.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
            return new EquatableArray<ClassForGames>(entries.Select(e => e.entry));
        }

        private static AnnotatedField BuildModel(INamedTypeSymbol containingType, List<FieldVisit> visits, List<DiagnosticInfo> diagnostics)
        {
            TypeShell shell = BuildTypeShell(containingType);
            if (visits.Count > 0 && !shell.IsPartial)
            {
                diagnostics.Add(new DiagnosticInfo(Diagnostics.TypeMustBePartial, shell.Location, shell.DisplayName));
                visits.Clear();
            }
            return new AnnotatedField(shell, DerivesFromObjectBinary(containingType), BuildBaseChain(containingType),
                new EquatableArray<FieldVisit>(visits), new EquatableArray<DiagnosticInfo>(diagnostics));
        }

        public static TypeShell BuildTypeShell(INamedTypeSymbol type)
        {
            var containment = new List<INamedTypeSymbol>();
            for (INamedTypeSymbol t = type; t is not null; t = t.ContainingType)
            {
                containment.Add(t);
            }
            containment.Reverse();

            bool isPartial = true;
            var headers = new List<string>(containment.Count);
            foreach (INamedTypeSymbol t in containment)
            {
                isPartial &= IsDeclaredPartial(t);
                headers.Add($"partial {KeywordFor(t)} {NameWithTypeParameters(t)}");
            }

            string ns = type.ContainingNamespace is { IsGlobalNamespace: false } n
                ? n.ToDisplayString()
                : string.Empty;

            Location declarationLocation = null;
            if (!type.DeclaringSyntaxReferences.IsDefaultOrEmpty
                && type.DeclaringSyntaxReferences[0].GetSyntax() is TypeDeclarationSyntax declaration)
            {
                declarationLocation = declaration.Identifier.GetLocation();
            }

            return new TypeShell(
                type.ToDisplayString(FqnFormat),
                ns,
                new EquatableArray<string>(headers),
                isPartial,
                type.IsValueType,
                type.IsSealed,
                DerivesFromObjectBinary(type),
                type.ToDisplayString(),
                ShortNameOf(type),
                LocationInfo.From(declarationLocation));
        }

        /// <summary>
        /// The chain of source-declared base types, nearest first, stopping at ObjectBinary (which declares the
        /// root method itself) and at anything that can't be extended with a partial declaration.
        /// </summary>
        public static EquatableArray<TypeShell> BuildBaseChain(INamedTypeSymbol type)
        {
            var chain = new List<TypeShell>();
            for (INamedTypeSymbol baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                if (baseType.SpecialType is not SpecialType.None
                    || baseType.ToDisplayString(FqnFormat) == ObjectBinaryFqn
                    || baseType.DeclaringSyntaxReferences.IsDefaultOrEmpty)
                {
                    break;
                }
                chain.Add(BuildTypeShell(baseType));
            }
            return new EquatableArray<TypeShell>(chain);
        }

        public static bool DerivesFromObjectBinary(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol t = type.BaseType; t is not null; t = t.BaseType)
            {
                if (t.ToDisplayString(FqnFormat) == ObjectBinaryFqn)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasManualAttribute(INamedTypeSymbol type)
        {
            foreach (AttributeData attr in type.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == ManualAttributeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDeclaredPartial(INamedTypeSymbol type)
        {
            if (type.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            {
                return false;
            }
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not TypeDeclarationSyntax declaration
                    || !declaration.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                {
                    return false;
                }
            }
            return true;
        }

        private static string KeywordFor(INamedTypeSymbol type) => type switch
        {
            { TypeKind: TypeKind.Struct, IsRecord: true } => "record struct",
            { TypeKind: TypeKind.Struct } => "struct",
            { IsRecord: true } => "record",
            { TypeKind: TypeKind.Interface } => "interface",
            _ => "class"
        };

        private static string NameWithTypeParameters(INamedTypeSymbol type)
        {
            if (type.TypeParameters.IsDefaultOrEmpty)
            {
                return type.Name;
            }
            var sb = new StringBuilder(type.Name).Append('<');
            for (int i = 0; i < type.TypeParameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(type.TypeParameters[i].Name);
            }
            return sb.Append('>').ToString();
        }

        /// <summary>
        /// The types a field could be recursed into: itself, and for arrays and Lists, the element type.
        /// </summary>
        public static IEnumerable<ITypeSymbol> RecursableTypesOf(ITypeSymbol fieldType)
        {
            if (fieldType is IArrayTypeSymbol { Rank: 1 } array)
            {
                yield return array.ElementType;
                yield break;
            }
            if (TryGetListElementType(fieldType, out ITypeSymbol element))
            {
                yield return element;
                yield break;
            }
            if (TryGetMapTypeArguments(fieldType, out ITypeSymbol keyType, out ITypeSymbol valueType))
            {
                yield return keyType;
                yield return valueType;
                yield break;
            }
            yield return fieldType;
        }

        public static bool IsInt(ITypeSymbol type) => type?.SpecialType == SpecialType.System_Int32;

        private static bool IsListOfInt(ITypeSymbol type) =>
            TryGetListElementType(type, out ITypeSymbol element) && IsInt(element);

        private static bool TryGetListElementType(ITypeSymbol type, out ITypeSymbol elementType)
        {
            elementType = null;
            if (type is not INamedTypeSymbol { IsGenericType: true } named
                || named.ConstructedFrom.ToDisplayString(FqnFormat) != "global::System.Collections.Generic.List<T>")
            {
                return false;
            }
            elementType = named.TypeArguments[0];
            return true;
        }

        /// <summary>
        /// True for UMap/UMultiMap (anything deriving from UMapBase), yielding the key and value types.
        /// </summary>
        private static bool TryGetMapTypeArguments(ITypeSymbol type, out ITypeSymbol keyType, out ITypeSymbol valueType)
        {
            keyType = null;
            valueType = null;
            if (type is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } named)
            {
                return false;
            }
            bool isMap = false;
            for (INamedTypeSymbol t = named; t is not null; t = t.BaseType)
            {
                if (t.Name == "UMapBase" && t.ContainingNamespace?.ToDisplayString() == CollectionsNamespace)
                {
                    isMap = true;
                    break;
                }
            }
            if (!isMap)
            {
                return false;
            }
            keyType = named.TypeArguments[0];
            valueType = named.TypeArguments[1];
            return true;
        }
    }
}
