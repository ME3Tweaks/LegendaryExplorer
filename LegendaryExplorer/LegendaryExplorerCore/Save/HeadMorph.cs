using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace LegendaryExplorerCore.Save
{
    public class HeadMorph
    {
        public string HairMesh { get; set; }
        public List<string> AccessoryMeshes { get; set; }
        public Dictionary<string, float> MorphFeatures { get; set; }
        public Dictionary<string, Vector3> OffsetBones { get; set; }
        public List<Vector3> Lod0Vertices { get; set; }
        public List<Vector3> Lod1Vertices { get; set; }
        public List<Vector3> Lod2Vertices { get; set; }
        public List<Vector3> Lod3Vertices { get; set; }
        public Dictionary<string, float> ScalarParameters { get; set; }
        public Dictionary<string, LinearColor> VectorParameters { get; set; }
        public Dictionary<string, string> TextureParameters { get; set; }

        public static HeadMorph FromRonFile(string ronFilePath)
        {
            HeadMorph head = new()
            {
                AccessoryMeshes = [],
                MorphFeatures = [],
                OffsetBones = [],
                ScalarParameters = [],
                VectorParameters = [],
                TextureParameters = [],
                Lod0Vertices = [],
                Lod1Vertices = [],
                Lod2Vertices = [],
                Lod3Vertices = [],
            };

            var lines = File.ReadAllLines(ronFilePath);
            var sectionIDs = new[]
            {
                @"accessory_mesh", @"morph_features", @"offset_bones", @"lod0_vertices", @"lod1_vertices",
                @"lod2_vertices",
                @"lod3_vertices", @"scalar_parameters", @"vector_parameters", @"texture_parameters"
            };

            string parsingSection = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0 || i == lines.Length - 1)
                    continue; // trash
                var line = lines[i];
                var keyValSplit = line.Split(':');

                // TODO this is super brittle and I want to do it better
                if (i == 1)
                {
                    head.HairMesh = keyValSplit[1].Trim().Trim(',', '"');
                    continue;
                }

                bool cont = false;
                foreach (var si in sectionIDs)
                {
                    if (line.Contains(si))
                    {
                        parsingSection = si;
                        cont = true;
                        break;
                    }
                }

                if (cont)
                    continue;

                switch (parsingSection)
                {
                    case "accessory_mesh":
                        {
                            head.AccessoryMeshes.Add(line.Trim().Trim(',', '"'));
                        }
                        break;
                    case "morph_features":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = GetKeyedScalar(keyValSplit);
                            head.MorphFeatures.Add(scalar.Key, scalar.Value);
                        }
                        break;
                    case "offset_bones":
                        {
                            while (!line.Contains('}'))
                            {
                                // Read 4 lines
                                var boneName = lines[i].Split(':')[0].Trim().Trim('"');
                                Vector3 v = new()
                                {
                                    X = GetKeyedScalar(lines[i + 1].Split(':')).Value,
                                    Y = GetKeyedScalar(lines[i + 2].Split(':')).Value,
                                    Z = GetKeyedScalar(lines[i + 3].Split(':')).Value
                                };

                                head.OffsetBones.Add(boneName, v);

                                i += 5; //skip )
                                line = lines[i];
                            }
                        }
                        break;
                    case "lod0_vertices":
                        ReadVertices(head.Lod0Vertices, lines, ref i);
                        break;
                    case "lod1_vertices":
                        ReadVertices(head.Lod1Vertices, lines, ref i);
                        break;
                    case "lod2_vertices":
                        ReadVertices(head.Lod2Vertices, lines, ref i);
                        break;
                    case "lod3_vertices":
                        ReadVertices(head.Lod3Vertices, lines, ref i);
                        break;
                    case "scalar_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = GetKeyedScalar(keyValSplit);
                            head.ScalarParameters.Add(scalar.Key, scalar.Value);
                        }
                        break;
                    case "vector_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var vector = GetKeyedVector(keyValSplit);
                            head.VectorParameters.Add(vector.Key, vector.Value);
                        }
                        break;
                    case "texture_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = GetKeyedString(keyValSplit);
                            head.TextureParameters.Add(scalar.Key, scalar.Value);
                        }
                        break;
                }
            }

            return head;
        }

        private static KeyValuePair<string, float> GetKeyedScalar(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var off = float.Parse(keyValSplit[1].Trim().Trim(','), CultureInfo.InvariantCulture);
            return new KeyValuePair<string, float>(fn, off);
        }

        private static KeyValuePair<string, LinearColor> GetKeyedVector(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var vectStr = keyValSplit[1].Trim().Trim('(', ')', ',').Split(',');
            return new KeyValuePair<string, LinearColor>(fn, new LinearColor()
            {
                R = float.Parse(vectStr[0], CultureInfo.InvariantCulture),
                G = float.Parse(vectStr[1], CultureInfo.InvariantCulture),
                B = float.Parse(vectStr[2], CultureInfo.InvariantCulture),
                A = float.Parse(vectStr[3], CultureInfo.InvariantCulture),
            });
        }

        private static KeyValuePair<string, string> GetKeyedString(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var off = keyValSplit[1].Trim().Trim(',').Trim('"');
            return new KeyValuePair<string, string>(fn, off);
        }

        private static void ReadVertices(List<Vector3> vertices, string[] lines, ref int i)
        {
            var line = lines[i];
            while (!line.Contains("]"))
            {
                vertices.Add(new Vector3
                {
                    X = GetKeyedScalar(lines[i + 1].Split(':')).Value,
                    Y = GetKeyedScalar(lines[i + 2].Split(':')).Value,
                    Z = GetKeyedScalar(lines[i + 3].Split(':')).Value
                });

                i += 5; //skip )
                line = lines[i];
            }
        }

        public void ToRonFile(string file)
        {
            StringBuilder sb = new StringBuilder();

            // starting line opening a struct
            sb.AppendLine("(");

            // hair mesh
            if (HairMesh == null)
            {
                sb.AppendLine("    hair_mesh: \"None\",");
            }
            else
            {
                sb.AppendLine($"    hair_mesh: \"{HairMesh}\",");
            }

            // accessory meshes
            if (AccessoryMeshes == null || AccessoryMeshes.Count == 0)
            {
                sb.AppendLine("    accessory_mesh: [],");
            }
            else
            {
                sb.AppendLine("    accessory_mesh: [");
                foreach (var accessory in AccessoryMeshes)
                {
                    // TODO
                }
                sb.AppendLine("    ],");
            }

            // morph features
            if (MorphFeatures == null || MorphFeatures.Count == 0)
            {
                sb.AppendLine("    morph_features: {},");
            }
            else
            {
                sb.AppendLine("    morph_features: {");
                foreach (var morph in MorphFeatures)
                {
                    sb.AppendLine($"        \"{morph.Key}\": {morph.Value},");
                }
                sb.AppendLine("    },");
            }

            // offset bones
            if (OffsetBones == null || OffsetBones.Count == 0)
            {
                sb.AppendLine("    offset_bones: {},");
            }
            else
            {
                sb.AppendLine("    offset_bones: {");
                foreach (var bone in OffsetBones)
                {
                    sb.AppendLine($"        \"{bone.Key}\": (");
                    sb.AppendLine($"            x: {bone.Value.X},");
                    sb.AppendLine($"            y: {bone.Value.Y},");
                    sb.AppendLine($"            z: {bone.Value.Z},");
                    sb.AppendLine("        ),");
                }
                sb.AppendLine("    },");
            }

            // vertices
            OuptutVertices(sb, Lod0Vertices, "0");
            OuptutVertices(sb, Lod1Vertices, "1");
            OuptutVertices(sb, Lod2Vertices, "2");
            OuptutVertices(sb, Lod3Vertices, "3");

            // scalars
            if (ScalarParameters == null || ScalarParameters.Count == 0)
            {
                sb.AppendLine("    scalar_parameters: {},");
            }
            else
            {
                sb.AppendLine("    scalar_parameters: {");
                foreach (var scalar in ScalarParameters)
                {
                    sb.AppendLine($"        \"{scalar.Key}\": {scalar.Value},");
                }
                sb.AppendLine("    },");
            }

            // vectors
            if (VectorParameters == null || VectorParameters.Count == 0)
            {
                sb.AppendLine("    vector_parameters: {},");
            }
            else
            {
                sb.AppendLine("    vector_parameters: {");
                foreach (var vector in VectorParameters)
                {
                    sb.AppendLine($"        \"{vector.Key}\": ({vector.Value.R}, {vector.Value.G}, {vector.Value.B}, {vector.Value.A}),");
                }
                sb.AppendLine("    },");
            }

            // textures
            if (TextureParameters == null || TextureParameters.Count == 0)
            {
                sb.AppendLine("    texture_parameters: {},");
            }
            else
            {
                sb.AppendLine("    texture_parameters: {");
                foreach (var texture in TextureParameters)
                {
                    sb.AppendLine($"        \"{texture.Key}\": \"{texture.Value}\",");
                }
                sb.AppendLine("    },");
            }

            // close the struct at the end of the file
            sb.AppendLine(")");

            File.WriteAllText(file, sb.ToString());
        }

        private static void OuptutVertices(StringBuilder sb, List<Vector3> lod, string lodNumber)
        {
            if (lod == null || lod.Count == 0)
            {
                sb.AppendLine($"    lod{lodNumber}_vertices: [],");
            }
            else
            {
                sb.AppendLine($"    lod{lodNumber}_vertices: [");
                for (var i = 0; i < lod.Count; i++)
                {
                    {
                        sb.AppendLine("        (");
                        sb.AppendLine($"            x: {lod[i].X},");
                        sb.AppendLine($"            y: {lod[i].Y},");
                        sb.AppendLine($"            z: {lod[i].Z},");
                        sb.AppendLine($"        ),// [{i}]");
                    }
                }
                sb.AppendLine("    ],");
            }
        }
    }
}
