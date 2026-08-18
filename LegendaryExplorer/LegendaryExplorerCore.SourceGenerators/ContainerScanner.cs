using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LegendaryExplorerCore.SourceGenerators
{
    /// <summary>
    /// Finds fields whose type has [UIndexRef] fields of its own but which aren't marked
    /// [UIndexRefContainer], so nothing ever recurses into them. Without this the annotations on a nested type
    /// look complete while never actually running.
    /// </summary>
    internal static class ContainerScanner
    {
        /// <summary>
        /// Cheap syntax-only filter: an unannotated instance field in the scanned namespace that
        /// could be holding something with fields of its own. Every field in the namespace reaches here, and
        /// everything that gets through costs a semantic lookup in <see cref="GetCandidates"/>, so the two
        /// syntax-only rejections below are worth making before that.
        /// </summary>
        public static bool IsCandidateField(SyntaxNode node, CancellationToken cancellationToken)
        {
            if (node is not FieldDeclarationSyntax field)
            {
                return false;
            }
            foreach (SyntaxToken modifier in field.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.ConstKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword))
                {
                    return false;
                }
            }
            if (CoverageScanner.HasUIndexRefAttribute(field) || HoldsOnlyPredefinedTypes(field.Declaration.Type))
            {
                return false;
            }
            return CoverageScanner.IsInScannedNamespace(field);
        }

        /// <summary>
        /// True when everything the field could hold is a predefined type like int or float, which can't have
        /// [UIndexRef] fields. Deliberately conservative: anything it isn't sure of is passed along for
        /// <see cref="GetCandidates"/> to decide semantically.
        /// </summary>
        /// <remarks>
        /// The generic names here are the collections <see cref="Parser.RecursableTypesOf"/> looks inside;
        /// if it learns about another one, this needs to as well, or fields of that type stop being checked.
        /// </remarks>
        private static bool HoldsOnlyPredefinedTypes(TypeSyntax type)
        {
            switch (type)
            {
                case PredefinedTypeSyntax:
                    return true;
                case NullableTypeSyntax nullable:
                    return HoldsOnlyPredefinedTypes(nullable.ElementType);
                case ArrayTypeSyntax array:
                    return HoldsOnlyPredefinedTypes(array.ElementType);
                case GenericNameSyntax { Identifier.ValueText: "List" or "UMap" or "UMultiMap" } generic:
                    foreach (TypeSyntax argument in generic.TypeArgumentList.Arguments)
                    {
                        if (!HoldsOnlyPredefinedTypes(argument))
                        {
                            return false;
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// One candidate per declared variable: 'public Foo A, B;' is two fields that each need the attribute.
        /// </summary>
        public static EquatableArray<ContainerCandidate> GetCandidates(GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
        {
            var field = (FieldDeclarationSyntax)ctx.Node;

            List<ContainerCandidate> candidates = null;
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                if (ctx.SemanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol symbol
                    || symbol.ContainingType is null
                    || Parser.HasManualAttribute(symbol.ContainingType))
                {
                    continue;
                }

                // The field itself, or what it holds: T, T[] and List<T> can all be recursed into.
                var candidateTypes = new List<string>();
                foreach (ITypeSymbol type in Parser.RecursableTypesOf(symbol.Type))
                {
                    if (type is INamedTypeSymbol { SpecialType: SpecialType.None, TypeKind: TypeKind.Class or TypeKind.Struct } named)
                    {
                        candidateTypes.Add(named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }
                if (candidateTypes.Count == 0)
                {
                    continue;
                }

                (candidates ??= []).Add(new ContainerCandidate(
                    Parser.ShortNameOf(symbol.ContainingType),
                    symbol.Name,
                    new EquatableArray<string>(candidateTypes),
                    LocationInfo.From(variable)));
            }
            return candidates is null
                ? EquatableArray<ContainerCandidate>.Empty
                : new EquatableArray<ContainerCandidate>(candidates);
        }
    }
}
