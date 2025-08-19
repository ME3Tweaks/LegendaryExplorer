using CommunityToolkit.HighPerformance;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc.ExperimentsTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Save;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;
using static LegendaryExplorerCore.Packages.CloningImportingAndRelinking.EntryImporter;
using static LegendaryExplorerCore.Unreal.PSA;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    static internal class PackageEditorExperimentsSquid
    {
        public static void ImportAnimSet(PackageEditorWindow pew)
        {
            if(GetPsaFromFile(pew, out var psa, out var filePath))
            {
                var name = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_");

                // first, create a new package export
                var pkg = ExportCreator.CreatePackageExport(pew.Pcc, pew.Pcc.GetNextIndexedName(name + "_ex_a"));

                // create an animSet, animSetData, and a list of sequences, one per animation in the psa
                var animSet = ExportCreator.CreateExport(pew.Pcc, name, "AnimSet", pkg, indexed: false);
                var animSetData = ExportCreator.CreateExport(pew.Pcc, name + "_BioAnimSetData", "BioAnimSetData", pkg, indexed: false);

                animSet.WriteProperty(new ObjectProperty(animSetData, "m_pBioAnimSetData"));

                animSetData.WriteProperty(new ArrayProperty<NameProperty>(psa.Bones.Select(x => new NameProperty(x.Name)), "TrackBoneNames"));

                // not sure how important this is
                animSetData.WriteProperty(new ArrayProperty<NameProperty>([new NameProperty("Root"), new NameProperty("Prop01"), new NameProperty("Prop02")], "UseTranslationBoneNames"));

                var animSequences = psa.GetAnimSequences();

                if (animSequences.IsEmpty())
                {
                    ShowError("this PSA contains no sequences.");
                    return;
                }

                List<ExportEntry> sequenceExports = [];
                foreach (AnimSequence seq in animSequences)
                {
                    var seqExp = ExportCreator.CreateExport(pew.Pcc, NameReference.FromInstancedString(seq.Name), "AnimSequence", pkg, indexed: false);
                    var props = seqExp.GetProperties();
                    // the compression format does not matter much, so we will use the one most comonly used by vanilla meshes which is the smallest one
                    seq.UpdateProps(props, pew.Pcc.Game, AnimationCompressionFormat.ACF_BioFixed48, forceUpdate: true);
                    props.AddOrReplaceProp(new ObjectProperty(animSetData, "m_pBioAnimSetData"));
                    seqExp.WriteProperties(props);
                    seqExp.WriteBinary(seq);
                    sequenceExports.Add(seqExp);
                }

                animSet.WriteProperty(new ArrayProperty<ObjectProperty>(sequenceExports.Select(x => (ObjectProperty)x), "Sequences"));

                // copy the footstep/sound/event notifies from the selected set onto this one
                if (GetSelectedItem(pew, "AnimSet", out var selectedAnimSet))
                {
                    var originalSequences = selectedAnimSet.GetProperty<ArrayProperty<ObjectProperty>>("Sequences").Select(x => x.ResolveToExport(pew.Pcc, new PackageCache()));
                    foreach (var seq in sequenceExports)
                    {
                        var seqName = seq.GetProperty<NameProperty>("SequenceName").Value;
                        var correspondingSequence = originalSequences.FirstOrDefault(x => x.GetProperty<NameProperty>("SequenceName").Value == seqName);
                        if (correspondingSequence == null)
                        {
                            continue;
                        }
                        var originalNotifies = correspondingSequence.GetProperty<ArrayProperty<StructProperty>>("Notifies");
                        if (originalNotifies == null)
                        {
                            continue;
                        }
                        var notifies = new ArrayProperty<StructProperty>("Notifies");
                        foreach (var notify in originalNotifies)
                        {
                            var newEntry = EntryCloner.CloneTree(notify.GetProp<ObjectProperty>("Notify").ResolveToEntry(pew.Pcc), false);
                            newEntry.Parent = seq;
                            var newNotify = notify.DeepClone();
                            newNotify.Properties.AddOrReplaceProp(new ObjectProperty(newEntry, "Notify"));
                            notifies.Add(newNotify);
                        }
                        seq.WriteProperty(notifies);
                    }
                }
            }
        }

        public static void ImportPskAsNewMesh(PackageEditorWindow pew)
        {
            if (pew.Pcc.Game == MEGame.ME1)
            {
                ShowError("This experiment does not yet support OT1; if you must do this, import it into another game and port it to OT1");
            }
            if (pew.Pcc.Game == MEGame.UDK)
            {
                ShowError("This experiment does not support UDK files;");
            }
            if (GetPskFromFile(pew, out var psk, out var path))
            {
                if (!psk.Bones.Any())
                {
                    throw new NotImplementedException("You can't make a static mesh yet");
                }

                var meshExport = ExportCreator.CreateExport(pew.Pcc, Path.GetFileNameWithoutExtension(path), "SkeletalMesh");
                var meshBin = SkeletalMesh.Create();

                SetupSkeleton(psk, meshBin);
                SetupBounds(psk, meshBin);
                SetupMaterials(pew, psk, meshBin);
                CalculateNormalsIfNeeded(psk);

                GetAllVertices(psk, out List<TempVertex> vertsInWedgeOrder, out TempVertex[] finalVerts);
                CalcualteTangents(psk, vertsInWedgeOrder);

                StaticLODModel LOD;
                List<MeshChunk> chunks;
                SetupSectionsAndChunks(psk, meshBin, vertsInWedgeOrder, finalVerts, out LOD, out chunks);

                #region the rest of the LOD data
                LOD.ActiveBoneIndices = [.. Enumerable.Range(0, psk.Bones.Count).Select(x => (ushort)x)];

                // finally, write out the vertex data!
                LOD.NumVertices = (uint)finalVerts.Length;

                LOD.VertexBufferGPUSkin = new SkeletalMeshVertexBuffer
                {
                    VertexData = new GPUSkinVertex[finalVerts.Length],
                    MeshExtension = new Vector3(1, 1, 1)
                };

                for (int chunkIndex = 0; chunkIndex < LOD.Chunks.Length; chunkIndex++)
                {
                    var LODChunk = LOD.Chunks[chunkIndex];
                    var chunk = chunks[chunkIndex];
                    for (var i = chunk.VertIndexStart; i <= chunk.VertIndexEnd; i++)
                    {
                        var tempVert = finalVerts[i];
                        var newVert = new GPUSkinVertex
                        {
                            UV = new Vector2DHalf(tempVert.U, tempVert.V),
                            Position = tempVert.Position with { Y = tempVert.Position.Y * -1 }
                        };

                        var vertNorm = tempVert.Normal with { Y = -tempVert.Normal.Y };
                        var packedNorm = (PackedNormal)Vector3.Normalize(vertNorm);
                        // the w component of the normal is stores the bitangent sign, indicating whether the UV mapping is mirorred here
                        var normalW = tempVert.BiTangentSign > 0 ? (byte)255 : (byte)0;
                        newVert.TangentZ = new PackedNormal(packedNorm.X, packedNorm.Y, packedNorm.Z, normalW);

                        var vertTangent = tempVert.Tangent with { Y = -tempVert.Tangent.Y };
                        var packedTangent = (PackedNormal)Vector3.Normalize(vertTangent);
                        newVert.TangentX = packedTangent;

                        // add in the bone influences
                        var newBoneInfluenceIndices = new byte[4];
                        var newBoneInfluenceWeights = new byte[4];
                        var influences = tempVert.Weights.OrderByDescending(x => x.Weight).ToArray();
                        // sum up all the influences so we can normalize them on import
                        var sum = influences.Select(x => x.Weight).Sum();
                        for (int j = 0; j < 4 && j < influences.Length; j++)
                        {
                            var influence = influences[j];

                            var boneName = psk.Bones[influence.Bone].Name;
                            var meshBoneIndex = meshBin.RefSkeleton.FindIndex(x => x.Name == boneName);
                            var mappedBoneIndex = LODChunk.BoneMap.IndexOf((ushort)meshBoneIndex);
                            newBoneInfluenceIndices[j] = (byte)mappedBoneIndex;
                            // normalize, convert to a byte with 0 being none and 255 being full
                            newBoneInfluenceWeights[j] = (byte)Math.Round(influence.Weight * 255f / sum);
                        }
                        newVert.InfluenceBones = new Influences(newBoneInfluenceIndices[0], newBoneInfluenceIndices[1], newBoneInfluenceIndices[2], newBoneInfluenceIndices[3]);
                        newVert.InfluenceWeights = new Influences(newBoneInfluenceWeights[0], newBoneInfluenceWeights[1], newBoneInfluenceWeights[2], newBoneInfluenceWeights[3]);

                        LOD.VertexBufferGPUSkin.VertexData[i] = newVert;
                    }
                }

                #endregion

                /* things I have not implemented: 
                 * net Index (probably not important unless you are doing ME3MP modding, and you can set it manually easily enough)
                 * Clothing Assets (all null anyway in vanilla)
                 * LOD size (doesn't seem to be important; UDK imports have it set to 0, and I don't know how it is calculated)
                 * PerPolyBoneKDOPS (no idea what this is, it's mostly empty in vanilla)
                 * importing to OT1 (the format is slightly different in ways I don't care to implement), you can probably use debug build to port into OT1 if you must
                 * */

                // just write one LOD. we could extend this to multiple in the future if needed, but no one I know of is actually generating multiple LODs
                meshBin.LODModels = [LOD];

                meshExport.WriteBinary(meshBin);

                // copy the sockets from the selected mesh onto the new one
                if (GetSelectedItem(pew, "SkeletalMesh", out var selectedMesh))
                {
                    var oldSocketsProp = selectedMesh.GetProperty<ArrayProperty<ObjectProperty>>("Sockets");
                    var newSocketsProp = new ArrayProperty<ObjectProperty>("Sockets");
                    foreach (var socket in oldSocketsProp)
                    {
                        var newEntry = EntryCloner.CloneEntry(socket.ResolveToEntry(pew.Pcc), incrementIndex: false);
                        newEntry.Parent = meshExport;
                        newSocketsProp.Add(new ObjectProperty(newEntry));
                    }
                    meshExport.WriteProperty(newSocketsProp);
                }
            }

            static void SetupSkeleton(PSK psk, SkeletalMesh meshBin)
            {
                // set up the skeleton
                // initialize the array to the right size
                meshBin.RefSkeleton = new MeshBone[psk.Bones.Count];
                // keep track of the depth of each bone so we can get the overall skeletal depth
                var skeletalDepth = Enumerable.Repeat(-1, psk.Bones.Count).ToArray();

                int GetDepth(int i)
                {
                    // check if we have already calculated this one
                    if (skeletalDepth[i] != -1)
                    {
                        return skeletalDepth[i];
                    }
                    var parentIndex = psk.Bones[i].ParentIndex;
                    // check for the case that this is the root bone of the skeleton, where it points to itself (usually 0) as its own parent
                    if (parentIndex == -1 || parentIndex == i)
                    {
                        skeletalDepth[i] = 1;
                        return 1;
                    }
                    // next, get the depth of the parent + 1
                    skeletalDepth[i] = GetDepth(parentIndex) + 1;
                    return skeletalDepth[i]; ;
                }
                for (var i = 0; i < psk.Bones.Count; i++)
                {
                    var currentBone = psk.Bones[i];
                    meshBin.NameIndexMap.Add(currentBone.Name, i);
                    meshBin.RefSkeleton[i] = new MeshBone()
                    {
                        Name = currentBone.Name,
                        NumChildren = currentBone.NumChildren,
                        BoneColor = new LegendaryExplorerCore.SharpDX.Color(new Vector4(1, 1, 1, 1)),
                        Flags = currentBone.Flags,
                        ParentIndex = currentBone.ParentIndex,
                        Position = new Vector3(currentBone.Position.X, currentBone.Position.Y * -1, currentBone.Position.Z),
                        Orientation = new Quaternion(currentBone.Rotation.X, currentBone.Rotation.Y * -1, currentBone.Rotation.Z, currentBone.Rotation.W)
                    };

                    // make sure we calculate the depth
                    GetDepth(i);
                }

                // now find the maximum depth and set that as the skeletal depth
                meshBin.SkeletalDepth = skeletalDepth.Max();
            }

            static void SetupBounds(PSK psk, SkeletalMesh meshBin)
            {
                // bounds are important at least for the camera display preview in LEX, and possibly important for when to cull meshes based on visibility in game
                // separate out the coordinates for each axis so we can operate on them
                var xCoords = psk.Points.Select(x => x.X);
                var yCoords = psk.Points.Select(x => -x.Y);
                var zCoords = psk.Points.Select(x => x.Z);

                // get the origin by averaging all vertex positions; it'll probably be close enough
                var origin = new Vector3(xCoords.Average(), yCoords.Average(), zCoords.Average());

                var xRange = xCoords.Select(coord => Math.Abs(coord - origin.X)).Max();
                var yRange = yCoords.Select(coord => Math.Abs(coord - origin.Y)).Max();
                var zRange = zCoords.Select(coord => Math.Abs(coord - origin.Z)).Max();
                var boxExtent = new Vector3(xRange, yRange, zRange);

                var sphereRad = boxExtent.Length();
                meshBin.Bounds = new BoxSphereBounds
                {
                    Origin = origin,
                    // best guess at a reasonable margin
                    BoxExtent = boxExtent * 2,
                    SphereRadius = sphereRad * 2
                };
            }

            static void SetupMaterials(PackageEditorWindow pew, PSK psk, SkeletalMesh meshBin)
            {
                SetNumMaterialSlots(meshBin, psk.Materials.Count);
                for (int i = 0; i < psk.Materials.Count; i++)
                {
                    // Does not work because it is looking for the full instanced path; can I export using that?
                    var entry = pew.Pcc.FindEntry(psk.Materials[i].Name);
                    // a good enough heuristic for now
                    entry ??= pew.Pcc.Exports.FirstOrDefault(x => x.ObjectName == psk.Materials[i].Name && x.ClassName.Contains("Material"));
                    if (entry != null)
                    {
                        meshBin.Materials[i] = entry.UIndex;
                    }
                }
            }

            static void CalculateNormalsIfNeeded(PSK psk)
            {
                // If the normals are not present already, calculate them here by averaging the normals of the faces containing each vertex, weighted by the angle containing that vertex, so as not to introduce artifacts due to triangulation
                if (psk.VertexNormals == null || psk.VertexNormals.Count == 0)
                {
                    // things we need per triangle:
                    // normal vector
                    // point index/angle pairs
                    float GetAngle(Vector3 p0, Vector3 p1, Vector3 p2)
                    {
                        var dot = Vector3.Dot(p1 - p0, p2 - p0);
                        var m1 = Vector3.Distance(p0, p1);
                        var m2 = Vector3.Distance(p0, p2);
                        var temp = dot / (m1 * m2);
                        return (float)Math.Acos(temp);
                    }

                    // need to calculate the normal per face
                    // need to group faces by point index, but with dupes
                    var summedNormals = new Vector3[psk.Points.Count];
                    foreach (var face in psk.Faces)
                    {
                        // point index of each vertex of the triangle
                        var i0 = psk.Wedges[face.WedgeIdx0].PointIndex;
                        var i1 = psk.Wedges[face.WedgeIdx1].PointIndex;
                        var i2 = psk.Wedges[face.WedgeIdx2].PointIndex;
                        // position of each vertex of the triangle
                        var p0 = psk.Points[i0];
                        var p1 = psk.Points[i1];
                        var p2 = psk.Points[i2];

                        // angle (in rad) of each angle of the triangle by point it contains
                        var a0 = GetAngle(p0, p1, p2);
                        var a1 = GetAngle(p1, p0, p2);
                        var a2 = GetAngle(p2, p1, p0);

                        var faceNormal = Vector3.Normalize(Vector3.Cross(p2 - p0, p1 - p0));

                        // accumulate the face normals for each point, weighted by the angle
                        summedNormals[i0] += faceNormal * a0;
                        summedNormals[i1] += faceNormal * a1;
                        summedNormals[i2] += faceNormal * a2;
                    }
                    psk.VertexNormals = [.. summedNormals.Select(x => Vector3.Normalize(x))];
                }
            }

            static void GetAllVertices(PSK psk, out List<TempVertex> vertsInWedgeOrder, out TempVertex[] finalVerts)
            {
                // I need this psk to be set up such that each point corresponds to a wedge, and all are paired like this.
                // So no loose points not assiciated with any triangles, and no points shared across UV/material seams. those points need to be duplicated for each wedge that shares them
                // check if this condition is already met and if so, maintain the point order

                bool preserveOrder;
                // group wedges by point index
                var groups = psk.Wedges.GroupBy(x => x.PointIndex);
                // get the count of each group
                var groupLengths = groups.Select(x => x.Count());
                // make sure none are greater than 1 (would indicate a shared point across a UV/material seam)
                // and the counts are equal (if points was greater this would indicate loose points not corresponding to any wedge)
                if (groupLengths.Max() == 1 && psk.Points.Count == psk.Wedges.Count)
                {
                    preserveOrder = true;
                }
                else
                {
                    preserveOrder = false;
                }

                // the numbers don't match; we need to rebuild these and update the corresponding stuff accordingly
                // wedges reference point index (will need to be updated)
                // triangles reference wedge indices (will need to be updated if we reorded to make materials contiguous, which I think we should do)
                // nevermind on the above, we don't need to reorder wegdes?
                // but we may want to reorder triangles to get nice even sections
                // we can do that even if we maintain vertex order
                // weights reference point, will need to be udpated
                // vertex normals go by points, I think, and will need to be reordered accordingly
                // if we are handling morphs, those reference points and will need to be updated

                var weightsByPoint = psk.Weights.GroupBy(x => x.Point).ToDictionary(g => g.Key, g => g.ToList());
                vertsInWedgeOrder = [];
                for (int i = 0; i < psk.Wedges.Count; i++)
                {
                    var wedge = psk.Wedges[i];
                    var point = psk.Points[wedge.PointIndex];
                    weightsByPoint.TryGetValue(wedge.PointIndex, out var weights);
                    weights ??= [];

                    var normal = psk.VertexNormals[wedge.PointIndex];

                    vertsInWedgeOrder.Add(new TempVertex()
                    {
                        OriginalWedgeIndex = (ushort)i,
                        OriginalPointIndex = wedge.PointIndex,
                        MaterialIndex = wedge.MatIndex,
                        U = wedge.U,
                        V = wedge.V,
                        Position = point,
                        Weights = weights,
                        Normal = normal,
                    });
                }

                // order by point index, then by wedge index implicitly if there are duplicates; this should maintain order if that is important
                IEnumerable<TempVertex> orderedVerts = vertsInWedgeOrder.OrderBy(x => x.OriginalPointIndex);

                // if we don't need to preserve order, order by material so we get contiguous chunks, and the same number as there are materials, like vanilla does it
                if (!preserveOrder)
                {
                    orderedVerts = orderedVerts.GroupBy(x => x.MaterialIndex).OrderBy(x => x.Key).SelectMany(x => x);
                }

                finalVerts = [.. orderedVerts];
                for (var i = 0; i < finalVerts.Length; i++)
                {
                    finalVerts[i].Index = (ushort)i;
                }
            }

            static void CalcualteTangents(PSK psk, List<TempVertex> vertsInWedgeOrder)
            {
                // generate tangents using the MikkTSpace algorithm which is used by most tools these days

                // callback to get vertex positions
                TempVertex GetVert(int face, int vert)
                {
                    var tri = psk.Faces[face];
                    return vert switch
                    {
                        0 => vertsInWedgeOrder[tri.WedgeIdx0],
                        1 => vertsInWedgeOrder[tri.WedgeIdx1],
                        2 => vertsInWedgeOrder[tri.WedgeIdx2],
                        _ => throw new IndexOutOfRangeException()
                    };
                }
                void vertPositionHandler(int face, int vertex, out float x, out float y, out float z)
                {
                    var vert = GetVert(face, vertex);

                    x = vert.Position.X; y = vert.Position.Y; z = vert.Position.Z;
                }
                // callback to get vertex normals
                void VertNormHandler(int face, int vertex, out float x, out float y, out float z)
                {
                    var vert = GetVert(face, vertex);

                    x = vert.Normal.X; y = vert.Normal.Y; z = vert.Normal.Z;
                }
                void VertUVHandler(int face, int vertex, out float u, out float v)
                {
                    var vert = GetVert(face, vertex);

                    u = vert.U; v = vert.V;
                }
                void BasicTangentHandler(int face, int vertex, float x, float y, float z, float sign)
                {
                    var vert = GetVert(face, vertex);

                    // this is needed to store the bitangent sign in the Vertex Normal W component. It is important
                    // it is basically whether the UV mapping at this part of the mesh is mirrored, and everything will look bad if it's not set correctly.
                    vert.BiTangentSign = sign;

                    // this is the tangent vector for this vertex
                    vert.Tangent = new Vector3(x, y, z);
                }
                Mikktspace.NET.MikkGenerator.GenerateTangentSpace(
                    // number of faces
                    psk.Faces.Count,
                    // number of verts per face; the algorithm supports quads, but it will always be triangles in a psk
                    _ => 3,
                    // callbacks to get the position, normal, and UV coordinates of a vertex
                    vertPositionHandler,
                    VertNormHandler,
                    VertUVHandler,
                    // callback to recieve the results: a tangent and BiNormal sign per vertex
                    BasicTangentHandler);
            }

            static void SetupSectionsAndChunks(PSK psk, SkeletalMesh meshBin, List<TempVertex> vertsInWedgeOrder, TempVertex[] finalVerts, out StaticLODModel LOD, out List<MeshChunk> chunks)
            {
                // next, write out the sections and chunks
                // the triangles, grouped by material
                var matGroups = psk.Faces.GroupBy(x => x.MatIndex).OrderBy(x => x.Key);

                LOD = new StaticLODModel
                {
                    // convert to the new point indices and make sure the order is correct to have the right normals (intentionally flipping 1 and 0)
                    IndexBuffer = [.. matGroups.SelectMany(x => x).SelectMany<PSK.PSKTriangle, ushort>(x => [vertsInWedgeOrder[x.WedgeIdx1].Index, vertsInWedgeOrder[x.WedgeIdx0].Index, vertsInWedgeOrder[x.WedgeIdx2].Index])],
                    // TODO filter this down to bones that actually have any weighting?
                    RequiredBones = [.. Enumerable.Range(0, psk.Bones.Count).Select(x => (byte)x)]
                };
                List<MeshSection> sections = [];
                var startIndex = 0;
                foreach (var matGroup in matGroups)
                {
                    var section = new MeshSection
                    {
                        Triangles = [.. matGroup],
                        BaseTriIndex = startIndex,
                        MatIndex = matGroup.Key,
                    };

                    // calculate the min and max vertex indices within this section
                    var sectionIndices = matGroup.SelectMany<PSK.PSKTriangle, ushort>(x => [vertsInWedgeOrder[x.WedgeIdx0].Index, vertsInWedgeOrder[x.WedgeIdx1].Index, vertsInWedgeOrder[x.WedgeIdx2].Index]);
                    section.MinVertIndex = sectionIndices.Min();
                    section.MaxVertIndex = sectionIndices.Max();

                    sections.Add(section);
                    startIndex += matGroup.Count();
                }

                // given this, I then need to make the fewest number of chunks with non overlapping vertex ranges
                // in the best case this means the same number of chunks as sections
                // in the worst case we fold them into a single chunk
                // hypothetically we could split the sections to avoid merging chunks but I haven't tested that and it won't work in all cases

                // first, sort the sections by min vert index then max vert index, so we can enumerate them in that order
                sections = [.. sections.OrderBy(x => x.MinVertIndex).ThenBy(x => x.MaxVertIndex)];
                chunks = [];
                chunks.Add(new MeshChunk
                {
                    VertIndexStart = 0,
                    VertIndexEnd = sections[0].MaxVertIndex,
                    InfluenceBones = []
                });
                foreach (var section in sections)
                {
                    if (section.MinVertIndex > chunks[^1].VertIndexEnd)
                    {
                        // sections have non overlapping vertices; make a new chunk
                        chunks.Add(new MeshChunk
                        {
                            VertIndexStart = section.MinVertIndex,
                            VertIndexEnd = section.MaxVertIndex,
                            InfluenceBones = []
                        });
                    }
                    else
                    {
                        // sections have overlapping vertices and we need to combine the chunks
                        chunks[^1].VertIndexEnd = Math.Max(section.MaxVertIndex, chunks[^1].VertIndexEnd);
                    }
                }

                // now, assign a chunk index to each section
                for (var i = 0; i < sections.Count; i++)
                {
                    sections[i].ChunkIndex = chunks.FindIndex(x => x.VertIndexStart <= sections[i].MinVertIndex && x.VertIndexEnd >= sections[i].MaxVertIndex);
                }

                // next, we need to see which bones influence each chunk
                // as well as count the rigid and soft vertices (not positive if that matters in game or not, but I am trying to emulate vanilla as closely as possible)
                foreach (var chunk in chunks)
                {
                    for (var i = chunk.VertIndexStart; i <= chunk.VertIndexEnd; i++)
                    {
                        var weights = finalVerts[i].Weights;
                        switch (weights.Count)
                        {
                            // TODO is this right?
                            case <= 1:
                                chunk.RigidVerts++;
                                break;
                            case > 4:
                                throw new Exception("there are too many bones influencing this vertex, and I don't know how to handle that.");
                            default:
                                chunk.SoftVerts++;
                                break;
                        }
                        if (weights.Count > chunk.maxBoneInfluences)
                        {
                            chunk.maxBoneInfluences = weights.Count;
                        }
                        foreach (var weight in weights)
                        {
                            chunk.InfluenceBones.Add((ushort)weight.Bone);
                        }
                    }
                    // the indices into the bone mapping array are bytes, so we can't have too many here without splitting the chunk up, which I have not implemented because it is extraorinarily unlikely to come up in real world usage
                    if (chunk.InfluenceBones.Count > 255)
                    {
                        throw new Exception("there are too many influence bones in this chunk; Send the psk to Squid and tell him to implement chunk splitting logic.");
                    }
                }

                ushort GetMeshBoneIndex(ushort pskIndex)
                {
                    var pskBone = psk.Bones[pskIndex];
                    return (ushort)meshBin.RefSkeleton.FindIndex(x => x.Name == pskBone.Name);
                }

                LOD.Sections = [..sections.Select(x => new SkelMeshSection
                {
                    BaseIndex = (uint)(x.BaseTriIndex * 3),
                    ChunkIndex = (ushort)x.ChunkIndex,
                    MaterialIndex = (ushort)x.MatIndex,
                    NumTriangles = x.Triangles.Length
                })];

                LOD.Chunks = [..chunks.Select(x => new SkelMeshChunk
                {
                    BaseVertexIndex = (uint)x.VertIndexStart,
                    MaxBoneInfluences = x.maxBoneInfluences,
                    NumRigidVertices = x.RigidVerts,
                    NumSoftVertices = x.SoftVerts,
                    BoneMap = [.. x.InfluenceBones.Select(GetMeshBoneIndex).Order()]
                })];
            }
        }

        private class TempVertex
        {
            public ushort Index { get; set; }
            public ushort OriginalPointIndex { get; set; }
            public ushort OriginalWedgeIndex { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Normal { get; set; }
            public Vector3 Tangent { get; set; }
            public float U { get; set; }
            public float V { get; set; }
            public byte MaterialIndex { get; set; }
            public List<PSK.PSKWeight> Weights { get; set; }
            public float BiTangentSign { get; set; }
        }

        public static void ExportSelectedToPsx(PackageEditorWindow pew)
        {
            var selectedEntryClass = pew.SelectedItem?.Entry?.ClassName;

            switch (selectedEntryClass)
            {
                case "SkeletalMesh":
                    // export the skeletal mesh as a psk
                    var d = new SaveFileDialog { Filter = "PSKX|*.pskx" };
                    if (d.ShowDialog() == true)
                    {
                        PSK.CreateFromSkeletalMesh(((ExportEntry)pew.SelectedItem.Entry).GetBinaryData<SkeletalMesh>(), 0, true).ToFile(d.FileName);
                    }
                    return;
                case "AnimSet":
                case "BioDynamicAnimSet":
                    ExportAnimSet(pew);
                    return;
                case "animSequence":
                    ExportAnimSequence(pew);
                    return;
                //case "StaticMesh":
                //    ExportStaticMeshToPSKX(pew);
                //    return;
                case "BioMorphFace":
                    BioMorphFaceToPskxAndPsa(pew);
                    return;
                case "MorphTargetSet":
                    ExportMorphTargetSet(pew);
                    return;
                // TODO support StaticMesh, BrushComponent, FracturedStaticMesh, etc. There are a few other mesh like objects it might be nice to be able to edit, but very low priority?
                default:
                    ShowError("You must open a pcc file and select a SkeletalMesh, BioMorphFace, MorphTargetSet, AnimSet, or AnimSequence for this experiment");
                    return;
            }
        }

        private static void ExportAnimSequence(PackageEditorWindow pew)
        {
            if (GetSelectedItem(pew, "AnimSequence", out var animSeqExport))
            {
                var d = new SaveFileDialog { Filter = "PSA|*.psa" };
                if (d.ShowDialog() == true)
                {
                    var psa = PSA.CreateFrom(animSeqExport.GetBinaryData<AnimSequence>());

                    psa.ToFile(d.FileName);
                }
            }
        }

        private static void ExportAnimSet(PackageEditorWindow pew)
        {
            if (GetSelectedItem(pew, ["AnimSet", "BioDynamicAnimSet"], out var animSetExport))
            {
                var d = new SaveFileDialog { Filter = "PSA|*.psa" };
                if (d.ShowDialog() == true)
                {
                    var sequences = animSetExport.GetProperty<ArrayProperty<ObjectProperty>>("Sequences").Select(x => (x.ResolveToExport(pew.Pcc, new PackageCache())).GetBinaryData<AnimSequence>());

                    var psa = PSA.CreateFrom([.. sequences]);

                    psa.ToFile(d.FileName);
                }
            }
        }

        //private static void ExportStaticMeshToPSKX(PackageEditorWindow pew)
        //{
        //    // TODO implement this
        //    throw new NotImplementedException("I haven't implemented exporting static meshes yet.");
        //}

        private static void BioMorphFaceToPskxAndPsa(PackageEditorWindow pew)
        {
            // get the selected bmf and ensure it has a base head mesh
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf) || bmf.GetProperty<ObjectProperty>("m_oBaseHead") == null)
            {
                ShowError("You must select a BioMorphFace with a base head mesh for this command to work");
                return;
            }

            var d = new SaveFileDialog { Filter = "PSKX|*.pskx" };
            if (d.ShowDialog() == true)
            {

                var baseHeadMesh = pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oBaseHead").Value) as ExportEntry;
                var baseMeshBin = baseHeadMesh.GetBinaryData<SkeletalMesh>();

                // make most of the psk from the base head mesh
                var psk = PSK.CreateFromSkeletalMesh(baseHeadMesh.GetBinaryData<SkeletalMesh>(), 0, true);

                var bmfBin = bmf.GetBinaryData<BioMorphFace>();

                for (var i = 0; i < psk.Points.Count && i < bmfBin.LODs[0].Length; i++)
                {
                    // modify each point in the psk with the points from the bmf
                    var bmfPoint = bmfBin.LODs[0][i];
                    psk.Points[i] = bmfPoint with { Y = -bmfPoint.Y };
                }

                psk.ToFile(d.FileName);


                // now, output the psa file and config file
                var config = new StringBuilder();
                config.AppendLine("[RemoveTracks]");
                var psa = new PSA
                {
                    Bones = [],
                    Infos = [],
                    Keys = []
                };

                var bmfSkeleton = bmf.GetProperty<ArrayProperty<StructProperty>>("m_aFinalSkeleton");

                // add the ref skeleton into the thing
                foreach (var bone in baseMeshBin.RefSkeleton)
                {
                    psa.Bones.Add(new PSABone
                    {
                        Name = bone.Name,
                        ParentIndex = bone.ParentIndex,
                    });
                }

                psa.Infos.Add(new PSAAnimInfo
                {
                    Name = "BioMorphFaceFinalSkeleton",
                    Group = "None",
                    TotalBones = baseMeshBin.RefSkeleton.Length,
                    KeyQuotum = baseMeshBin.RefSkeleton.Length, // this would be multiplied by the number of frames, but there is just one frame
                    TrackTime = 1,
                    AnimRate = 1,
                    FirstRawFrame = 0,
                    NumRawFrames = 1
                });

                for (int i = 0; i < baseMeshBin.RefSkeleton.Length; i++)
                {
                    var refBone = baseMeshBin.RefSkeleton[i];

                    // is this bone offset by this BMF?
                    var offset = bmfSkeleton.FirstOrDefault(x => x.GetProp<NameProperty>("nName").Value == refBone.Name);
                    var rotQuat = new Quaternion(0, 0, 0, 1);
                    var posVec = new Vector3(0, 0, 0);
                    if (offset != null)
                    {
                        var pos = offset.GetProp<StructProperty>("vPos");
                        posVec = new Vector3(pos.GetProp<FloatProperty>("X"), -pos.GetProp<FloatProperty>("Y"), pos.GetProp<FloatProperty>("Z"));
                        // do not output rotation when you import this one
                        config.AppendLine($"BioMorphFaceFinalSkeleton.{i}=rot");
                    }
                    else
                    {
                        // do not output anything when you import this one
                        config.AppendLine($"BioMorphFaceFinalSkeleton.{i}=all");
                    }

                    psa.Keys.Add(new PSAAnimKeys
                    {
                        Position = posVec,
                        Rotation = rotQuat,
                        Time = 30
                    });
                }

                psa.ToFile(Path.ChangeExtension(d.FileName, "psa"));

                // also output a config file next to this to tell it to skip rotations for every sequence and every bone, and skip everythig for bones that aren't part of the pose
                File.WriteAllText(Path.ChangeExtension(d.FileName, "config"), config.ToString());
            }
        }

        private static bool GetHeadmorphBaseHead(HeadMorph headmorph, MEGame game, out SkeletalMesh baseHeadMesh, out bool isFemaleMorph, out bool gameMismatch)
        {
            baseHeadMesh = null;
            isFemaleMorph = false;
            gameMismatch = false;
            // not supported, makes no sense
            if (game == MEGame.UDK || game == MEGame.LELauncher)
            {
                gameMismatch = true;
                return false;
            }

            // determine which game (1/2 [identical] or 3) and gender it is
            bool isGame3Morph;
            switch (headmorph.Lod0Vertices.Count)
            {
                case 2232:
                    // this is the ME1/2 HMF
                    isGame3Morph = false;
                    isFemaleMorph = true;
                    break;
                case 2294:
                    // this is the ME1/2 HMH
                    isGame3Morph = false;
                    isFemaleMorph = false;
                    break;
                case 2390:
                    // this is the ME3 HMF
                    isGame3Morph = true;
                    isFemaleMorph = true;
                    break;
                case 2392:
                    // this is the ME3 HMM
                    isGame3Morph = true;
                    isFemaleMorph = false;
                    break;
                default:
                    // unknown game and gender based on vert count
                    return false;
            }

            // if we don't have a specific game we are going for, find a suitable one
            if (game == MEGame.Unknown)
            {
                if (isGame3Morph)
                {
                    if (ME3TweaksBackups.GetGameBackupPath(MEGame.LE3) != null || LE3Directory.DefaultGamePath != null)
                    {
                        game = MEGame.LE3;
                    }
                    else if (ME3TweaksBackups.GetGameBackupPath(MEGame.ME3) != null || ME3Directory.DefaultGamePath != null)
                    {
                        game = MEGame.ME3;
                    }
                    else
                    {
                        // this is an ME3 morph, but neither OT3 or LE3 are intalled or have a backup
                        return false;
                    }
                }
                else
                {
                    // this is a ME1/2 morph; look for a source for the mesh going LE1, LE2, ME2, ME1 in that order
                    if (ME3TweaksBackups.GetGameBackupPath(MEGame.LE1) != null || LE1Directory.DefaultGamePath != null)
                    {
                        game = MEGame.LE1;
                    }
                    else if (ME3TweaksBackups.GetGameBackupPath(MEGame.LE2) != null || LE2Directory.DefaultGamePath != null)
                    {
                        game = MEGame.LE2;
                    }
                    else if (ME3TweaksBackups.GetGameBackupPath(MEGame.ME2) != null || ME2Directory.DefaultGamePath != null)
                    {
                        game = MEGame.ME2;
                    }
                    else if (ME3TweaksBackups.GetGameBackupPath(MEGame.ME1) != null || ME1Directory.DefaultGamePath != null)
                    {
                        game = MEGame.ME1;
                    }
                    else
                    {
                        // this is an ME1/2 morph, but neither OT1/2 or LE1/2 are intalled or have a backup
                        return false;
                    }
                }
            }

            // this will identify an ME3 morph going into ME1/2 or vice versa
            // this is almost certainly a mistake, and if it not, you can do the extra step of putting it in the right game then porting it over
            if (isGame3Morph ^ game.IsGame3())
            {
                gameMismatch = true;
                return false;
            }

            // the path to the file containing the mesh
            string packageFilePath = null;
            // the path to the mesh within the file (consistent in all games)
            string exportPath = isFemaleMorph ? "BIOG_HMF_HED_PROMorph_R.Custom.HMF_HED_PROCustom_MDL" : "BIOG_HMM_HED_PROMorph.Custom.HMM_HED_PROCustom_MDL";
            var basePath = ME3TweaksBackups.GetGameBackupPath(game) ?? MEDirectories.GetDefaultGamePath(game);
            switch (game)
            {
                case MEGame.ME1:
                    packageFilePath = Path.Combine(basePath, $"BioGame\\CookedPC\\Maps\\EntryMenu.SFM");
                    break;
                case MEGame.ME2:
                case MEGame.LE1:
                case MEGame.LE2:
                    // identical for LE1/2
                    packageFilePath = Path.Combine(basePath, $"BioGame\\{MEDirectories.CookedName(game)}\\BIOG_MORPH_FACE.pcc");
                    break;
                case MEGame.ME3:
                case MEGame.LE3:
                    // identical for ME3 and LE3
                    packageFilePath = Path.Combine(basePath, $"BioGame\\CookedPCConsole\\BioP_Char.pcc");
                    break;
            }

            // TODO unsafe partial load for performance?
            var proMorphFile = MEPackageHandler.OpenMEPackage(packageFilePath);
            baseHeadMesh = proMorphFile.FindExport(exportPath).GetBinaryData<SkeletalMesh>();
            return true;
        }

        public static void RonFileToPskx(PackageEditorWindow _)
        {
            // first, get the ron file imported
            if (GetHeadmorphFromFile(out var headmorph, out var _))
            {
                if (!GetHeadmorphBaseHead(headmorph, MEGame.Unknown, out var baseHeadMesh, out var _, out var _))
                {
                    // TODO check if there is a head in the accessory meshes from AMM LE3?
                    ShowError("unable to find base head; please convert to BioMorphFace and apply the base head then export that instead");
                    return;
                }

                var psk = PSK.CreateFromSkeletalMesh(baseHeadMesh, includeVertexNormals: true);

                // update the vertex positions:
                for (int i = 0; i < headmorph.Lod0Vertices.Count; i++)
                {
                    psk.Points[i] = headmorph.Lod0Vertices[i] with { Y = -headmorph.Lod0Vertices[i].Y };
                }

                // get an output for this file
                var d = new SaveFileDialog { Filter = "PSKX|*.pskx" };
                if (d.ShowDialog() != true)
                {
                    return;
                }

                psk.ToFile(d.FileName);

                // now, output the bone offsets as a psa file
                var config = new StringBuilder();
                config.AppendLine("[RemoveTracks]");
                var psa = new PSA
                {
                    Bones = [],
                    Infos = [],
                    Keys = []
                };

                // add the ref skeleton into the thing
                foreach (var bone in baseHeadMesh.RefSkeleton)
                {
                    psa.Bones.Add(new PSABone
                    {
                        Name = bone.Name,
                        ParentIndex = bone.ParentIndex,
                    });
                }

                psa.Infos.Add(new PSAAnimInfo
                {
                    Name = "BoneOffsets",
                    Group = "None",
                    TotalBones = baseHeadMesh.RefSkeleton.Length,
                    KeyQuotum = baseHeadMesh.RefSkeleton.Length, // this would be multiplied by the number of frames, but there is just one frame
                    TrackTime = 1,
                    AnimRate = 1,
                    FirstRawFrame = 0,
                    NumRawFrames = 1
                });

                for (int i = 0; i < baseHeadMesh.RefSkeleton.Length; i++)
                {
                    var refBone = baseHeadMesh.RefSkeleton[i];

                    // is this bone offset by this headmorph
                    var rotQuat = new Quaternion(0, 0, 0, 1);
                    var posVec = new Vector3(0, 0, 0);
                    if (headmorph.OffsetBones.TryGetValue(refBone.Name, out var offset))
                    {
                        // do not output rotation when you import this one
                        config.AppendLine($"BoneOffsets.{i}=rot");
                        posVec = offset with { Y = -offset.Y };
                    }
                    else
                    {
                        // do not output anything when you import this one
                        config.AppendLine($"BoneOffsets.{i}=all");
                    }

                    psa.Keys.Add(new PSAAnimKeys
                    {
                        Position = posVec,
                        Rotation = rotQuat,
                        Time = 30
                    });
                }

                psa.ToFile(Path.ChangeExtension(d.FileName, "psa"));

                // also output a config file next to this to tell it to skip rotations for every sequence and every bone, and skip everythig for bones that aren't part of the pose
                File.WriteAllText(Path.ChangeExtension(d.FileName, "config"), config.ToString());
            }
        }

        public static void MakeCustomMorphTargetSet(PackageEditorWindow pew)
        {
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return; }

            if (!(pew.SelectedItem.Entry.ClassName == "MorphTargetSet" || pew.SelectedItem.Entry.ClassName == "SkeletalMesh"))
            {
                ShowError("Selected item is not a MorphTargetSet or SkeletalMesh");
                return;
            }

            var SelectedExport = (ExportEntry)pew.SelectedItem.Entry;
            ExportEntry morphTargetSet = null;
            ExportEntry headMesh;

            if (SelectedExport.ClassName == "MorphTargetSet")
            {
                morphTargetSet = SelectedExport;
                headMesh = (ExportEntry)morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").ResolveToEntry(pew.Pcc);
            }
            else
            {
                headMesh = SelectedExport;
            }

            EnsureParentClassExists(pew);
            var newClass = CreateCustomMorphTargetSet(pew, morphTargetSet, headMesh);
            pew.GoToNumber(newClass.UIndex);
        }

        private static ExportEntry CreateCustomMorphTargetSet(PackageEditorWindow pew, ExportEntry morphTargetSet, ExportEntry headMesh)
        {
            var sb = new StringBuilder();

            var className = morphTargetSet == null ? headMesh.ObjectName : morphTargetSet.ObjectName;

            sb.AppendLine($"Class {className} extends CustomMorphTargetSet config(game);");
            sb.AppendLine("defaultproperties {");
            sb.AppendLine(HandleSkeletalMesh(pew, headMesh));
            if (morphTargetSet != null)
            {
                sb.AppendLine(HandleVanillaMorphTargetSet(pew, morphTargetSet));
            }
            sb.AppendLine("}");

            return MakeNewClass(pew, null, sb.ToString(), className);
        }

        private static string HandleVanillaMorphTargetSet(PackageEditorWindow pew, ExportEntry morphTargetSet)
        {
            var sb = new StringBuilder();

            var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");

            sb.AppendLine("\tBaseMorphTargets = (");
            for (int k = 0; k < targets.Count; k++)
            {
                var target = targets[k];
                var expEntryTarget = (ExportEntry)target.ResolveToEntry(pew.Pcc);
                // get the binary data from the export
                var targetBinary = expEntryTarget.GetBinaryData<MorphTarget>();

                // add the bone offsets from this target
                sb.AppendLine($"\t\t{{TargetName = '{expEntryTarget.ObjectNameString}',");

                sb.Append("\t\t\tBoneOffsets=(");
                for (int i = 0; i < targetBinary.BoneOffsets.Length; i++)
                {
                    var boneOffset = targetBinary.BoneOffsets[i];
                    sb.Append($"{{Bone = '{boneOffset.Bone}',Offset = {{X = {boneOffset.Offset.X:F8}, Y = {boneOffset.Offset.Y:F8}, Z = {boneOffset.Offset.Z:F8}}}}}{(i < targetBinary.BoneOffsets.Length - 1 ? "," : "")}");
                }
                sb.AppendLine("),");

                sb.Append("\t\t\tLodModels = (");
                for (int i = 0; i < targetBinary.MorphLODModels.Length; i++)
                {
                    var lodModel = targetBinary.MorphLODModels[i];
                    sb.Append($"{{NumBaseMeshVertices={lodModel.NumBaseMeshVerts},vertices = (");

                    for (int j = 0; j < lodModel.Vertices.Length; j++)
                    {
                        var vert = lodModel.Vertices[j];
                        sb.Append($"{{sourceIndex = {vert.SourceIdx},PositionDelta = {{X = {vert.PositionDelta.X:F8}, Y = {vert.PositionDelta.Y:F8}, Z = {vert.PositionDelta.Z:F8}}}}}{(j < lodModel.Vertices.Length - 1 ? "," : "")}");
                    }
                    sb.Append($")}}{(i < targetBinary.MorphLODModels.Length - 1 ? "," : "")}");
                }
                sb.Append(")");

                sb.AppendLine().AppendLine($"\t\t}}{(k < targets.Count - 1 ? "," : "")}");
            }

            // close targets
            sb.AppendLine("\t)");

            return sb.ToString();
        }

        private static ExportEntry GetOrCreatePackageFolder(PackageEditorWindow pew, string packageName)
        {
            var folder = pew.Pcc.FindExport(packageName);

            if (folder == null)
            {
                IEntry packageClass = pew.Pcc.GetEntryOrAddImport("Core.Package", "Class", "Core");
                folder = new ExportEntry(pew.Pcc, 0, packageName)
                {
                    Class = packageClass
                };
                pew.Pcc.AddExport(folder);
                folder = pew.Pcc.FindExport(packageName);
            }

            return folder;
        }

        private static ExportEntry CreateBioMorphFace(PackageEditorWindow pew, string objectName)
        {
            IEntry BioMorphFaceClass = pew.Pcc.GetEntryOrAddImport("SFXGame.BioMorphFace", "Class", "Core");
            var morphFace = new ExportEntry(pew.Pcc, 0, objectName)
            {
                Class = BioMorphFaceClass
            };
            pew.Pcc.AddExport(morphFace);
            morphFace = pew.Pcc.FindExport(objectName);

            return morphFace;
        }

        private static ExportEntry EnsureParentClassExists(PackageEditorWindow pew)
        {
            const string ParentClassText = @"Class CustomMorphTargetSet
    config(game);

// Types
struct BoneOffset 
{
    var Name Bone;
    var Vector Offset;
};
struct CustomMorphTarget 
{
    struct VertexOffset 
    {
        var int sourceIndex;
        var Vector PositionDelta;
    };
    struct LodModel 
    {
        var int NumBaseMeshVertices;
        var array<VertexOffset> vertices;
        
        structdefaultproperties
        {
            vertices = ()
        }
    };
    var array<BoneOffset> BoneOffsets;
    var array<LodModel> LodModels;
    var Name TargetName;
    
    structdefaultproperties
    {
        BoneOffsets = ()
        LodModels = ()
    }
};
struct MeshVertices 
{
    var array<Vector> vertices;
    
    structdefaultproperties
    {
        vertices = ()
    }
};

// Variables
var array<CustomMorphTarget> BaseMorphTargets;
var config array<CustomMorphTarget> CustomMorphTargets;
var array<BoneOffset> OriginalMeshBoneOffsets;
var array<MeshVertices> OriginalMeshLodModels;

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}";
            const string ParentClassPackage = "MeshTools";
            const string ParentClassName = "CustomMorphTargetSet";

            var parentClass = pew.Pcc.FindExport($"{ParentClassPackage}.{ParentClassName}");

            if (parentClass != null)
            {
                return parentClass;
            }

            var parentFolder = GetOrCreatePackageFolder(pew, ParentClassPackage);

            return MakeNewClass(pew, parentFolder, ParentClassText, ParentClassName);
        }

        private static ExportEntry MakeNewClass(PackageEditorWindow pew, IEntry parent, string classText, string className)
        {
            var usop = new UnrealScriptOptionsPackage();
            var fileLib = new FileLib(pew.Pcc);
            if (!fileLib.Initialize(usop))
            {
                var dlg = new ListDialog(fileLib.InitializationLog.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not build script database for this file!", pew);
                dlg.Show();
                throw new Exception("fileLib failed to initialize");
            }
            (_, MessageLog log) = UnrealScriptCompiler.CompileClass(pew.Pcc, classText, fileLib, usop, parent: parent);
            if (log.HasErrors)
            {
                var dlg = new ListDialog(log.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not create class!", pew);
                dlg.Show();
                throw new Exception("class failed to compile");
            }

            string fullPath = parent is null ? className : $"{parent.InstancedFullPath}.{className}";
            return (ExportEntry)pew.Pcc.FindEntry(fullPath);
        }

        private static string HandleSkeletalMesh(PackageEditorWindow pew, ExportEntry headMesh)
        {
            var meshBinary = headMesh.GetBinaryData<SkeletalMesh>();
            var morphHeadBinary = new LegendaryExplorerCore.Unreal.BinaryConverters.BioMorphFace();
            var morphHeadProps = new PropertyCollection();
            var morphHeadSkeleton = new ArrayProperty<StructProperty>("m_aFinalSkeleton");

            morphHeadProps.Add(new ObjectProperty(headMesh, "m_oBaseHead"));

            var MorphHeadExcludeBones = new List<string> { "God", "Root", "LowerBack", "Chest", "Chest1", "Chest2", "Neck", "Neck1", "Head", };
            // m_aFinalSkeleton, m_oBaseHead

            var sb = new StringBuilder();

            // add the original mesh bone offsets (ref skeleton)
            sb.AppendLine("\tOriginalMeshBoneOffsets = (");
            for (int i = 0; i < meshBinary.RefSkeleton.Length; i++)
            {
                var refBone = meshBinary.RefSkeleton[i];
                sb.AppendLine($"\t\t{{Bone = '{refBone.Name}',Offset = {{X = {refBone.Position.X:F8}, Y = {refBone.Position.Y:F8}, Z = {refBone.Position.Z:F8}}}}}{(i < meshBinary.RefSkeleton.Length - 1 ? "," : "")}");

                if (!MorphHeadExcludeBones.Contains(refBone.Name))
                {
                    morphHeadSkeleton.Add(new StructProperty("OffsetBonePos",
                        false,
                        new NameProperty(refBone.Name, "nName"),
                        new StructProperty("Vector", true,
                            new FloatProperty(refBone.Position.X, "X"),
                            new FloatProperty(refBone.Position.Y, "Y"),
                            new FloatProperty(refBone.Position.Z, "Z")
                            )
                        { Name = "vPos" }));
                }
            }
            sb.AppendLine("\t)");

            morphHeadProps.Add(morphHeadSkeleton);

            // add the original mesh vertices
            sb.AppendLine("\tOriginalMeshLodModels = (");
            morphHeadBinary.LODs = new System.Numerics.Vector3[meshBinary.LODModels.Length][];
            for (int i = 0; i < meshBinary.LODModels.Length; i++)
            {
                var lodModel = meshBinary.LODModels[i];
                morphHeadBinary.LODs[i] = new System.Numerics.Vector3[lodModel.VertexBufferGPUSkin.VertexData.Length];
                var morphLod = morphHeadBinary.LODs[i];

                sb.Append("\t\t{vertices = (");
                for (int j = 0; j < lodModel.VertexBufferGPUSkin.VertexData.Length; j++)
                {
                    var vert = lodModel.VertexBufferGPUSkin.VertexData[j];
                    sb.Append($"{{X = {vert.Position.X:F8},Y = {vert.Position.Y:F8}, Z = {vert.Position.Z:F8}}}{(j < lodModel.VertexBufferGPUSkin.VertexData.Length - 1 ? "," : "")}");
                    morphLod[j] = new System.Numerics.Vector3(vert.Position.X, vert.Position.Y, vert.Position.Z);
                }
                sb.AppendLine($")}}{(i < meshBinary.LODModels.Length - 1 ? "," : "")}");
            }
            sb.AppendLine("\t)");

            var morphHead = CreateBioMorphFace(pew, headMesh.ObjectName + "_MorphHead");

            morphHead.WritePropertiesAndBinary(morphHeadProps, morphHeadBinary);

            return sb.ToString();
        }

        private static bool GetSelectedMeshBinary(PackageEditorWindow pew, out ExportEntry meshExport, out SkeletalMesh binary)
        {
            meshExport = null;
            binary = null;

            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return false; }

            if (pew.SelectedItem.Entry.ClassName != "SkeletalMesh")
            {
                ShowError("Selected item is not a SkeletalMesh");
                return false;
            }

            meshExport = (ExportEntry)pew.SelectedItem.Entry;
            binary = meshExport.GetBinaryData<SkeletalMesh>();

            return true;
        }

        public static void GetMeshMaterials(PackageEditorWindow pew)
        {
            List<string> mats = [];
            // get the export and binary of the Skeletal Mesh that is currently selected, if any
            if (GetSelectedMeshBinary(pew, out _, out var meshBinary))
            {

                foreach (var uIndex in meshBinary.Materials)
                {
                    var entry = pew.Pcc.GetEntry(uIndex);
                    mats.Add($"\"{entry.MemoryFullPath}\"");
                }

                var result = string.Join(",", mats);
                Clipboard.SetText(result);
            }
        }

        public static void MakeHeterochromiaMesh(PackageEditorWindow pew)
        {
            // get the export and binary of the Skeletal Mesh that is currently selected, if any
            if (GetSelectedMeshBinary(pew, out var headMesh, out var meshBinary))
            {
                // ask the user to pick which material is the eye material
                var chosenMaterialIndex = ChooseMaterial(pew, meshBinary, "Which material is the eye material?");
                if (chosenMaterialIndex == -1)
                {
                    return;
                }
                // add a new material slot to split the right eye into
                SetNumMaterialSlots(meshBinary, meshBinary.Materials.Length + 1);
                var newMaterialIndex = meshBinary.Materials.Length + 1;

                // from there, find the section we need to modify
                foreach (var lod in meshBinary.LODModels)
                {
                    SplitMaterial(lod, chosenMaterialIndex, newMaterialIndex, IsRightEyeTriangle);
                }

                headMesh.WriteBinary(meshBinary);
            }
        }

        private static void SplitMaterial(StaticLODModel lod, int originalMaterialIndex, int newMaterialIndex, Func<StaticLODModel, int, bool> isTriangleNewMaterial)
        {
            SkelMeshSection targetSection = null;
            int targetSectionIndex = -1;
            for (int i = 0; i < lod.Sections.Length; i++)
            {
                var section = lod.Sections[i];
                if (section.MaterialIndex == originalMaterialIndex)
                {
                    targetSectionIndex = i;
                    targetSection = section;
                    break;
                }
            }

            if (targetSection == null)
            {
                return;
            }

            var newSections = new List<SkelMeshSection>();

            for (int i = 0; i < targetSectionIndex; i++)
            {
                newSections.Add(lod.Sections[i]);
            }

            bool isNewMaterial = isTriangleNewMaterial(lod, (int)targetSection.BaseIndex);
            int currentTriangleCount = 0;
            int currentBaseIndex = (int)targetSection.BaseIndex;
            for (int i = (int)targetSection.BaseIndex; i < (int)targetSection.BaseIndex + targetSection.NumTriangles * 3; i += 3)
            {
                if (isTriangleNewMaterial(lod, i) == isNewMaterial)
                {
                    currentTriangleCount++;
                    continue;
                }

                newSections.Add(new SkelMeshSection()
                {
                    BaseIndex = (uint)currentBaseIndex,
                    ChunkIndex = targetSection.ChunkIndex,
                    MaterialIndex = (ushort)(isNewMaterial ? newMaterialIndex : originalMaterialIndex),
                    NumTriangles = currentTriangleCount,
                    TriangleSorting = targetSection.TriangleSorting
                });

                isNewMaterial = !isNewMaterial;
                currentBaseIndex = i;
                currentTriangleCount = 1;
            }

            newSections.Add(new SkelMeshSection()
            {
                BaseIndex = (uint)currentBaseIndex,
                ChunkIndex = targetSection.ChunkIndex,
                MaterialIndex = (ushort)(isNewMaterial ? newMaterialIndex : originalMaterialIndex),
                NumTriangles = currentTriangleCount,
                TriangleSorting = targetSection.TriangleSorting
            });

            for (int i = targetSectionIndex + 1; i < lod.Sections.Length; i++)
            {
                newSections.Add(lod.Sections[i]);
            }

            lod.Sections = [.. newSections];
        }

        private static (int, int, int) GetTriangle(StaticLODModel lod, int triangleIndex)
        {
            return (lod.IndexBuffer[triangleIndex], lod.IndexBuffer[triangleIndex + 1], lod.IndexBuffer[triangleIndex + 2]);
        }

        private static GPUSkinVertex GetVertex(StaticLODModel lod, int vertIndex)
        {
            return lod.VertexBufferGPUSkin.VertexData[vertIndex];
        }

        private static bool IsRightEyeTriangle(StaticLODModel lod, int triangleIndex)
        {
            var (v1, v2, v3) = GetTriangle(lod, triangleIndex);

            var numRightVerts = 0;
            if (IsRightEyeVertex(lod, v1))
            {
                numRightVerts++;
            }
            if (IsRightEyeVertex(lod, v2))
            {
                numRightVerts++;
            }
            if (IsRightEyeVertex(lod, v3))
            {
                numRightVerts++;
            }
            // if any of the three vertices is on the right side of the mesh, count this triangle as being on the right side
            return numRightVerts >= 1;
        }

        private static bool IsRightEyeVertex(StaticLODModel lod, int vertIndex)
        {
            // consider values very close to 0 as being 0 to avoid triangles that just barely cross onto the right side as being on the right side
            return GetVertex(lod, vertIndex).Position.Y > 0.0001;
        }

        private static int ChooseMaterial(PackageEditorWindow pew, SkeletalMesh meshBinary, string prompt)
        {
            var materialChoices = meshBinary.Materials.Select<int, IEntry>(x => x switch
            {
                < 0 => pew.Pcc.GetImport(x),
                0 => null,
                > 0 => pew.Pcc.GetUExport(x)
            }).ToList();

            var mat = EntrySelector.GetEntry<IEntry>(pew, pew.Pcc, prompt,
                    exp => materialChoices.Contains(exp));

            if (mat == null)
            {
                return -1;
            }
            return materialChoices.IndexOf(mat);
        }

        private static ExportEntry ChooseSkeletalMesh(PackageEditorWindow pew, string prompt)
        {
            if (EntrySelector.GetEntry<ExportEntry>(pew, pew.Pcc, prompt, exp => exp.ClassName == "SkeletalMesh") is ExportEntry meshExport)
            {
                return meshExport;
            }
            return null;
        }

        private static void SetNumMaterialSlots(SkeletalMesh meshBinary, int numMaterials)
        {
            if (meshBinary.Materials.Length == numMaterials)
            {
                return;
            }

            var tempMaterials = meshBinary.Materials;

            meshBinary.Materials = new int[numMaterials];
            for (int i = 0; i < numMaterials && i < tempMaterials.Length; i++)
            {
                meshBinary.Materials[i] = tempMaterials[i];
            }
        }

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
        }

        public static void BioMorphFaceToUniqueSkeletalMesh(PackageEditorWindow pew)
        {
            // make sure something is selected, a package is open ,and the right thing is selected
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf) || bmf.GetProperty<ObjectProperty>("m_oBaseHead") == null)
            {
                ShowError("You must select a BioMorphFace with a base head mesh for this command to work");
                return;
            }

            var baseHeadMesh = pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oBaseHead").Value) as ExportEntry;

            // clone the base head tree
            var newHeadEntry = EntryCloner.CloneTree(baseHeadMesh, false);
            newHeadEntry.Parent = bmf.Parent;
            newHeadEntry.ObjectNameString = $"{bmf.ObjectNameString}_MDL";

            var newHeadBinary = newHeadEntry.GetBinaryData<SkeletalMesh>();

            // create new materials
            for (int i = 0; i < newHeadBinary.Materials.Length; i++)
            {
                var oldMatIndex = newHeadBinary.Materials[i];
                var newMat = ExportCreator.CreateExport(pew.Pcc, $"{bmf.ObjectNameString}_MAT_1{NumToLetter(i)}", "MaterialInstanceConstant", bmf.Parent, null, false);
                newMat.WriteProperty(new ObjectProperty(pew.Pcc.GetEntry(oldMatIndex), "Parent"));
                newHeadBinary.Materials[i] = newMat.UIndex;
                // copy the relevant material configs from the thing
                if (pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oMaterialOverrides").Value) is ExportEntry bmo)
                {
                    BmoToMic(bmo, newMat);
                }
            }

            // next, copy the vertices from the bioMorphFace binary to the mesh binary
            var bmfBinary = bmf.GetBinaryData<BioMorphFace>();

            for (int lodIndex = 0; lodIndex < bmfBinary.LODs.Length && lodIndex < newHeadBinary.LODModels.Length; lodIndex++)
            {
                var lod = bmfBinary.LODs[lodIndex];
                var meshData = newHeadBinary.LODModels[lodIndex];

                if (pew.Pcc.Game == MEGame.ME1)
                {
                    for (int i = 0; i < lod.Length && i < meshData.ME1VertexBufferGPUSkin.Length; i++)
                    {
                        var bmfVert = lod[i];

                        meshData.ME1VertexBufferGPUSkin[i].Position.X = bmfVert.X;
                        meshData.ME1VertexBufferGPUSkin[i].Position.Y = bmfVert.Y;
                        meshData.ME1VertexBufferGPUSkin[i].Position.Z = bmfVert.Z;
                    }
                }
                else
                {
                    for (int i = 0; i < lod.Length && i < meshData.VertexBufferGPUSkin.VertexData.Length; i++)
                    {
                        var bmfVert = lod[i];

                        meshData.VertexBufferGPUSkin.VertexData[i].Position.X = bmfVert.X;
                        meshData.VertexBufferGPUSkin.VertexData[i].Position.Y = bmfVert.Y;
                        meshData.VertexBufferGPUSkin.VertexData[i].Position.Z = bmfVert.Z;
                    }
                }
            }

            newHeadEntry.WriteBinary(newHeadBinary);

            // make a new BioMorphFace with the same material overrides and skeleton adjstments, but remove the binary data with the vertex positions
            // so you can use this with an edited mesh and it won't deform it
            var newBmf = EntryCloner.CloneTree(bmf);
            var newBmfBinary = newBmf.GetBinaryData<BioMorphFace>();
            newBmfBinary.LODs = [];
            newBmf.WriteBinary(newBmfBinary);
            newBmf.RemoveProperty("m_aMorphFeatures");
            newBmf.WriteProperty(new ObjectProperty(newHeadEntry, "m_oBaseHead"));
        }

        private static char NumToLetter(int input)
        {
            return (char)('a' + (char)input);
        }

        private static void BmoToMic(ExportEntry source, ExportEntry targetExport)
        {
            var parentMat = SharedMethods.ResolveEntryToExport(targetExport.FileRef.GetEntry(targetExport.GetProperty<ObjectProperty>("Parent").Value), new PackageCache());

            ArrayProperty<StructProperty>? parentTextures = parentMat?.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");

            ArrayProperty<StructProperty> sourceTextures = source.GetProperty<ArrayProperty<StructProperty>>("m_aTextureOverrides");
            ArrayProperty<StructProperty> sourceVectors = source.GetProperty<ArrayProperty<StructProperty>>("m_aColorOverrides");
            ArrayProperty<StructProperty> sourceScalars = source.GetProperty<ArrayProperty<StructProperty>>("m_aScalarOverrides");

            ArrayProperty<StructProperty> targetTextures = new("TextureParameterValues");
            ArrayProperty<StructProperty> targetVectors = new("VectorParameterValues");
            ArrayProperty<StructProperty> targetScalars = new("ScalarParameterValues");

            if (sourceTextures != null)
            {
                foreach (StructProperty sourceTex in sourceTextures)
                {
                    var sourceParamName = sourceTex.GetProp<NameProperty>("nName").Value;
                    // make sure the texture exists on the base material so LEX is happier displaying it
                    if (parentTextures == null || parentTextures.Any(x => x.GetProp<NameProperty>("ParameterName").Value == sourceParamName))
                    {
                        PropertyCollection props =
                        [
                            new NameProperty(sourceParamName, "ParameterName"),
                            new ObjectProperty(sourceTex.GetProp<ObjectProperty>("m_pTexture").Value, "ParameterValue"),
                        ];
                        targetTextures.Add(new StructProperty("TextureParameterValue", props));
                    }
                }
            }

            if (sourceVectors != null)
            {
                foreach (StructProperty sourceVect in sourceVectors)
                {

                    PropertyCollection color =
                    [
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("R"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("G"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("B"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("A"),
                    ];
                    StructProperty ParameterValue = new("LinearColor", color, "ParameterValue", true);
                    PropertyCollection props =
                    [
                        ParameterValue,
                        new NameProperty(sourceVect.GetProp<NameProperty>("nName").Value, "ParameterName"),
                    ];
                    targetVectors.Add(new StructProperty("VectorParameterValue", props));
                }
            }

            if (sourceScalars != null)
            {
                foreach (StructProperty sourceScal in sourceScalars)
                {
                    PropertyCollection props =
                    [
                        new NameProperty(sourceScal.GetProp<NameProperty>("nName").Value, "ParameterName"),
                        new FloatProperty(sourceScal.GetProp<FloatProperty>("sValue").Value, "ParameterValue"),
                    ];
                    targetScalars.Add(new StructProperty("ScalarParameterValue", props));
                }
            }

            if (sourceTextures != null) { targetExport.WriteProperty(targetTextures); }
            if (sourceVectors != null) { targetExport.WriteProperty(targetVectors); }
            if (sourceScalars != null) { targetExport.WriteProperty(targetScalars); }
        }

        // seems promising, but needs more work
        public static void SmoothMeshSeams(PackageEditorWindow pew)
        {
            // pick two meshes
            var sourceMesh = ChooseSkeletalMesh(pew, "Choose source mesh (usually a head mesh) which will not be modified in this operation, just used as the source for vertex normals");
            var targetMesh = ChooseSkeletalMesh(pew, "Choose Target mesh (usually a body with a neck seam or a hair mesh that needs to be seamless with the scalp) which will have its vertex normals updated to match those on the source mesh as part of the operation.");

            if (sourceMesh != null && targetMesh != null)
            {
                var sourceBin = sourceMesh.GetBinaryData<SkeletalMesh>();
                var targetBin = targetMesh.GetBinaryData<SkeletalMesh>();

                var sourceVerts = new List<(int vertIndex, GPUSkinVertex vert)>();
                var targetVerts = new List<(int vertIndex, GPUSkinVertex vert)>();

                for (var i = 0; i < sourceBin.LODModels[0].VertexBufferGPUSkin.VertexData.Length; i++)
                {
                    sourceVerts.Add((i, sourceBin.LODModels[0].VertexBufferGPUSkin.VertexData[i]));
                }

                for (var i = 0; i < targetBin.LODModels[0].VertexBufferGPUSkin.VertexData.Length; i++)
                {
                    targetVerts.Add((i, targetBin.LODModels[0].VertexBufferGPUSkin.VertexData[i]));
                }

                var overlap = targetVerts.Join(sourceVerts, first => first.vert.Position, second => second.vert.Position, (first, second) => (first.vertIndex, second.vert), new VertComparer()).ToList();
                // now find which verts are in both sequences comparing by position, returning the ones from 
                //var intersect = targetVerts.Intersect(sourceVerts, new VertComparer()).ToArray();

                foreach (var (targetIndex, sourceVert) in overlap)
                {
                    // copy the position, tanX and tanZ from the source to the target to make the seam match up better.
                    targetBin.LODModels[0].VertexBufferGPUSkin.VertexData[targetIndex].Position = sourceVert.Position;
                    //targetBin.LODModels[0].VertexBufferGPUSkin.VertexData[targetIndex].TangentX = sourceVert.TangentX;
                    targetBin.LODModels[0].VertexBufferGPUSkin.VertexData[targetIndex].TangentZ = sourceVert.TangentZ;
                }

                targetMesh.WriteBinary(targetBin);
            }
        }

        private class VertComparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 x, Vector3 y)
            {
                return (x - y).Length() < 0.1;
            }

            public int GetHashCode(Vector3 obj)
            {
                return 0;
            }
        }

        private static bool GetPsaFromFile(PackageEditorWindow pew, out PSA psa, out string filePath)
        {
            var d = new OpenFileDialog
            {
                Filter = "PSA|*.psa",
                Title = "Select a psa file"
            };
            if (d.ShowDialog() == true)
            {
                psa = PSA.FromFile(d.FileName);
                filePath = d.FileName;
                return psa != null;
            }

            psa = null;
            filePath = null;
            return false;
        }

        private static bool GetPskFromFile(PackageEditorWindow pew, out PSK psk, out string filePath)
        {
            var d = new OpenFileDialog
            {
                Filter = "PSK|*.psk;*.pskx",
                Title = "Select a psk file"
            };
            if (d.ShowDialog() == true)
            {
                psk = PSK.FromFile(d.FileName);
                filePath = d.FileName;
                return psk != null;
            }

            psk = null;
            filePath = null;
            return false;
        }

        private static bool GetHeadmorphFromFile(out HeadMorph headmorph, out string filePath)
        {
            var d = new OpenFileDialog
            {
                Filter = "RON|*.ron",
                Title = "Select a ron file"
            };
            if (d.ShowDialog() == true)
            {
                headmorph = HeadMorph.FromRonFile(d.FileName);
                filePath = d.FileName;
                return headmorph != null;
            }

            headmorph = null;
            filePath = null;
            return false;
        }

        private class MeshSection
        {
            public PSK.PSKTriangle[] Triangles;
            public int BaseTriIndex;
            public int ChunkIndex;
            public int MatIndex;
            public int MinVertIndex;
            public int MaxVertIndex;
        }

        private class MeshChunk
        {
            public int VertIndexStart;
            public int VertIndexEnd;
            public HashSet<ushort> InfluenceBones;
            public int RigidVerts;
            public int SoftVerts;
            public int maxBoneInfluences;
        }

        public static void ReplaceBMFDataFromPskAndPsa(PackageEditorWindow pew)
        {
            if (GetSelectedItem(pew, "BioMorphFace", out var bmfExport))
            {
                if (GetPskFromFile(pew, out var psk, out _))
                {
                    var bmfBin = bmfExport.GetBinaryData<BioMorphFace>();

                    Vector3[] vertexPos = new Vector3[psk.Points.Count];

                    for (int i = 0; i < psk.Points.Count; i++)
                    {
                        vertexPos[i] = psk.Points[i] with { Y = -psk.Points[i].Y };
                    }

                    bmfBin.LODs = [[.. vertexPos]];

                    bmfExport.WriteBinary(bmfBin);
                }
                if (GetPsaFromFile(pew, out var psa, out _))
                {
                    // make sure there is at least one keyframe in this psa
                    if (psa.Keys.Count >= psa.Bones.Count)
                    {
                        var finalSkel = new ArrayProperty<StructProperty>("m_aFinalSkeleton");
                        for (int i = 0; i < psa.Bones.Count; i++)
                        {
                            var bone = psa.Bones[i];
                            var boneKeyframe = psa.Keys[i];
                            if (Vector3.Distance(bone.Position, boneKeyframe.Position) > 0.001)
                            {
                                var offsetBonePos = new StructProperty("OffsetBonePos", false, Vector3ToStructProperty(boneKeyframe.Position with { Y = -boneKeyframe.Position.Y }, "vPos"), new NameProperty(bone.Name, "nName"));
                                finalSkel.Add(offsetBonePos);
                            }
                        }
                        bmfExport.WriteProperty(finalSkel);
                    }
                }
            }
            else
            {
                ShowError("you must select a BioMorphFace to use this experiment");
                return;
            }
        }

        public static void ImportRonToBmf(PackageEditorWindow pew)
        {
            // make sure a file is open to put the new BMF into
            if (pew.Pcc == null)
            {
                return;
            }
            if (GetHeadmorphFromFile(out var headMorph, out var filePath))
            {
                IEntry baseHeadEntry = null;
                // find the base head mesh that goes with this
                if (GetHeadmorphBaseHead(headMorph, pew.Pcc.Game, out var baseHead, out var isFemaleMorph, out var gameMismatch))
                {
                    // esnure the base head is in this file
                    if (baseHead.Export.FileRef == pew.Pcc)
                    {
                        baseHeadEntry = baseHead.Export;
                    }
                    else
                    {
                        // create the package structure
                        var rootPackage = ExportCreator.CreatePackageExport(pew.Pcc, isFemaleMorph ? "BIOG_HMF_HED_PROMorph_R" : "BIOG_HMM_HED_PROMorph");
                        var containingPackage = ExportCreator.CreatePackageExport(pew.Pcc, "Custom", rootPackage);

                        ImportAndRelinkEntries(PortingOption.CloneAllDependencies, baseHead.Export, pew.Pcc, containingPackage, true, new RelinkerOptionsPackage(), out baseHeadEntry);
                    }
                }
                else if (gameMismatch)
                {
                    ShowError("you are attemtping to import a headmorph from ME1/2 into ME3 or vice versa; this is almost cetainly not what you want; if it is, import it into the correct game and then port it to the target game");
                    return;
                }

                // create a new BioMorphFace
                var bmf = ExportCreator.CreateExport(pew.Pcc, Path.GetFileNameWithoutExtension(filePath), "BioMorphFace");

                // add a reference to the hair mesh, if it is in this file
                if (headMorph.HairMesh != null && headMorph.HairMesh != "None")
                {
                    var hairMeshEntry = pew.Pcc.FindEntry(headMorph.HairMesh);
                    if (hairMeshEntry != null)
                    {
                        bmf.WriteProperty(new ObjectProperty(hairMeshEntry, "m_oHairMesh"));
                    }
                }

                // create the BioMaterialOverride object and property
                var matOverrideEntry = ExportCreator.CreateExport(pew.Pcc, "BioMaterialOverride", "BioMaterialOverride", bmf);
                bmf.WriteProperty(new ObjectProperty(matOverrideEntry, "m_oMaterialOverrides"));

                // write the scalars into the material overrides
                var scalarParams = headMorph.ScalarParameters.Select(x => new StructProperty("ScalarParameter", false, new NameProperty(x.Key, "nName"), new FloatProperty(x.Value, "sValue")));
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(scalarParams, "m_aScalarOverrides"));

                // write the scalars into the material overrides
                var vectorParams = headMorph.VectorParameters.Select(x => new StructProperty("ColorParameter", false, new NameProperty(x.Key, "nName"), LinearColorToStructProperty(x.Value, "cValue")));
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(vectorParams, "m_aColorOverrides"));

                // write the textures into the material overrides
                List<StructProperty> textureParams = [];
                foreach (var tex in headMorph.TextureParameters)
                {
                    // if the referenced texture is in this file, add it to the material overrides
                    var textureEntry = pew.Pcc.FindEntry(tex.Value, "Texture2D");
                    if (textureEntry != null)
                    {
                        textureParams.Add(new StructProperty("TextureParameter", [new NameProperty(tex.Key, "nName"), new ObjectProperty(textureEntry, "m_pTexture")]));
                    }
                }
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(textureParams, "m_aTextureOverrides"));

                // add the accessories if they are in this file
                List<ObjectProperty> otherMeshes = [];
                foreach (var accessory in headMorph.AccessoryMeshes)
                {
                    var meshEntry = pew.Pcc.FindEntry(accessory, "SkeletalMesh");
                    if (meshEntry != null)
                    {
                        otherMeshes.Add(new ObjectProperty(meshEntry));
                    }
                }
                if (otherMeshes.Any())
                {
                    bmf.WriteProperty(new ArrayProperty<ObjectProperty>(otherMeshes, "m_oOtherMeshes"));
                }

                // morph features
                var morphFeatures = headMorph.MorphFeatures.Select(x => new StructProperty("MorphFeature", false, new NameProperty(x.Key, "sFeatureName"), new FloatProperty(x.Value, "Offset")));
                bmf.WriteProperty(new ArrayProperty<StructProperty>(morphFeatures, "m_aMorphFeatures"));

                // bone offsets
                var boneOffsets = headMorph.OffsetBones.Select(x => new StructProperty("OffsetBonePos", false, new NameProperty(x.Key, "nName"), Vector3ToStructProperty(x.Value, "vPos")));
                bmf.WriteProperty(new ArrayProperty<StructProperty>(boneOffsets, "m_aFinalSkeleton"));

                // vertices
                var bmfBin = new BioMorphFace();
                List<Vector3[]> LODs = [];
                if (headMorph.Lod0Vertices != null && headMorph.Lod0Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod0Vertices]);
                }
                if (headMorph.Lod1Vertices != null && headMorph.Lod1Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod1Vertices]);
                }
                if (headMorph.Lod2Vertices != null && headMorph.Lod2Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod2Vertices]);
                }
                bmfBin.LODs = [.. LODs];

                bmf.WriteBinary(bmfBin);

                if (baseHead != null)
                {
                    bmf.WriteProperty(new ObjectProperty(baseHeadEntry, "m_oBaseHead"));
                }
                else
                {
                    ShowError("BioMorphFace was created, but the experiment was unable to link a base head");
                }
            }
        }

        private static StructProperty LinearColorToStructProperty(LinearColor color, NameReference? name = null)
        {
            return new StructProperty("LinearColor",
                [
                    new FloatProperty(color.R, "R"),
                    new FloatProperty(color.G, "G"),
                    new FloatProperty(color.B, "B"),
                    new FloatProperty(color.A, "A")
                ], name, true);
        }

        private static StructProperty Vector3ToStructProperty(Vector3 vect, NameReference? name = null)
        {
            return new StructProperty("Vector",
                [
                    new FloatProperty(vect.X, "X"),
                    new FloatProperty(vect.Y, "Y"),
                    new FloatProperty(vect.Z, "Z"),
                ], name, true);
        }

        private static Vector3 StructPropertyToVector3(StructProperty prop)
        {
            return new Vector3(
                prop.GetProp<FloatProperty>("X").Value,
                prop.GetProp<FloatProperty>("Y").Value,
                prop.GetProp<FloatProperty>("Z").Value);
        }

        private static LinearColor StructPropertyToLinearColor(StructProperty prop)
        {
            return new LinearColor(
                prop.GetProp<FloatProperty>("R").Value,
                prop.GetProp<FloatProperty>("G").Value,
                prop.GetProp<FloatProperty>("B").Value,
                prop.GetProp<FloatProperty>("A").Value);
        }

        public static void UpdateRonFromPskAndPsa(PackageEditorWindow pew)
        {
            if (GetHeadmorphFromFile(out var headMorph, out var ronFilePath))
            {
                if (GetPskFromFile(pew, out var psk, out _))
                {
                    headMorph.Lod0Vertices = new List<Vector3>(psk.Points.Count);
                    for (int i = 0; i < psk.Points.Count; i++)
                    {
                        headMorph.Lod0Vertices.Add(psk.Points[i] with { Y = -psk.Points[i].Y });
                    }
                }
                if (GetPsaFromFile(pew, out var psa, out _))
                {
                    // make sure there is at least one keyframe in this psa
                    if (psa.Keys.Count >= psa.Bones.Count)
                    {
                        headMorph.OffsetBones.Clear();
                        for (int i = 0; i < psa.Bones.Count; i++)
                        {
                            var bone = psa.Bones[i];
                            var boneKeyframe = psa.Keys[i];
                            if (Vector3.Distance(bone.Position, boneKeyframe.Position) > 0.001)
                            {
                                headMorph.OffsetBones.Add(bone.Name, boneKeyframe.Position with { Y = -boneKeyframe.Position.Y });
                            }
                        }
                    }
                }
                headMorph.ToRonFile(ronFilePath);
            }
        }

        public static void ExportBmfToRon(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf))
            {
                ShowError("you must select a BioMorphFace export for this experiment");
                return;
            }

            var headMorph = new HeadMorph()
            {
                AccessoryMeshes = [],
                Lod0Vertices = [],
                Lod1Vertices = [],
                Lod2Vertices = [],
                Lod3Vertices = [],
                MorphFeatures = [],
                OffsetBones = [],
                ScalarParameters = [],
                TextureParameters = [],
                VectorParameters = []
            };

            var props = bmf.GetProperties();

            // morph features
            var morphs = props.GetProp<ArrayProperty<StructProperty>>("m_aMorphFeatures");
            foreach (var morph in morphs)
            {
                headMorph.MorphFeatures.Add(morph.GetProp<NameProperty>("sFeatureName").Value, morph.GetProp<FloatProperty>("Offset").Value);
            }

            // final skeleton
            var finalSkeleton = props.GetProp<ArrayProperty<StructProperty>>("m_aFinalSkeleton");
            foreach (var offsetBone in finalSkeleton)
            {
                headMorph.OffsetBones.Add(offsetBone.GetProp<NameProperty>("nName").Value, StructPropertyToVector3(offsetBone.GetProp<StructProperty>("vPos")));
            }

            // other meshes
            var otherMeshes = props.GetProp<ArrayProperty<ObjectProperty>>("m_oOtherMeshes");
            if (otherMeshes != null)
            {
                foreach (var otherMesh in otherMeshes)
                {
                    var entry = pew.Pcc.GetEntry(otherMesh.Value);
                    if (entry != null)
                    {
                        headMorph.AccessoryMeshes.Add(entry.MemoryFullPath);
                    }
                }
            }

            // LODs
            var bmfBin = bmf.GetBinaryData<BioMorphFace>();
            if (bmfBin.LODs.Length > 0)
            {
                headMorph.Lod0Vertices = [.. bmfBin.LODs[0]];
            }
            if (bmfBin.LODs.Length > 1)
            {
                headMorph.Lod1Vertices = [.. bmfBin.LODs[1]];
            }
            if (bmfBin.LODs.Length > 2)
            {
                headMorph.Lod2Vertices = [.. bmfBin.LODs[2]];
            }
            if (bmfBin.LODs.Length > 3)
            {
                headMorph.Lod3Vertices = [.. bmfBin.LODs[3]];
            }

            var materialsProp = props.GetProp<ObjectProperty>("m_oMaterialOverrides");
            if (materialsProp != null)
            {
                var matOverrides = (ExportEntry)pew.Pcc.GetEntry(materialsProp.Value);

                if (matOverrides != null)
                {
                    var matProps = matOverrides.GetProperties();

                    // textures
                    var textureProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aTextureOverrides");
                    foreach (var tex in textureProps)
                    {
                        headMorph.TextureParameters.Add(
                            tex.GetProp<NameProperty>("nName").Value,
                            pew.Pcc.GetEntry(tex.GetProp<ObjectProperty>("m_pTexture").Value).MemoryFullPath);
                    }

                    // vectors
                    var vectorProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aColorOverrides");
                    foreach (var vector in vectorProps)
                    {
                        headMorph.VectorParameters.Add(
                            vector.GetProp<NameProperty>("nName").Value,
                            StructPropertyToLinearColor(vector.GetProp<StructProperty>("cValue")));
                    }

                    // scalars
                    var scalarProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aScalarOverrides");
                    foreach (var scalar in scalarProps)
                    {
                        headMorph.ScalarParameters.Add(
                            scalar.GetProp<NameProperty>("nName").Value,
                            scalar.GetProp<FloatProperty>("sValue").Value);
                    }
                }
            }

            var d = new SaveFileDialog { Filter = "RON|*.ron" };
            if (d.ShowDialog() == true)
            {
                headMorph.ToRonFile(d.FileName);
            }
        }

        public static void ImportPskAndPsaAsMorphTarget(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "MorphTargetSet", out var morphTargetSet))
            {
                ShowError("you must select a morphTargetSet export for this experiment");
                return;
            }

            var baseMesh = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").Value), new PackageCache());

            if (baseMesh == null || baseMesh.ClassName != "SkeletalMesh")
            {
                ShowError("selected MorphTargetSet must have a base mesh");
                return;
            }

            var baseMeshBinary = baseMesh.GetBinaryData<SkeletalMesh>();

            // using bitwise | so it evaluates the second even if the first evaluates to true
            if (GetPskFromFile(pew, out var psk, out var pskName) | GetPsaFromFile(pew, out var psa, out var psaName))
            {
                var morphTargetName = Path.GetFileNameWithoutExtension(pskName ?? psaName);

                var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");
                // get or create a morph target with the name of the psa/psk, along with the binary data
                var morphTarget = targets.Select(x => pew.Pcc.GetEntry(x.Value)).FirstOrDefault(x => x.ObjectName == morphTargetName && x.ClassName == "MorphTarget") as ExportEntry;
                MorphTarget morphTargetBin = morphTarget?.GetBinaryData<MorphTarget>();
                if (morphTarget == null)
                {
                    // create the new export
                    morphTarget = ExportCreator.CreateExport(pew.Pcc, morphTargetName, "MorphTarget", morphTargetSet, indexed: false);
                    // set up the skeleton of the binary data
                    morphTargetBin = new MorphTarget
                    {
                        MorphLODModels = [new MorphTarget.MorphLODModel()]
                    };
                    morphTargetBin.MorphLODModels[0].NumBaseMeshVerts = psk.Points.Count;

                    // add it to the morph target set
                    targets.Add(new ObjectProperty(morphTarget.UIndex));
                    morphTargetSet.WriteProperty(targets);
                }

                if (psk != null)
                {
                    if (psk.Points.Count != baseMeshBinary.LODModels[0].NumVertices)
                    {
                        ShowError("the number of vertices in the base mesh (LOD 0) and the psk must match.");
                        return;
                    }

                    if (psk.Points.Count != psk.Wedges.Count)
                    {
                        ShowError("Can't use this psk; number of points and wedges differ.");
                        return;
                    }

                    List<MorphTarget.MorphVertex> vertDeltas = [];

                    for (int i = 0; i < psk.Points.Count; i++)
                    {
                        // gotta flip the y part of the position
                        psk.Points[i] = new Vector3(psk.Points[i].X, psk.Points[i].Y * -1, psk.Points[i].Z);

                        // TODO I could more simply represent this with a distance call and comparison
                        if (!ApproximatelyEqual(baseMeshBinary.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position, psk.Points[i]))
                        {
                            vertDeltas.Add(new MorphTarget.MorphVertex()
                            {
                                SourceIdx = (ushort)i,
                                PositionDelta = psk.Points[i] - baseMeshBinary.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position
                            });
                        }

                        // TODO anything with vertex normal deltas once we can export those, and they aren't actually used in game? probably not
                    }

                    morphTargetBin.MorphLODModels[0].Vertices = [.. vertDeltas];
                }

                if (psa != null && psa.Keys.Count >= psa.Bones.Count)
                {
                    List<MorphTarget.BoneOffset> boneOffsets = [];
                    for (int i = 0; i < psa.Bones.Count; i++)
                    {
                        var bone = psa.Bones[i];
                        var boneKeyframe = psa.Keys[i];
                        if (Vector3.Distance(bone.Position, boneKeyframe.Position) > 0.001)
                        {
                            var offset = boneKeyframe.Position - bone.Position;
                            boneOffsets.Add(new MorphTarget.BoneOffset
                            {
                                Bone = bone.Name,
                                Offset = offset with { Y = -offset.Y }
                            });
                        }
                    }
                    morphTargetBin.BoneOffsets = [.. boneOffsets];
                }

                morphTarget.WriteBinary(morphTargetBin);
            }
        }

        private static bool ApproximatelyEqual(Vector3 first, Vector3 second)
        {
            var acceptabledelta = 0.01;
            if (Math.Abs(first.X - second.X) < acceptabledelta
                && Math.Abs(first.Y - second.Y) < acceptabledelta
                && Math.Abs(first.Z - second.Z) < acceptabledelta)
            {
                return true;
            }
            return false;
        }

        private static void ExportMorphTargetSet(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "MorphTargetSet", out var morphTargetSet))
            {
                ShowError("you must select a morphTargetSet export for this experiment");
                return;
            }

            var baseMesh = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").Value), new PackageCache());

            if (baseMesh == null || baseMesh.ClassName != "SkeletalMesh")
            {
                ShowError("selected MorphTargetSet must have a base mesh");
                return;
            }

            var baseMeshBin = baseMesh.GetBinaryData<SkeletalMesh>();
            var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");

            var d = new SaveFileDialog { Filter = "PSKX|*.pskx" };
            if (d.ShowDialog() == true)
            {
                // output the special psk into a file with the name of the base head
                // make most of the psk from the base skeletal mesh
                var psk = PSK.CreateFromSkeletalMesh(baseMeshBin, 0, true);

                foreach (var target in targets)
                {
                    var targetExport = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(target.Value), new PackageCache());
                    var targetBin = targetExport.GetBinaryData<MorphTarget>();
                    psk.Morphs.Add(new PSK.MorphInfo
                    {
                        Name = targetExport.ObjectNameString,
                        VertexCount = targetBin.MorphLODModels[0].Vertices.Length
                    });

                    foreach (var vertex in targetBin.MorphLODModels[0].Vertices)
                    {
                        psk.MorphData.Add(new PSK.MorphDelta
                        {
                            PointIndex = vertex.SourceIdx,
                            PositionDelta = vertex.PositionDelta,
                            // this gets ignored on import to Blender anyway
                            //TangentZDelta = vertex.TangentZDelta
                        });
                    }
                }

                psk.ToFile(d.FileName);

                // now, output the psa file and config file
                var config = new StringBuilder();
                config.AppendLine("[RemoveTracks]");
                var psa = new PSA
                {
                    Bones = [],
                    Infos = [],
                    Keys = []
                };

                foreach (var bone in baseMeshBin.RefSkeleton)
                {
                    psa.Bones.Add(new PSABone
                    {
                        Name = bone.Name,
                        ParentIndex = bone.ParentIndex,
                    });
                }

                var frameNum = 0;
                foreach (var target in targets)
                {
                    var targetExport = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(target.Value), new PackageCache());
                    var targetBin = targetExport.GetBinaryData<MorphTarget>();

                    if (targetBin.BoneOffsets.Length == 0)
                    {
                        continue;
                    }

                    psa.Infos.Add(new PSAAnimInfo
                    {
                        Name = targetExport.ObjectNameString,
                        Group = "None",
                        TotalBones = baseMeshBin.RefSkeleton.Length,
                        KeyQuotum = baseMeshBin.RefSkeleton.Length, // this would be multiplied by the number of frames, but there is just one frame
                        TrackTime = 1,
                        AnimRate = 1,
                        FirstRawFrame = frameNum,
                        NumRawFrames = 1
                    });
                    frameNum += 1;

                    for (int i = 0; i < baseMeshBin.RefSkeleton.Length; i++)
                    {
                        var refBone = baseMeshBin.RefSkeleton[i];
                        // does this bone get influenced by this morph target?
                        var influence = targetBin.BoneOffsets.FirstOrDefault(x => x.Bone == refBone.Name);
                        //var rotQuat = new Quaternion(refBone.Orientation.X, refBone.Orientation.Y, refBone.Orientation.Z, refBone.Orientation.W);
                        var rotQuat = new Quaternion(0, 0, 0, 1);
                        //var posVec = refBone.Position with { Y = refBone.Position.Y * -1 };
                        var posVec = new Vector3(0, 0, 0);
                        if (influence != null)
                        {
                            posVec = new Vector3(refBone.Position.X + influence.Offset.X, -refBone.Position.Y - influence.Offset.Y, refBone.Position.Z + influence.Offset.Z);
                            // do not output rotation when you import this one
                            config.AppendLine($"{targetExport.ObjectName}.{i}=rot");
                        }
                        else
                        {
                            // do not output anything when you import this one
                            config.AppendLine($"{targetExport.ObjectName}.{i}=all");
                        }

                        psa.Keys.Add(new PSAAnimKeys
                        {
                            Position = posVec,
                            Rotation = rotQuat,
                            Time = 30
                        });
                    }
                }

                psa.ToFile(Path.ChangeExtension(d.FileName, "psa"));

                // also output a config file next to this to tell it to skip rotations for every sequence and every bone
                File.WriteAllText(Path.ChangeExtension(d.FileName, "config"), config.ToString());
            }
        }

        private static bool GetSelectedItem(PackageEditorWindow pew, string expectedType, out ExportEntry entry)
        {
            return GetSelectedItem(pew, [expectedType], out entry);
        }

        private static bool GetSelectedItem(PackageEditorWindow pew, string[] expectedTypes, out ExportEntry entry)
        {
            entry = null;
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return false; }

            if (!expectedTypes.Contains(pew.SelectedItem.Entry.ClassName))
            {
                return false;
            }

            entry = (ExportEntry)pew.SelectedItem.Entry;

            return entry != null;
        }
    }
}
