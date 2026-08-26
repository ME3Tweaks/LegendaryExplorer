using System;
using System.Collections.Generic;
using System.Threading;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LegendaryExplorerCore.SourceGenerators
{
    /// <summary>
    /// Finds UIndex fields that nobody has said what class they refer to yet. Attributes can only be found by
    /// their presence, so tracking down the fields that are missing one needs a separate pass over the syntax.
    /// </summary>
    internal static class CoverageScanner
    {
        /// <summary>The attributes' own names, minus the Attribute suffix that can be left off at a use site.</summary>
        private static readonly string[] AttributeNames =
        {
            nameof(UIndexRefAttribute),
            TrimAttributeSuffix(nameof(UIndexRefAttribute)),
            nameof(UIndexRefContainerAttribute),
            TrimAttributeSuffix(nameof(UIndexRefContainerAttribute))
        };

        private static string TrimAttributeSuffix(string name) =>
            name.EndsWith(nameof(Attribute), StringComparison.Ordinal)
                ? name.Substring(0, name.Length - nameof(Attribute).Length)
                : name;

        /// <summary>
        /// Cheap syntax-only filter: a field in the scanned namespace whose type mentions UIndex.
        /// </summary>
        public static bool IsCandidateField(SyntaxNode node, CancellationToken cancellationToken)
        {
            if (node is not FieldDeclarationSyntax field)
            {
                return false;
            }
            if (!field.Declaration.Type.ToString().Contains("UIndex"))
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
            return IsInScannedNamespace(field);
        }

        /// <summary>
        /// One diagnostic per declared variable: 'public UIndex A, B;' is two fields that each need annotating.
        /// </summary>
        public static EquatableArray<DiagnosticInfo> GetUncoveredFields(GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
        {
            var field = (FieldDeclarationSyntax)ctx.Node;
            if (HasUIndexRefAttribute(field))
            {
                return EquatableArray<DiagnosticInfo>.Empty;
            }
            if (!MentionsUIndexAlias(ctx.SemanticModel, field.Declaration.Type))
            {
                return EquatableArray<DiagnosticInfo>.Empty;
            }

            List<DiagnosticInfo> diagnostics = null;
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                if (ctx.SemanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol symbol
                    || symbol.ContainingType is null
                    || Parser.HasManualAttribute(symbol.ContainingType))
                {
                    continue;
                }
                (diagnostics ??= []).Add(new DiagnosticInfo(Diagnostics.UnannotatedUIndexField,
                    LocationInfo.From(variable), Parser.ShortNameOf(symbol.ContainingType), symbol.Name));
            }
            return diagnostics is null
                ? EquatableArray<DiagnosticInfo>.Empty
                : new EquatableArray<DiagnosticInfo>(diagnostics);
        }

        /// <summary>
        /// Whether the node sits in <see cref="Parser.ScannedNamespace"/>. Block namespaces can nest, so the
        /// declared names are gathered on the way up and joined before comparing; matching one declaration on
        /// its own would both miss a nested spelling and accept a namespace that merely ends the same way.
        /// </summary>
        public static bool IsInScannedNamespace(SyntaxNode node)
        {
            List<string> parts = null;
            for (SyntaxNode current = node.Parent; current is not null; current = current.Parent)
            {
                string name = current switch
                {
                    FileScopedNamespaceDeclarationSyntax fileScoped => fileScoped.Name.ToString(),
                    NamespaceDeclarationSyntax block => block.Name.ToString(),
                    _ => null
                };
                if (name is not null)
                {
                    (parts ??= []).Add(name);
                }
            }
            if (parts is null)
            {
                return false;
            }
            if (parts.Count == 1)
            {
                return parts[0] == Parser.ScannedNamespace;
            }
            parts.Reverse();
            return string.Join(".", parts) == Parser.ScannedNamespace;
        }

        public static bool HasUIndexRefAttribute(FieldDeclarationSyntax field)
        {
            foreach (AttributeListSyntax list in field.AttributeLists)
            {
                foreach (AttributeSyntax attribute in list.Attributes)
                {
                    string name = attribute.Name switch
                    {
                        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                        SimpleNameSyntax simple => simple.Identifier.ValueText,
                        _ => attribute.Name.ToString()
                    };
                    if (Array.IndexOf(AttributeNames, name) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Confirms that a 'UIndex' in the field's type really is the 'using UIndex = System.Int32;' alias and not
        /// some unrelated identifier that happens to contain the word.
        /// </summary>
        private static bool MentionsUIndexAlias(SemanticModel semanticModel, TypeSyntax typeSyntax)
        {
            foreach (SyntaxNode node in typeSyntax.DescendantNodesAndSelf())
            {
                if (node is IdentifierNameSyntax { Identifier.ValueText: "UIndex" } identifier
                    && semanticModel.GetAliasInfo(identifier) is { Target: ITypeSymbol { SpecialType: SpecialType.System_Int32 } })
                {
                    return true;
                }
            }
            return false;
        }
    }
}
