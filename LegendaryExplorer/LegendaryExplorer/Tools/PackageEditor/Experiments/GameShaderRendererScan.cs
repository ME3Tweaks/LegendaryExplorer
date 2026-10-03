using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using D3D11 = SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Checks every MaterialShaderMap in a game's reference shader cache against the steps the LevelEditor's game shader renderer
    /// (<see cref="MaterialRenderProxy"/>) takes, and reports which ones would fail, and why.
    /// </summary>
    public static class GameShaderRendererScan
    {
        private class FailureCategory
        {
            public int Count;
            public readonly List<string> Examples = [];
        }

        public static void ScanRefShaderCache(PackageEditorWindow pew, MEGame game = MEGame.LE3)
        {
            pew.IsBusy = true;
            pew.BusyText = $"Scanning {game} RefShaderCache";
            Task.Run(() => Scan(game, progress => pew.BusyText = progress)).ContinueWithOnUIThread(prevTask =>
            {
                pew.IsBusy = false;
                if (prevTask.Exception is not null)
                {
                    new ListDialog(new List<string> { prevTask.Exception.FlattenException() }, "Scan failed", "", pew).Show();
                    return;
                }
                (List<string> summary, string reportPath) = ((Task<(List<string>, string)>)prevTask).Result;
                new ListDialog(summary, "Game shader renderer compatibility", $"Full report: {reportPath}", pew).Show();
            });
        }

        public static (List<string> summary, string reportPath) Scan(MEGame game, Action<string> reportProgress = null)
        {
            var failures = new Dictionary<string, FailureCategory>();
            var expressionTypeCounts = new Dictionary<string, int>();
            int total = 0;
            int succeeded = 0;

            void Fail(string category, string example)
            {
                if (!failures.TryGetValue(category, out FailureCategory failure))
                {
                    failures[category] = failure = new FailureCategory();
                }
                failure.Count++;
                if (failure.Examples.Count < 10)
                {
                    failure.Examples.Add(example);
                }
            }

            //first pass: parse every shader map, and evaluate its uniform expressions
            var candidates = new List<(string name, Guid[] shaderGuids)>();
            var lightingPathCounts = new Dictionary<string, int>();
            var uniformContext = new UniformExpressionRenderContext([], [], 0, 0, (_, _) => LinearColor.Black, (_, _) => LinearColor.Black);
            foreach ((StaticParameterSet sps, MaterialShaderMap msm, Exception error) in RefShaderCacheReader.EnumerateMaterialShaderMaps(game))
            {
                total++;
                if (total % 500 == 0)
                {
                    reportProgress?.Invoke($"Scanning {game} RefShaderCache: {total} shader maps");
                }
                if (error is not null)
                {
                    Fail($"MaterialShaderMap failed to parse: {FirstLine(error.Message)}", $"BaseMaterialId {sps?.BaseMaterialId}");
                    continue;
                }
                string name = $"{msm.FriendlyName} (BaseMaterialId {sps?.BaseMaterialId})";

                foreach (MaterialUniformExpression expr in msm.UniformPixelVectorExpressions.Concat(msm.UniformPixelScalarExpressions)
                             .Concat(msm.UniformVertexVectorExpressions).Concat(msm.UniformVertexScalarExpressions)
                             .Concat(msm.Uniform2DTextureExpressions).Concat(msm.UniformCubeTextureExpressions))
                {
                    CountExpressionTypes(expr, expressionTypeCounts);
                }

                string evalError = null;
                foreach (MaterialUniformExpression expr in msm.UniformPixelVectorExpressions.Concat(msm.UniformPixelScalarExpressions)
                             .Concat(msm.UniformVertexVectorExpressions).Concat(msm.UniformVertexScalarExpressions))
                {
                    try
                    {
                        LinearColor val = default;
                        expr.GetNumberValue(uniformContext, ref val);
                    }
                    catch (Exception e)
                    {
                        evalError = $"Uniform expression evaluation failed: {expr.ExpressionType.Instanced}: {FirstLine(e.Message)}";
                        break;
                    }
                }
                if (evalError is not null)
                {
                    Fail(evalError, name);
                    continue;
                }

                MeshShaderMap localVFMap = msm.MeshShaderMaps.FirstOrDefault(m => m.VertexFactoryType.Name == MaterialRenderProxy.VERTEX_FACTORY_TYPE_NAME);
                if (localVFMap is null)
                {
                    string vertexFactories = string.Join(", ", msm.MeshShaderMaps.Select(m => m.VertexFactoryType.Name).Order());
                    Fail($"No {MaterialRenderProxy.VERTEX_FACTORY_TYPE_NAME} shaders. Has: [{vertexFactories}]", name);
                    continue;
                }
                var shaderGuids = new Guid[MaterialRenderProxy.ShaderTypeNames.Length];
                bool anyFound = false;
                for (int j = 0; j < shaderGuids.Length; j++)
                {
                    if (localVFMap.Shaders.TryGetValue(MaterialRenderProxy.ShaderTypeNames[j], out ShaderReference shaderRef))
                    {
                        shaderGuids[j] = shaderRef.Id;
                        anyFound = true;
                    }
                }
                if (!anyFound)
                {
                    Fail("No supported base pass shaders", name);
                    continue;
                }
                candidates.Add((name, shaderGuids));
            }

            //second pass: parse the shaders, and create them on a D3D11 device
            reportProgress?.Invoke($"Reading {candidates.Count} shader maps' shaders");
            int shadersPerMap = MaterialRenderProxy.ShaderTypeNames.Length;
            var guids = new List<Guid>();
            foreach ((_, Guid[] shaderGuids) in candidates)
            {
                guids.AddRange(shaderGuids);
            }
            Shader[] shaders = RefShaderCacheReader.GetShaders(game, guids, out _, out _) ?? new Shader[guids.Count];

            using var device = new D3D11.Device(SharpDX.Direct3D.DriverType.Hardware, D3D11.DeviceCreationFlags.None);
            var vsResults = new Dictionary<Guid, string>();
            var psResults = new Dictionary<Guid, string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                string name = candidates[i].name;
                //the lighting model isn't known from the shader map alone. Lit is tried first, so this matches what the renderer does for lit materials
                (Shader vs, Shader ps) = MaterialRenderProxy.SelectShaders(new ArraySegment<Shader>(shaders, i * shadersPerMap, shadersPerMap), isUnlit: false);

                FVertexFactoryParameterRef? vertexFactoryParameters = vs switch
                {
                    TBasePassVertexShader<FNullPolicy, FNullPolicy> noLightMapVS => noLightMapVS.VertexFactoryParameters,
                    TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy> directionalVS => directionalVS.VertexFactoryParameters,
                    _ => null
                };
                if (vertexFactoryParameters is null)
                {
                    Fail(vs is null ? "Vertex shader could not be read" : $"Vertex shader parsed as unexpected type {vs.GetType().Name}", name);
                    continue;
                }
                if (ps is not (TBasePassPixelShader<FNullPolicy> or TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType> or TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType>))
                {
                    Fail(ps is null ? "Pixel shader could not be read" : $"Pixel shader parsed as unexpected type {ps.GetType().Name}", name);
                    continue;
                }
                if (vertexFactoryParameters.Value.Parameters is not FLocalVertexFactoryShaderParameters)
                {
                    Fail($"Unsupported vertex factory parameters: {vertexFactoryParameters.Value.VertexFactoryType}", name);
                    continue;
                }

                if (!vsResults.TryGetValue(vs.Guid, out string vsError))
                {
                    vsResults[vs.Guid] = vsError = TryCreate(() =>
                    {
                        using var d3dShader = new D3D11.VertexShader(device, vs.ShaderByteCode);
                        using var inputLayout = new D3D11.InputLayout(device, vs.ShaderByteCode, LEVertex.InputElements);
                    });
                }
                if (vsError is not null)
                {
                    Fail($"D3D11 vertex shader/input layout creation failed: {vsError}", name);
                    continue;
                }
                if (!psResults.TryGetValue(ps.Guid, out string psError))
                {
                    psResults[ps.Guid] = psError = TryCreate(() =>
                    {
                        using var d3dShader = new D3D11.PixelShader(device, ps.ShaderByteCode);
                    });
                }
                if (psError is not null)
                {
                    Fail($"D3D11 pixel shader creation failed: {psError}", name);
                    continue;
                }
                succeeded++;
                string lightingPath = ps.ShaderType.Name;
                lightingPathCounts[lightingPath] = lightingPathCounts.GetValueOrDefault(lightingPath) + 1;
            }

            var summary = new List<string>
            {
                $"{game} RefShaderCache: {total} MaterialShaderMaps",
                $"Renderable with game shaders: {succeeded} ({(total > 0 ? 100.0 * succeeded / total : 0):F1}%)",
                ""
            };
            summary.AddRange(lightingPathCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => $"  rendered with {kvp.Key}: {kvp.Value}"));
            summary.Add("");
            summary.AddRange(failures.OrderByDescending(kvp => kvp.Value.Count).Select(kvp => $"{kvp.Value.Count}: {kvp.Key}"));

            var report = new StringBuilder();
            foreach (string line in summary)
            {
                report.AppendLine(line);
            }
            report.AppendLine();
            report.AppendLine("Example base materials per failure:");
            foreach ((string category, FailureCategory failure) in failures.OrderByDescending(kvp => kvp.Value.Count))
            {
                report.AppendLine($"  {category}");
                foreach (string example in failure.Examples)
                {
                    report.AppendLine($"    {example}");
                }
            }
            report.AppendLine();
            report.AppendLine("Uniform expression types (count of occurrences, including nested):");
            foreach ((string type, int count) in expressionTypeCounts.OrderByDescending(kvp => kvp.Value))
            {
                report.AppendLine($"  {count}: {type}");
            }

            string reportPath = Path.Combine(Path.GetTempPath(), $"{game}_GameShaderRendererScan.txt");
            File.WriteAllText(reportPath, report.ToString());
            return (summary, reportPath);
        }

        private static void CountExpressionTypes(MaterialUniformExpression expr, Dictionary<string, int> counts)
        {
            if (expr is null) return;
            string type = expr.ExpressionType.Instanced;
            counts[type] = counts.GetValueOrDefault(type) + 1;
            switch (expr)
            {
                case MaterialUniformExpressionUnaryOp unary:
                    CountExpressionTypes(unary.X, counts);
                    break;
                case MaterialUniformExpressionBinaryOp binary:
                    CountExpressionTypes(binary.A, counts);
                    CountExpressionTypes(binary.B, counts);
                    break;
                case MaterialUniformExpressionClamp clamp:
                    CountExpressionTypes(clamp.Input, counts);
                    CountExpressionTypes(clamp.Min, counts);
                    CountExpressionTypes(clamp.Max, counts);
                    break;
            }
        }

        private static string TryCreate(Action create)
        {
            try
            {
                create();
                return null;
            }
            catch (Exception e)
            {
                return FirstLine(e.Message);
            }
        }

        private static string FirstLine(string s) => s?.Split('\n')[0].Trim();
    }
}
