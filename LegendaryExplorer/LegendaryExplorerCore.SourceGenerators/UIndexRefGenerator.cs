using Microsoft.CodeAnalysis;

namespace LegendaryExplorerCore.SourceGenerators
{
    /// <summary>
    /// Emits VerifyUIndexRefs methods for the types in LegendaryExplorerCore whose UIndex fields have been
    /// annotated with [UIndexRef], and warns about the ones that haven't been annotated yet.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class UIndexRefGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var refFields = context.SyntaxProvider.ForAttributeWithMetadataName(
                Parser.UIndexRefAttributeName,
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax,
                transform: static (ctx, _) => Parser.ParseUIndexRefField(ctx));

            var containerFields = context.SyntaxProvider.ForAttributeWithMetadataName(
                Parser.UIndexRefContainerAttributeName,
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax,
                transform: static (ctx, _) => Parser.ParseContainerField(ctx));

            // Emission needs every annotated type at once: whether a generated method overrides another one
            // depends on which of a type's base types also ended up with annotations.
            var allFields = refFields.Collect().Combine(containerFields.Collect());
            context.RegisterSourceOutput(allFields, static (spc, pair) => Emitter.Emit(spc, pair.Left.AddRange(pair.Right)));

            // A type can be fully annotated and still never run, if no field marks itself as recursing into it.
            var containerCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, ct) => ContainerScanner.IsCandidateField(node, ct),
                    transform: static (ctx, ct) => ContainerScanner.GetCandidates(ctx, ct))
                .SelectMany(static (candidates, _) => candidates)
                .Collect();
            context.RegisterSourceOutput(allFields.Combine(containerCandidates),
                static (spc, pair) => Emitter.ReportMissingContainers(spc, pair.Left.Left.AddRange(pair.Left.Right), pair.Right));

            var uncovered = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, ct) => CoverageScanner.IsCandidateField(node, ct),
                    transform: static (ctx, ct) => CoverageScanner.GetUncoveredFields(ctx, ct))
                .SelectMany(static (infos, _) => infos);
            context.RegisterSourceOutput(uncovered, static (spc, info) => spc.ReportDiagnostic(info.ToDiagnostic()));
        }
    }
}
