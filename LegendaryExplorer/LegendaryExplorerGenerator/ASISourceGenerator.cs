using System;
using Microsoft.CodeAnalysis;

namespace LegendaryExplorerGenerator
{
    // This is not hooked up to LEX right now cause it doesn't do anything
    // If ever implemented, it needs added to the .csproj.
    // This is the V2 API (.NET 6)

    /// <summary>
    /// This library is a source generator for Legendary Explorer
    /// </summary>
    [Generator]
    public class LegendaryGenerator : IIncrementalGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            // Code generation goes here

            // Todo: Auto update the Interop ASI hashes
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Not sure what goes here
        }
    }
}
