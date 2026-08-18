using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LegendaryExplorerCore.SourceGenerators
{
    internal enum VisitKind
    {
        /// <summary>A single UIndex.</summary>
        Single,
        /// <summary>UIndex[]</summary>
        Array,
        /// <summary>List&lt;UIndex&gt;</summary>
        List,
        /// <summary>The keys of a UMap/UMultiMap.</summary>
        MapKey,
        /// <summary>The values of a UMap/UMultiMap.</summary>
        MapValue,
        /// <summary>A field whose type has its own annotated fields.</summary>
        ContainerSingle,
        ContainerArray,
        ContainerList,
        /// <summary>A map whose keys are a type with its own annotated fields.</summary>
        ContainerMapKey,
        /// <summary>A map whose values are a type with its own annotated fields.</summary>
        ContainerMapValue
    }

    /// <summary>
    /// The class a reference is expected to be an instance of, and the games that expectation applies to.
    /// </summary>
    internal sealed class ClassForGames : IEquatable<ClassForGames>
    {
        /// <summary>Null when the reference can be of any class.</summary>
        public string AcceptedClass { get; }
        /// <summary>MEGame member names. Empty means every game not claimed by another entry.</summary>
        public EquatableArray<string> Games { get; }

        public ClassForGames(string acceptedClass, EquatableArray<string> games)
        {
            AcceptedClass = acceptedClass;
            Games = games;
        }

        public bool IsFallback => Games.Length == 0;

        public bool Equals(ClassForGames other) =>
            other is not null && AcceptedClass == other.AcceptedClass && Games.Equals(other.Games);

        public override bool Equals(object obj) => Equals(obj as ClassForGames);

        public override int GetHashCode() =>
            unchecked((AcceptedClass?.GetHashCode() ?? 0) * 31 + Games.GetHashCode());
    }

    /// <summary>
    /// Everything the emitter needs to produce one statement group for one field.
    /// </summary>
    internal sealed class FieldVisit : IEquatable<FieldVisit>
    {
        public VisitKind Kind { get; }
        public string FieldName { get; }
        /// <summary>Which class is expected in which game. Empty for container visits.</summary>
        public EquatableArray<ClassForGames> Classes { get; }
        /// <summary>For container visits: whether the contained type is a value type (so it can't be null).</summary>
        public bool ContainedTypeIsValueType { get; }
        /// <summary>For container visits: the type that is recursed into.</summary>
        public string ContainedTypeFqn { get; }
        /// <summary>Where to point diagnostics that can only be worked out once every type is known.</summary>
        public LocationInfo Location { get; }
        /// <summary>
        /// True for visits the generator offered on spec rather than the author asking for them, namely the two
        /// halves of a map. Dropping one of those isn't a mistake worth reporting.
        /// </summary>
        public bool IsSpeculative { get; }

        public FieldVisit(VisitKind kind, string fieldName, EquatableArray<ClassForGames> classes,
            bool containedTypeIsValueType = false, string containedTypeFqn = null, LocationInfo location = null,
            bool isSpeculative = false)
        {
            IsSpeculative = isSpeculative;
            Kind = kind;
            FieldName = fieldName;
            Classes = classes;
            ContainedTypeIsValueType = containedTypeIsValueType;
            ContainedTypeFqn = containedTypeFqn;
            Location = location;
        }

        public bool Equals(FieldVisit other) =>
            other is not null
            && Kind == other.Kind
            && FieldName == other.FieldName
            && Classes.Equals(other.Classes)
            && ContainedTypeIsValueType == other.ContainedTypeIsValueType
            && ContainedTypeFqn == other.ContainedTypeFqn
            && IsSpeculative == other.IsSpeculative
            && Equals(Location, other.Location);

        public override bool Equals(object obj) => Equals(obj as FieldVisit);

        public override int GetHashCode()
        {
            int hash = (int)Kind;
            hash = unchecked(hash * 31 + FieldName.GetHashCode());
            hash = unchecked(hash * 31 + Classes.GetHashCode());
            hash = unchecked(hash * 31 + ContainedTypeIsValueType.GetHashCode());
            hash = unchecked(hash * 31 + (ContainedTypeFqn?.GetHashCode() ?? 0));
            return hash;
        }
    }

    /// <summary>
    /// A field that might need [UIndexRefContainer], pending knowing which types ended up annotated.
    /// </summary>
    internal sealed class ContainerCandidate : IEquatable<ContainerCandidate>
    {
        public string ContainingTypeName { get; }
        public string FieldName { get; }
        /// <summary>The field's type and, for arrays and Lists, its element type.</summary>
        public EquatableArray<string> CandidateTypeFqns { get; }
        public LocationInfo Location { get; }

        public ContainerCandidate(string containingTypeName, string fieldName,
            EquatableArray<string> candidateTypeFqns, LocationInfo location)
        {
            ContainingTypeName = containingTypeName;
            FieldName = fieldName;
            CandidateTypeFqns = candidateTypeFqns;
            Location = location;
        }

        public bool Equals(ContainerCandidate other) =>
            other is not null
            && ContainingTypeName == other.ContainingTypeName
            && FieldName == other.FieldName
            && CandidateTypeFqns.Equals(other.CandidateTypeFqns)
            && Equals(Location, other.Location);

        public override bool Equals(object obj) => Equals(obj as ContainerCandidate);

        public override int GetHashCode() =>
            unchecked((ContainingTypeName.GetHashCode() * 31 + FieldName.GetHashCode()) * 31 + CandidateTypeFqns.GetHashCode());
    }

    /// <summary>
    /// Enough information about a type to re-open it as a partial declaration, without holding onto a symbol.
    /// </summary>
    internal sealed class TypeShell : IEquatable<TypeShell>
    {
        /// <summary>Fully qualified, including the global:: prefix. Used as the identity of the type.</summary>
        public string Fqn { get; }
        /// <summary>Containing namespace, or the empty string.</summary>
        public string Namespace { get; }
        /// <summary>Declaration headers from outermost containing type down to this type, e.g. "partial class Level".</summary>
        public EquatableArray<string> DeclarationHeaders { get; }
        /// <summary>False if this type or any type it is nested in isn't declared partial.</summary>
        public bool IsPartial { get; }
        public bool IsValueType { get; }
        public bool IsSealed { get; }
        /// <summary>
        /// True if this type inherits ObjectBinary's own VerifyUIndexRefs, which takes no prefix. A container
        /// field passes one, so it has nothing it can call on such a type.
        /// </summary>
        public bool DerivesFromObjectBinary { get; }
        /// <summary>The name to display in diagnostics about the type itself, namespace included.</summary>
        public string DisplayName { get; }
        /// <summary>
        /// The name to display in diagnostics about one of the type's fields, where the namespace is noise but
        /// any containing types aren't. See <see cref="Parser.ShortNameOf"/>.
        /// </summary>
        public string ShortName { get; }
        public LocationInfo Location { get; }

        public TypeShell(string fqn, string ns, EquatableArray<string> declarationHeaders, bool isPartial,
            bool isValueType, bool isSealed, bool derivesFromObjectBinary, string displayName, string shortName,
            LocationInfo location)
        {
            Fqn = fqn;
            Namespace = ns;
            DeclarationHeaders = declarationHeaders;
            IsPartial = isPartial;
            IsValueType = isValueType;
            IsSealed = isSealed;
            DerivesFromObjectBinary = derivesFromObjectBinary;
            DisplayName = displayName;
            ShortName = shortName;
            Location = location;
        }

        /// <summary>A unique, filename-safe identifier for this type.</summary>
        public string HintName
        {
            get
            {
                string s = Fqn.StartsWith("global::", StringComparison.Ordinal) ? Fqn.Substring("global::".Length) : Fqn;
                var chars = s.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '_')
                    {
                        chars[i] = '_';
                    }
                }
                return new string(chars);
            }
        }

        public bool Equals(TypeShell other) =>
            other is not null
            && Fqn == other.Fqn
            && Namespace == other.Namespace
            && DeclarationHeaders.Equals(other.DeclarationHeaders)
            && IsPartial == other.IsPartial
            && IsValueType == other.IsValueType
            && IsSealed == other.IsSealed
            && DerivesFromObjectBinary == other.DerivesFromObjectBinary
            && DisplayName == other.DisplayName
            && Equals(Location, other.Location);

        public override bool Equals(object obj) => Equals(obj as TypeShell);

        public override int GetHashCode()
        {
            int hash = Fqn.GetHashCode();
            hash = unchecked(hash * 31 + DeclarationHeaders.GetHashCode());
            hash = unchecked(hash * 31 + IsPartial.GetHashCode());
            return hash;
        }
    }

    /// <summary>
    /// The annotated fields of one type, plus the ancestry needed to decide whether the generated method
    /// overrides something.
    /// </summary>
    internal sealed class AnnotatedField : IEquatable<AnnotatedField>
    {
        public TypeShell ContainingType { get; }
        /// <summary>True if the containing type derives from ObjectBinary, which declares the root virtual method.</summary>
        public bool IsObjectBinary { get; }
        /// <summary>Base types from the immediate base upwards, stopping at ObjectBinary/object.</summary>
        public EquatableArray<TypeShell> BaseChain { get; }
        public EquatableArray<FieldVisit> Visits { get; }
        public EquatableArray<DiagnosticInfo> Diagnostics { get; }

        public AnnotatedField(TypeShell containingType, bool isObjectBinary, EquatableArray<TypeShell> baseChain,
            EquatableArray<FieldVisit> visits, EquatableArray<DiagnosticInfo> diagnostics)
        {
            ContainingType = containingType;
            IsObjectBinary = isObjectBinary;
            BaseChain = baseChain;
            Visits = visits;
            Diagnostics = diagnostics;
        }

        public bool Equals(AnnotatedField other) =>
            other is not null
            && Equals(ContainingType, other.ContainingType)
            && IsObjectBinary == other.IsObjectBinary
            && BaseChain.Equals(other.BaseChain)
            && Visits.Equals(other.Visits)
            && Diagnostics.Equals(other.Diagnostics);

        public override bool Equals(object obj) => Equals(obj as AnnotatedField);

        public override int GetHashCode()
        {
            int hash = ContainingType?.GetHashCode() ?? 0;
            hash = unchecked(hash * 31 + Visits.GetHashCode());
            hash = unchecked(hash * 31 + Diagnostics.GetHashCode());
            return hash;
        }
    }

    /// <summary>
    /// A <see cref="Location"/> reduced to equatable data, so models don't root syntax trees.
    /// </summary>
    internal sealed class LocationInfo : IEquatable<LocationInfo>
    {
        public string FilePath { get; }
        public TextSpan TextSpan { get; }
        public LinePositionSpan LineSpan { get; }

        public LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
        {
            FilePath = filePath;
            TextSpan = textSpan;
            LineSpan = lineSpan;
        }

        public static LocationInfo From(SyntaxNode node) => From(node?.GetLocation());

        public static LocationInfo From(Location location)
        {
            if (location is null || location.SourceTree is null)
            {
                return null;
            }
            return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }

        public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

        public bool Equals(LocationInfo other) =>
            other is not null && FilePath == other.FilePath && TextSpan == other.TextSpan && LineSpan == other.LineSpan;

        public override bool Equals(object obj) => Equals(obj as LocationInfo);

        public override int GetHashCode() => unchecked(FilePath.GetHashCode() * 31 + TextSpan.GetHashCode());
    }

    /// <summary>A diagnostic reduced to equatable data.</summary>
    internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
    {
        private readonly DiagnosticDescriptor _descriptor;
        private readonly LocationInfo _location;
        private readonly EquatableArray<string> _messageArgs;

        public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo location, params string[] messageArgs)
        {
            _descriptor = descriptor;
            _location = location;
            _messageArgs = new EquatableArray<string>(messageArgs ?? Array.Empty<string>());
        }

        public Diagnostic ToDiagnostic()
        {
            var args = new object[_messageArgs.Length];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = _messageArgs[i];
            }
            return Diagnostic.Create(_descriptor, _location?.ToLocation(), args);
        }

        public bool Equals(DiagnosticInfo other) =>
            other is not null
            && _descriptor.Id == other._descriptor.Id
            && Equals(_location, other._location)
            && _messageArgs.Equals(other._messageArgs);

        public override bool Equals(object obj) => Equals(obj as DiagnosticInfo);

        public override int GetHashCode() =>
            unchecked((_descriptor.Id.GetHashCode() * 31 + (_location?.GetHashCode() ?? 0)) * 31 + _messageArgs.GetHashCode());
    }
}
