using Microsoft.CodeAnalysis;

namespace LegendaryExplorerCore.SourceGenerators
{
    internal static class Diagnostics
    {
        private const string Category = "UIndexRefs";

        /// <summary>A UIndex field that nobody has said what class it refers to yet.</summary>
        public static readonly DiagnosticDescriptor UnannotatedUIndexField = new DiagnosticDescriptor(
            id: "LEX0001",
            title: "UIndex field is not annotated",
            messageFormat: "UIndex field '{0}.{1}' has no [UIndexRef] attribute, so its references can't be type checked. Specify the class(es) it can refer to, or use [UIndexRef] with no arguments if any class is valid",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>A field holding a type that has annotated fields, but which nothing recurses into.</summary>
        public static readonly DiagnosticDescriptor MissingContainerAttribute = new DiagnosticDescriptor(
            id: "LEX0002",
            title: "Field holding UIndex refs is never recursed into",
            messageFormat: "Field '{0}.{1}' is of type '{2}', which has [UIndexRef] fields of its own, but the field has no [UIndexRefContainer] attribute, so those references are never verified",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>The field type is one the generator has no code shape for.</summary>
        public static readonly DiagnosticDescriptor UnsupportedFieldShape = new DiagnosticDescriptor(
            id: "LEX0003",
            title: "Unsupported UIndex field shape",
            messageFormat: "Field '{0}.{1}' is of type '{2}', which the UIndex ref generator can't handle. It will not be verified; use [ManualUIndexRefVerification] on the type and write VerifyUIndexRefs by hand",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>[UIndexRef] on something that isn't an int, or [UIndexRefContainer] on something with no annotated fields.</summary>
        public static readonly DiagnosticDescriptor AttributeOnWrongFieldType = new DiagnosticDescriptor(
            id: "LEX0004",
            title: "UIndex ref attribute applied to an incompatible field",
            messageFormat: "Field '{0}.{1}' is of type '{2}'. {3}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>Can't add a member to a type that isn't partial.</summary>
        public static readonly DiagnosticDescriptor TypeMustBePartial = new DiagnosticDescriptor(
            id: "LEX0005",
            title: "Type with UIndex ref annotations must be partial",
            messageFormat: "Type '{0}' has UIndex ref annotations, so it (and any type it is nested in) must be declared 'partial'. No verification code was generated for it",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>Two [UIndexRef]s that would produce the same visit.</summary>
        public static readonly DiagnosticDescriptor ConflictingAttributes = new DiagnosticDescriptor(
            id: "LEX0006",
            title: "Conflicting UIndexRef attributes",
            messageFormat: "Field '{0}.{1}' has multiple [UIndexRef] attributes that apply to the same thing. Multiple attributes must specify different games and/or map parts (key/value)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
