using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Shaders;

/// <summary>Read-only, bounded comparison of translator-emitted LE3 serialized uniform trees.</summary>
internal static class LE3UniformExpressionComparer
{
    private const int MaximumDepth = 128;
    private const int MaximumVisits = 8192;

    internal static UniformExpressionComparison Compare(TranslationResult translation, MaterialShaderMap expectedMap)
    {
        ArgumentNullException.ThrowIfNull(translation);
        ArgumentNullException.ThrowIfNull(expectedMap);
        var errors = new List<string>();
        var differences = new List<string>();
        var memo = new Dictionary<(MaterialUniformExpression, MaterialUniformExpression), string>();
        bool translationSucceeded = translation.Errors != null && translation.Success;
        if (!translationSucceeded) errors.Add("A successful material translation is required for uniform comparison.");
        int expectedCount = 0, translatedCount = 0, visits = 0;
        bool limitReported = false;
        CompareArray(nameof(expectedMap.UniformPixelVectorExpressions), translation.UniformPixelVectorExpressions, expectedMap.UniformPixelVectorExpressions);
        CompareArray(nameof(expectedMap.UniformPixelScalarExpressions), translation.UniformPixelScalarExpressions, expectedMap.UniformPixelScalarExpressions);
        CompareArray(nameof(expectedMap.Uniform2DTextureExpressions), translation.Uniform2DTextureExpressions, expectedMap.Uniform2DTextureExpressions);
        CompareArray(nameof(expectedMap.UniformCubeTextureExpressions), translation.UniformCubeTextureExpressions, expectedMap.UniformCubeTextureExpressions);
        CompareArray(nameof(expectedMap.UniformVertexVectorExpressions), translation.UniformVertexVectorExpressions, expectedMap.UniformVertexVectorExpressions);
        CompareArray(nameof(expectedMap.UniformVertexScalarExpressions), translation.UniformVertexScalarExpressions, expectedMap.UniformVertexScalarExpressions);
        return new UniformExpressionComparison
        {
            TranslationSucceeded = translationSucceeded,
            ExpectedExpressionCount = expectedCount, TranslatedExpressionCount = translatedCount,
            Errors = errors.AsReadOnly(), Differences = differences.AsReadOnly()
        };

        void CompareArray(string name, IReadOnlyList<MaterialUniformExpression> actual, IReadOnlyList<MaterialUniformExpression> expected)
        {
            if (actual == null) errors.Add($"Translated.{name} must be supplied, even when empty.");
            if (expected == null) errors.Add($"Expected.{name} must be supplied, even when empty.");
            translatedCount += actual?.Count ?? 0; expectedCount += expected?.Count ?? 0;
            // Validate both complete arrays, including entries without a counterpart. A count
            // mismatch must not hide an unsupported or cyclic trailing tree.
            bool actualValid = ValidateArray(actual, "Translated." + name);
            bool expectedValid = ValidateArray(expected, "Expected." + name);
            if (actual == null || expected == null) return;
            if (actual.Count != expected.Count)
                differences.Add($"{name}.Count: translated {actual.Count}, expected {expected.Count}.");
            if (!actualValid || !expectedValid) return;
            for (int i = 0; i < Math.Min(actual.Count, expected.Count); i++)
                if (Difference(actual[i], expected[i]) is { } reason)
                    differences.Add($"{name}[{i}].{reason}");
        }

        bool ValidateArray(IReadOnlyList<MaterialUniformExpression> array, string name)
        {
            if (array == null) return false;
            bool valid = true;
            var active = new HashSet<MaterialUniformExpression>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] != null && (array[i] is MaterialUniformExpressionTexture and not MaterialUniformExpressionFlipBookTextureParameter) != name.Contains("TextureExpressions", StringComparison.Ordinal))
                {
                    errors.Add($"{name}[{i}] has the wrong numeric/texture root category for this uniform array.");
                    valid = false;
                }
                valid &= Validate(array[i], $"{name}[{i}]", active, 0);
                if (limitReported) return false;
            }
            return valid;
        }

        bool Validate(MaterialUniformExpression node, string path, HashSet<MaterialUniformExpression> active, int depth)
        {
            if (++visits > MaximumVisits)
            {
                if (!limitReported) errors.Add($"Uniform comparison exceeds the {MaximumVisits}-visit validation limit.");
                limitReported = true; return false;
            }
            if (node == null) { errors.Add($"{path} is null."); return false; }
            if (depth > MaximumDepth) { errors.Add($"{path} exceeds the {MaximumDepth}-level nesting limit."); return false; }
            if (!active.Add(node)) { errors.Add($"{path} contains a uniform-expression cycle."); return false; }
            try
            {
                string typeName = TypeName(node.GetType());
                if (typeName == null) { errors.Add($"{path} has unsupported uniform class {node.GetType().Name}."); return false; }
                bool valid = true;
                if (node.ExpressionType.Number != 0 || !string.Equals(node.ExpressionType.Name, typeName, StringComparison.Ordinal))
                { errors.Add($"{path}.ExpressionType must be {typeName} with Number=0."); valid = false; }
                switch (node)
                {
                    case MaterialUniformExpressionConstant constant when constant.ValueType is not (1 or 2 or 4 or 8 or 15):
                        errors.Add($"{path}.ValueType is not a supported numeric material type."); valid = false; break;
                    case MaterialUniformExpressionScalarParameter scalar: valid &= ValidateName(scalar.ParameterName, path); break;
                    case MaterialUniformExpressionVectorParameter vector: valid &= ValidateName(vector.ParameterName, path); break;
                    case MaterialUniformExpressionTextureParameter textureParameter: valid &= ValidateName(textureParameter.ParameterName, path); break;
                    case MaterialUniformExpressionFoldedMath math when (byte)math.Op > 4:
                        errors.Add($"{path}.Op is not a supported folded-math operation."); valid = false; break;
                    case MaterialUniformExpressionAppendVector append when append.NumComponentsA is < 1 or > 4:
                        errors.Add($"{path}.NumComponentsA must be in the range 1-4."); valid = false; break;
                }
                if (node is MaterialUniformExpressionTexture texture && texture.TextureIndex < 0)
                { errors.Add($"{path}.TextureIndex must be a resolved nonnegative resource slot."); valid = false; }
                if (node is MaterialUniformExpressionUnaryOp unary) valid &= Validate(unary.X, path + ".X", active, depth + 1);
                if (node is MaterialUniformExpressionBinaryOp binary)
                {
                    valid &= Validate(binary.A, path + ".A", active, depth + 1);
                    valid &= Validate(binary.B, path + ".B", active, depth + 1);
                }
                if (node is MaterialUniformExpressionClamp clamp)
                {
                    valid &= Validate(clamp.Input, path + ".Input", active, depth + 1);
                    valid &= Validate(clamp.Min, path + ".Min", active, depth + 1);
                    valid &= Validate(clamp.Max, path + ".Max", active, depth + 1);
                }
                return valid;
            }
            finally { active.Remove(node); }
        }

        bool ValidateName(NameReference name, string path)
        {
            if (!string.IsNullOrWhiteSpace(name.Name) && name.Number >= 0) return true;
            errors.Add($"{path}.ParameterName requires a nonempty name and nonnegative Number."); return false;
        }

        string Difference(MaterialUniformExpression actual, MaterialUniformExpression expected)
        {
            var key = (actual, expected);
            if (memo.TryGetValue(key, out string cached)) return cached;
            string reason = FindDifference(); memo.Add(key, reason); return reason;

            string FindDifference()
            {
                if (actual.GetType() != expected.GetType()) return "Class differs.";
                switch (actual)
                {
                    case MaterialUniformExpressionConstant a:
                        var b = (MaterialUniformExpressionConstant)expected;
                        if (a.ValueType != b.ValueType) return "ValueType differs.";
                        if (!ColorEquals(a.Value, b.Value)) return $"Value differs ({Describe(a.Value)} vs expected {Describe(b.Value)}).";
                        break;
                    case MaterialUniformExpressionScalarParameter a:
                        var scalar = (MaterialUniformExpressionScalarParameter)expected;
                        if (a.ParameterName != scalar.ParameterName) return "ParameterName differs.";
                        if (!FloatIdentical(a.DefaultValue, scalar.DefaultValue)) return $"DefaultValue differs ({a.DefaultValue:R} vs expected {scalar.DefaultValue:R}).";
                        break;
                    case MaterialUniformExpressionVectorParameter a:
                        var vector = (MaterialUniformExpressionVectorParameter)expected;
                        if (a.ParameterName != vector.ParameterName) return "ParameterName differs.";
                        if (!ColorEquals(a.DefaultValue, vector.DefaultValue)) return $"DefaultValue differs ({Describe(a.DefaultValue)} vs expected {Describe(vector.DefaultValue)}).";
                        break;
                    case MaterialUniformExpressionTextureParameter a:
                        if (a.ParameterName != ((MaterialUniformExpressionTextureParameter)expected).ParameterName) return "ParameterName differs.";
                        break;
                    case MaterialUniformExpressionSine a:
                        if (a.bIsCosine != ((MaterialUniformExpressionSine)expected).bIsCosine) return "bIsCosine differs.";
                        break;
                    case MaterialUniformExpressionFoldedMath a:
                        if (a.Op != ((MaterialUniformExpressionFoldedMath)expected).Op) return "Op differs.";
                        break;
                    case MaterialUniformExpressionAppendVector a:
                        if (a.NumComponentsA != ((MaterialUniformExpressionAppendVector)expected).NumComponentsA) return "NumComponentsA differs.";
                        break;
                }
                if (actual is MaterialUniformExpressionTexture texture && texture.TextureIndex != ((MaterialUniformExpressionTexture)expected).TextureIndex)
                    return "TextureIndex differs.";
                if (actual is MaterialUniformExpressionUnaryOp unary && Difference(unary.X, ((MaterialUniformExpressionUnaryOp)expected).X) is { } unaryReason)
                    return "X." + unaryReason;
                if (actual is MaterialUniformExpressionBinaryOp binary)
                {
                    var other = (MaterialUniformExpressionBinaryOp)expected;
                    if (Difference(binary.A, other.A) is { } aReason) return "A." + aReason;
                    if (Difference(binary.B, other.B) is { } bReason) return "B." + bReason;
                }
                if (actual is MaterialUniformExpressionClamp clamp)
                {
                    var other = (MaterialUniformExpressionClamp)expected;
                    if (Difference(clamp.Input, other.Input) is { } inputReason) return "Input." + inputReason;
                    if (Difference(clamp.Min, other.Min) is { } minReason) return "Min." + minReason;
                    if (Difference(clamp.Max, other.Max) is { } maxReason) return "Max." + maxReason;
                }
                return null;
            }
        }
    }

    // LE3 leaf identity uses UCOMISS/JNZ without a parity branch: equality OR unordered
    // passes. This is deliberately not mathematical equivalence or bitwise equality.
    internal static bool FloatIdentical(float a, float b) => a == b || float.IsNaN(a) || float.IsNaN(b);
    internal static bool ColorIdentical(LinearColor a, LinearColor b) => FloatIdentical(a.R, b.R) &&
        FloatIdentical(a.G, b.G) && FloatIdentical(a.B, b.B) && FloatIdentical(a.A, b.A);
    private static bool ColorEquals(LinearColor a, LinearColor b) => ColorIdentical(a, b);

    private static string Describe(LinearColor c) => $"{c.R:R},{c.G:R},{c.B:R},{c.A:R}";

    private static string TypeName(Type type) => type.Name switch
    {
        nameof(MaterialUniformExpressionConstant) when type == typeof(MaterialUniformExpressionConstant) => "FMaterialUniformExpressionConstant",
        nameof(MaterialUniformExpressionScalarParameter) when type == typeof(MaterialUniformExpressionScalarParameter) => "FMaterialUniformExpressionScalarParameter",
        nameof(MaterialUniformExpressionVectorParameter) when type == typeof(MaterialUniformExpressionVectorParameter) => "FMaterialUniformExpressionVectorParameter",
        nameof(MaterialUniformExpressionTexture) when type == typeof(MaterialUniformExpressionTexture) => "FMaterialUniformExpressionTexture",
        nameof(MaterialUniformExpressionFlipBookTextureParameter) when type == typeof(MaterialUniformExpressionFlipBookTextureParameter) => "FMaterialUniformExpressionFlipBookTextureParameter",
        nameof(MaterialUniformExpressionTextureParameter) when type == typeof(MaterialUniformExpressionTextureParameter) => "FMaterialUniformExpressionTextureParameter",
        nameof(MaterialUniformExpressionTime) when type == typeof(MaterialUniformExpressionTime) => "FMaterialUniformExpressionTime",
        nameof(MaterialUniformExpressionRealTime) when type == typeof(MaterialUniformExpressionRealTime) => "FMaterialUniformExpressionRealTime",
        nameof(MaterialUniformExpressionAbs) when type == typeof(MaterialUniformExpressionAbs) => "FMaterialUniformExpressionAbs",
        nameof(MaterialUniformExpressionCeil) when type == typeof(MaterialUniformExpressionCeil) => "FMaterialUniformExpressionCeil",
        nameof(MaterialUniformExpressionFloor) when type == typeof(MaterialUniformExpressionFloor) => "FMaterialUniformExpressionFloor",
        nameof(MaterialUniformExpressionFrac) when type == typeof(MaterialUniformExpressionFrac) => "FMaterialUniformExpressionFrac",
        nameof(MaterialUniformExpressionPeriodic) when type == typeof(MaterialUniformExpressionPeriodic) => "FMaterialUniformExpressionPeriodic",
        nameof(MaterialUniformExpressionSquareRoot) when type == typeof(MaterialUniformExpressionSquareRoot) => "FMaterialUniformExpressionSquareRoot",
        nameof(MaterialUniformExpressionSine) when type == typeof(MaterialUniformExpressionSine) => "FMaterialUniformExpressionSine",
        nameof(MaterialUniformExpressionFmod) when type == typeof(MaterialUniformExpressionFmod) => "FMaterialUniformExpressionFmod",
        nameof(MaterialUniformExpressionBIOMod) when type == typeof(MaterialUniformExpressionBIOMod) => "FMaterialUniformExpressionBIOMod",
        nameof(MaterialUniformExpressionBIOSineSubtend) when type == typeof(MaterialUniformExpressionBIOSineSubtend) => "FMaterialUniformExpressionBIOSineSubtend",
        nameof(MaterialUniformExpressionMax) when type == typeof(MaterialUniformExpressionMax) => "FMaterialUniformExpressionMax",
        nameof(MaterialUniformExpressionMin) when type == typeof(MaterialUniformExpressionMin) => "FMaterialUniformExpressionMin",
        nameof(MaterialUniformExpressionAppendVector) when type == typeof(MaterialUniformExpressionAppendVector) => "FMaterialUniformExpressionAppendVector",
        nameof(MaterialUniformExpressionFoldedMath) when type == typeof(MaterialUniformExpressionFoldedMath) => "FMaterialUniformExpressionFoldedMath",
        nameof(MaterialUniformExpressionClamp) when type == typeof(MaterialUniformExpressionClamp) => "FMaterialUniformExpressionClamp",
        _ => null
    };
}
