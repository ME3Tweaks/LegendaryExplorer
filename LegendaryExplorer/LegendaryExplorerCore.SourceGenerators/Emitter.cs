using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LegendaryExplorerCore.SourceGenerators
{
    internal static class Emitter
    {
        private const string VerifierParam = "verifier";
        private const string PrefixParam = "prefix";
        private const string GameParam = "game";
        private const string ClassLocal = "acceptedClass";
        private const string ExtraHook = "VerifyExtraUIndexRefs";

        /// <summary>All the annotated fields of one type, gathered back together.</summary>
        private sealed class TypeGroup
        {
            public TypeShell Shell;
            public bool IsObjectBinary;
            public EquatableArray<TypeShell> BaseChain;
            public readonly List<FieldVisit> Visits = [];
        }

        public static void Emit(SourceProductionContext context, ImmutableArray<AnnotatedField> fields)
        {
            if (fields.IsDefaultOrEmpty)
            {
                return;
            }

            var reported = new HashSet<DiagnosticInfo>();
            var groups = new Dictionary<string, TypeGroup>(StringComparer.Ordinal);
            var knownShells = new Dictionary<string, TypeShell>(StringComparer.Ordinal);
            // Fqns of base types that an annotated type derives from, so we know where a virtual root is needed.
            var baseTypesOfAnnotated = new HashSet<string>(StringComparer.Ordinal);

            foreach (AnnotatedField field in fields)
            {
                if (field is null)
                {
                    continue;
                }
                foreach (DiagnosticInfo diagnostic in field.Diagnostics)
                {
                    if (reported.Add(diagnostic))
                    {
                        context.ReportDiagnostic(diagnostic.ToDiagnostic());
                    }
                }
                if (field.Visits.Length == 0)
                {
                    continue;
                }

                knownShells[field.ContainingType.Fqn] = field.ContainingType;
                foreach (TypeShell baseShell in field.BaseChain)
                {
                    if (!knownShells.ContainsKey(baseShell.Fqn))
                    {
                        knownShells[baseShell.Fqn] = baseShell;
                    }
                    baseTypesOfAnnotated.Add(baseShell.Fqn);
                }

                if (!groups.TryGetValue(field.ContainingType.Fqn, out TypeGroup group))
                {
                    groups[field.ContainingType.Fqn] = group = new TypeGroup
                    {
                        Shell = field.ContainingType,
                        IsObjectBinary = field.IsObjectBinary,
                        BaseChain = field.BaseChain
                    };
                }
                group.Visits.AddRange(field.Visits);
            }

            if (groups.Count == 0)
            {
                return;
            }

            // A container field can be declared as a base type that has no annotations of its own but whose
            // subclasses do (LightMap is the example). Those base types need a virtual no-op to dispatch through.
            var virtualRoots = new HashSet<string>(StringComparer.Ordinal);
            foreach (TypeGroup group in groups.Values)
            {
                foreach (FieldVisit visit in group.Visits)
                {
                    string contained = visit.ContainedTypeFqn;
                    if (contained is null || groups.ContainsKey(contained))
                    {
                        continue;
                    }
                    // An ObjectBinary already has a VerifyUIndexRefs of its own, with a different signature, so a
                    // virtual root can't be added to one. IsCallable reports those.
                    if (baseTypesOfAnnotated.Contains(contained)
                        && !(knownShells.TryGetValue(contained, out TypeShell containedShell) && containedShell.DerivesFromObjectBinary))
                    {
                        virtualRoots.Add(contained);
                    }
                }
            }

            foreach (TypeGroup group in groups.Values.OrderBy(g => g.Shell.Fqn, StringComparer.Ordinal))
            {
                EmitType(context, group, groups, virtualRoots, knownShells);
            }

            foreach (string rootFqn in virtualRoots.OrderBy(f => f, StringComparer.Ordinal))
            {
                EmitVirtualRoot(context, knownShells[rootFqn]);
            }
        }

        /// <summary>
        /// Flags fields holding a type that has annotated fields but which nothing recurses into. A type can be
        /// fully annotated and still never be visited, because reaching it depends on the field that holds it.
        /// </summary>
        public static void ReportMissingContainers(SourceProductionContext context,
            ImmutableArray<AnnotatedField> fields, ImmutableArray<ContainerCandidate> candidates)
        {
            if (fields.IsDefaultOrEmpty || candidates.IsDefaultOrEmpty)
            {
                return;
            }

            // A field is worth recursing into if its type is annotated, or is a base of an annotated type
            // (LightMap has no annotations of its own, but LightMap_1D does).
            var recursable = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (AnnotatedField field in fields)
            {
                if (field is null || field.Visits.Length == 0)
                {
                    continue;
                }
                recursable[field.ContainingType.Fqn] = field.ContainingType.DisplayName;
                foreach (TypeShell baseShell in field.BaseChain)
                {
                    if (!recursable.ContainsKey(baseShell.Fqn))
                    {
                        recursable[baseShell.Fqn] = baseShell.DisplayName;
                    }
                }
            }

            foreach (ContainerCandidate candidate in candidates)
            {
                foreach (string fqn in candidate.CandidateTypeFqns)
                {
                    if (recursable.TryGetValue(fqn, out string displayName))
                    {
                        context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.MissingContainerAttribute,
                            candidate.Location, candidate.ContainingTypeName, candidate.FieldName, displayName).ToDiagnostic());
                        break;
                    }
                }
            }
        }

        private static void EmitType(SourceProductionContext context, TypeGroup group,
            Dictionary<string, TypeGroup> groups, HashSet<string> virtualRoots, Dictionary<string, TypeShell> knownShells)
        {
            TypeShell shell = group.Shell;
            bool hasPrefix = !group.IsObjectBinary;

            // Which of our ancestors, if any, already declares the method we're about to write.
            string overriddenBase = null;
            if (!group.IsObjectBinary)
            {
                foreach (TypeShell baseShell in group.BaseChain)
                {
                    if (groups.ContainsKey(baseShell.Fqn) || virtualRoots.Contains(baseShell.Fqn))
                    {
                        overriddenBase = baseShell.Fqn;
                        break;
                    }
                }
            }

            string modifier;
            if (group.IsObjectBinary || overriddenBase is not null)
            {
                modifier = "override ";
            }
            else if (shell.IsValueType || shell.IsSealed)
            {
                modifier = "";
            }
            else
            {
                modifier = "virtual ";
            }

            var writer = new IndentedWriter();
            WriteHeader(writer);
            OpenType(writer, shell, out int closeCount);

            var visits = group.Visits
                .Where(v => IsCallable(v, shell.ShortName, groups, virtualRoots, knownShells, context))
                .OrderBy(v => v.FieldName, StringComparer.Ordinal)
                .ThenBy(v => (int)v.Kind)
                .ToList();

            writer.Line("/// <summary>Generated. See <see cref=\"" + Parser.ObjectBinaryFqn.Substring("global::".Length) + ".VerifyUIndexRefs\"/>.</summary>");
            writer.Line(hasPrefix
                ? $"public {modifier}void VerifyUIndexRefs({Parser.MEGameFqn} {GameParam}, {Parser.VerifierFqn} {VerifierParam}, string {PrefixParam})"
                : $"public {modifier}void VerifyUIndexRefs({Parser.MEGameFqn} {GameParam}, {Parser.VerifierFqn} {VerifierParam})");
            writer.Line("{");
            writer.Indent++;
            if (group.IsObjectBinary)
            {
                writer.Line($"base.VerifyUIndexRefs({GameParam}, {VerifierParam});");
            }
            else if (overriddenBase is not null)
            {
                writer.Line($"base.VerifyUIndexRefs({GameParam}, {VerifierParam}, {PrefixParam});");
            }
            foreach (FieldVisit visit in visits)
            {
                WriteVisit(writer, visit, hasPrefix);
            }
            writer.Line(hasPrefix
                ? $"{ExtraHook}({GameParam}, {VerifierParam}, {PrefixParam});"
                : $"{ExtraHook}({GameParam}, {VerifierParam});");
            writer.Indent--;
            writer.Line("}");
            writer.Blank();
            writer.Line("/// <summary>");
            writer.Line("/// Implement this in the hand written part of the type to visit references that aren't");
            writer.Line("/// [UIndexRef] fields, such as ones decoded out of bytecode. Left unimplemented, both this");
            writer.Line("/// and the call above are removed by the compiler.");
            writer.Line("/// </summary>");
            writer.Line(hasPrefix
                ? $"partial void {ExtraHook}({Parser.MEGameFqn} {GameParam}, {Parser.VerifierFqn} {VerifierParam}, string {PrefixParam});"
                : $"partial void {ExtraHook}({Parser.MEGameFqn} {GameParam}, {Parser.VerifierFqn} {VerifierParam});");

            CloseType(writer, closeCount);
            context.AddSource($"{shell.HintName}.UIndexRefs.g.cs", SourceText.From(writer.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// A container field is only emitted if the type it points at actually ends up with a generated method.
        /// </summary>
        private static bool IsCallable(FieldVisit visit, string containingTypeName, Dictionary<string, TypeGroup> groups,
            HashSet<string> virtualRoots, Dictionary<string, TypeShell> knownShells, SourceProductionContext context)
        {
            if (visit.ContainedTypeFqn is null)
            {
                return true;
            }

            // An ObjectBinary's generated method overrides the one ObjectBinary declares, which takes no prefix,
            // so there is no overload for a container field to call. Caught here rather than left to fail as a
            // compile error inside generated code.
            if (knownShells.TryGetValue(visit.ContainedTypeFqn, out TypeShell containedShell)
                && containedShell.DerivesFromObjectBinary)
            {
                if (!visit.IsSpeculative)
                {
                    context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, visit.Location,
                        containingTypeName, visit.FieldName, containedShell.DisplayName,
                        "[UIndexRefContainer] can't recurse into an ObjectBinary, which verifies its own references. Refer to it with a [UIndexRef] UIndex instead, or move the shared fields into a type that doesn't derive from ObjectBinary.").ToDiagnostic());
                }
                return false;
            }

            if (groups.ContainsKey(visit.ContainedTypeFqn) || virtualRoots.Contains(visit.ContainedTypeFqn))
            {
                return true;
            }
            if (visit.IsSpeculative)
            {
                return false; // Only one half of the map was ever going to be the annotated one
            }
            context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.AttributeOnWrongFieldType, visit.Location,
                containingTypeName, visit.FieldName,
                containedShell?.DisplayName ?? visit.ContainedTypeFqn,
                "[UIndexRefContainer] was used, but that type has no [UIndexRef] fields of its own, and neither do any types deriving from it.").ToDiagnostic());
            return false;
        }

        private static void EmitVirtualRoot(SourceProductionContext context, TypeShell shell)
        {
            if (!shell.IsPartial)
            {
                context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.TypeMustBePartial, shell.Location, shell.DisplayName).ToDiagnostic());
                return;
            }
            var writer = new IndentedWriter();
            WriteHeader(writer);
            OpenType(writer, shell, out int closeCount);
            writer.Line("/// <summary>Generated. Overridden by the subclasses that have [UIndexRef] fields.</summary>");
            writer.Line($"public virtual void VerifyUIndexRefs({Parser.MEGameFqn} {GameParam}, {Parser.VerifierFqn} {VerifierParam}, string {PrefixParam})");
            writer.Line("{");
            writer.Line("}");
            CloseType(writer, closeCount);
            context.AddSource($"{shell.HintName}.UIndexRefs.g.cs", SourceText.From(writer.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// Emits one field's statements, wrapping them in a game switch when the expected class depends on
        /// which game the object came from.
        /// </summary>
        private static void WriteVisit(IndentedWriter writer, FieldVisit visit, bool hasPrefix)
        {
            var entries = visit.Classes.ToList();

            // The common case: one class (or none) that applies to every game.
            if (entries.Count <= 1 && (entries.Count == 0 || entries[0].IsFallback))
            {
                string literal = entries.Count == 1 ? Quote(entries[0].AcceptedClass) : "null";
                WriteVisitBody(writer, visit, hasPrefix, literal);
                return;
            }

            bool hasFallback = entries.Any(e => e.IsFallback);
            writer.Line("{");
            writer.Indent++;
            writer.Line($"string {ClassLocal};");
            if (!hasFallback)
            {
                // Games that no attribute claims don't have this reference at all, so they're skipped.
                writer.Line("bool applies = true;");
            }
            writer.Line($"switch ({GameParam})");
            writer.Line("{");
            writer.Indent++;
            foreach (ClassForGames entry in entries.Where(e => !e.IsFallback))
            {
                foreach (string gameName in entry.Games)
                {
                    writer.Line($"case {Parser.MEGameFqn}.{gameName}:");
                }
                writer.Indent++;
                writer.Line($"{ClassLocal} = {Quote(entry.AcceptedClass)};");
                writer.Line("break;");
                writer.Indent--;
            }
            writer.Line("default:");
            writer.Indent++;
            if (hasFallback)
            {
                writer.Line($"{ClassLocal} = {Quote(entries.First(e => e.IsFallback).AcceptedClass)};");
            }
            else
            {
                writer.Line($"{ClassLocal} = null;");
                writer.Line("applies = false;");
            }
            writer.Line("break;");
            writer.Indent--;
            writer.Indent--;
            writer.Line("}");

            if (hasFallback)
            {
                WriteVisitBody(writer, visit, hasPrefix, ClassLocal);
            }
            else
            {
                writer.Line("if (applies)");
                writer.Line("{");
                writer.Indent++;
                WriteVisitBody(writer, visit, hasPrefix, ClassLocal);
                writer.Indent--;
                writer.Line("}");
            }
            writer.Indent--;
            writer.Line("}");
        }

        private static void WriteVisitBody(IndentedWriter writer, FieldVisit visit, bool hasPrefix, string classes)
        {
            string field = visit.FieldName;
            switch (visit.Kind)
            {
                case VisitKind.Single:
                    writer.Line($"{VerifierParam}.Verify({field}, {classes}, {Path(hasPrefix, field)});");
                    break;
                case VisitKind.Array:
                case VisitKind.List:
                    string count = visit.Kind == VisitKind.Array ? "Length" : "Count";
                    writer.Line($"if ({field} is not null)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"for (int i = 0; i < {field}.{count}; i++)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"{VerifierParam}.Verify({field}[i], {classes}, {IndexedPath(hasPrefix, field)});");
                    writer.Indent--;
                    writer.Line("}");
                    writer.Indent--;
                    writer.Line("}");
                    break;
                case VisitKind.MapKey:
                case VisitKind.MapValue:
                    string half = visit.Kind == VisitKind.MapKey ? "Key" : "Value";
                    writer.Line($"if ({field} is not null)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line("int i = 0;");
                    writer.Line($"foreach (var kvp in {field})");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"{VerifierParam}.Verify(kvp.{half}, {classes}, {IndexedPath(hasPrefix, field, "." + half)});");
                    writer.Line("i++;");
                    writer.Indent--;
                    writer.Line("}");
                    writer.Indent--;
                    writer.Line("}");
                    break;
                case VisitKind.ContainerSingle:
                    string singleCall = visit.ContainedTypeIsValueType ? "." : "?.";
                    writer.Line($"{field}{singleCall}VerifyUIndexRefs({GameParam}, {VerifierParam}, {Path(hasPrefix, field + ".")});");
                    break;
                case VisitKind.ContainerMapKey:
                case VisitKind.ContainerMapValue:
                    string mapHalf = visit.Kind == VisitKind.ContainerMapKey ? "Key" : "Value";
                    string mapCall = visit.ContainedTypeIsValueType ? "." : "?.";
                    writer.Line($"if ({field} is not null)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line("int i = 0;");
                    writer.Line($"foreach (var kvp in {field})");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"kvp.{mapHalf}{mapCall}VerifyUIndexRefs({GameParam}, {VerifierParam}, {IndexedPath(hasPrefix, field, "." + mapHalf + ".")});");
                    writer.Line("i++;");
                    writer.Indent--;
                    writer.Line("}");
                    writer.Indent--;
                    writer.Line("}");
                    break;
                case VisitKind.ContainerArray:
                case VisitKind.ContainerList:
                    string containerCount = visit.Kind == VisitKind.ContainerArray ? "Length" : "Count";
                    string call = visit.ContainedTypeIsValueType ? "." : "?.";
                    writer.Line($"if ({field} is not null)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"for (int i = 0; i < {field}.{containerCount}; i++)");
                    writer.Line("{");
                    writer.Indent++;
                    writer.Line($"{field}[i]{call}VerifyUIndexRefs({GameParam}, {VerifierParam}, {IndexedPath(hasPrefix, field, ".")});");
                    writer.Indent--;
                    writer.Line("}");
                    writer.Indent--;
                    writer.Line("}");
                    break;
            }
        }

        /// <summary>"Foo" or prefix + "Foo".</summary>
        private static string Path(bool hasPrefix, string suffix) =>
            hasPrefix ? $"{PrefixParam} + \"{suffix}\"" : $"\"{suffix}\"";

        /// <summary>$"Foo[{i}]" or $"{prefix}Foo[{i}]".</summary>
        private static string IndexedPath(bool hasPrefix, string fieldName, string trailing = "") =>
            hasPrefix
                ? $"$\"{{{PrefixParam}}}{fieldName}[{{i}}]{trailing}\""
                : $"$\"{fieldName}[{{i}}]{trailing}\"";

        private static string Quote(string s) =>
            s is null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static void WriteHeader(IndentedWriter writer)
        {
            writer.Line("// <auto-generated/>");
            writer.Line("// Generated by LegendaryExplorerCore.SourceGenerators.UIndexRefGenerator. Do not edit.");
            writer.Blank();
        }

        private static void OpenType(IndentedWriter writer, TypeShell shell, out int closeCount)
        {
            closeCount = 0;
            if (shell.Namespace.Length > 0)
            {
                writer.Line($"namespace {shell.Namespace}");
                writer.Line("{");
                writer.Indent++;
                closeCount++;
            }
            foreach (string header in shell.DeclarationHeaders)
            {
                writer.Line(header);
                writer.Line("{");
                writer.Indent++;
                closeCount++;
            }
        }

        private static void CloseType(IndentedWriter writer, int closeCount)
        {
            for (int i = 0; i < closeCount; i++)
            {
                writer.Indent--;
                writer.Line("}");
            }
        }

        private sealed class IndentedWriter
        {
            private readonly StringBuilder _sb = new StringBuilder();
            public int Indent;

            public void Line(string text)
            {
                _sb.Append(' ', Indent * 4).Append(text).Append('\n');
            }

            public void Blank() => _sb.Append('\n');

            public override string ToString() => _sb.ToString();
        }
    }
}
