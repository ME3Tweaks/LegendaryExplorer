using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    /// Output coordinate system for BVH export.
    /// UE3's native space is Z-up, X-forward, Y-right, left-handed.
    /// </summary>
    public enum BVHCoordinateSystem
    {
        /// <summary>UE3 native — no transform applied.</summary>
        ZUpLeftHanded,
        /// <summary>Z-up right-handed (Y axis flipped to change handedness).</summary>
        ZUpRightHanded,
        /// <summary>Y-up left-handed (cyclic axis permutation: X→Y, Y→Z, Z→X).</summary>
        YUpLeftHanded,
        /// <summary>Y-up right-handed — Blender's default BVH import convention.</summary>
        YUpRightHanded,
    }

    public static class BVH
    {
        private const float RadToDeg = 180f / MathF.PI;
        private const float DegToRad = MathF.PI / 180f;

        public static void ExportToBVH(AnimSequence animSeq, string filePath, MeshBone[] refSkeleton,
                                       BVHCoordinateSystem coordinateSystem = BVHCoordinateSystem.YUpRightHanded)
        {
            ArgumentNullException.ThrowIfNull(animSeq);
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(refSkeleton);

            // Always decompress fresh (matches PSA pattern)
            animSeq.DecompressAnimationData();

            var shouldBoneUsePositionTrack = animSeq.GetPositionTrackFilter();

            // Map bone name → animTrack index for fast lookup
            var boneNameToTrackIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < animSeq.Bones.Count; i++)
                boneNameToTrackIdx[animSeq.Bones[i]] = i;

            // Build children lists from refSkeleton
            var children = new List<int>[refSkeleton.Length];
            for (int i = 0; i < refSkeleton.Length; i++)
                children[i] = [];

            int rootIdx = -1;
            for (int i = 0; i < refSkeleton.Length; i++)
            {
                int parent = refSkeleton[i].ParentIndex;
                if (parent < 0 || parent == i)
                    rootIdx = i;
                else
                    children[parent].Add(i);
            }

            if (rootIdx < 0)
                throw new InvalidOperationException("RefSkeleton has no root bone (bone with no parent).");

            // Build depth-first traversal order — determines column order in MOTION data
            var traversalOrder = new List<int>(refSkeleton.Length);
            BuildTraversalOrder(rootIdx, children, traversalOrder);

            using var sw = new StreamWriter(filePath, append: false, Encoding.ASCII);
            sw.NewLine = "\n";

            sw.WriteLine("HIERARCHY");
            WriteHierarchy(sw, rootIdx, refSkeleton, children, 0, coordinateSystem);

            float frameTime = animSeq.NumFrames > 1 && animSeq.SequenceLength > 0f
                ? animSeq.SequenceLength / (animSeq.NumFrames * animSeq.RateScale)
                : 1f / 30f;

            sw.WriteLine("MOTION");
            sw.WriteLine($"Frames: {animSeq.NumFrames}");
            sw.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Frame Time: {frameTime:F6}"));

            var sb = new StringBuilder();
            for (int frameIdx = 0; frameIdx < animSeq.NumFrames; frameIdx++)
            {
                sb.Clear();
                bool firstValue = true;
                foreach (int boneIdx in traversalOrder)
                {
                    string boneName = refSkeleton[boneIdx].Name.Instanced;
                    bool hasTrack = boneNameToTrackIdx.TryGetValue(boneName, out int trackIdx);

                    Vector3 pos;
                    Quaternion rot;

                    if (hasTrack)
                    {
                        AnimTrack track = animSeq.RawAnimationData[trackIdx];
                        rot = SampleRotation(track, frameIdx);
                        pos = shouldBoneUsePositionTrack(boneName)
                            ? SamplePosition(track, frameIdx, refSkeleton[boneIdx].Position)
                            : refSkeleton[boneIdx].Position;
                    }
                    else
                    {
                        // No animation track: hold bind pose
                        rot = Quaternion.Identity;
                        pos = refSkeleton[boneIdx].Position;
                    }

                    (float rz, float rx, float ry) = QuaternionToZXYEulerDegrees(rot, coordinateSystem);

                    if (!firstValue) sb.Append(' ');
                    firstValue = false;

                    Vector3 tp = TransformPosition(pos, coordinateSystem);
                    sb.Append(tp.X.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(tp.Y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(tp.Z.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');

                    sb.Append(rz.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(rx.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(ry.ToString("F6", CultureInfo.InvariantCulture));
                }
                sw.WriteLine(sb);
            }
        }

        private static void WriteHierarchy(StreamWriter sw, int boneIdx, MeshBone[] refSkeleton,
                                           List<int>[] children, int depth, BVHCoordinateSystem cs)
        {
            string indent = new string('\t', depth);
            string boneName = refSkeleton[boneIdx].Name.Instanced;
            Vector3 tp = TransformPosition(refSkeleton[boneIdx].Position, cs);
            string ox = tp.X.ToString("F6", CultureInfo.InvariantCulture);
            string oy = tp.Y.ToString("F6", CultureInfo.InvariantCulture);
            string oz = tp.Z.ToString("F6", CultureInfo.InvariantCulture);

            bool isRoot = depth == 0;
            string keyword = isRoot ? "ROOT" : "JOINT";

            sw.WriteLine($"{indent}{keyword} {boneName}");
            sw.WriteLine($"{indent}{{");
            sw.WriteLine($"{indent}\tOFFSET {ox} {oy} {oz}");
            sw.WriteLine($"{indent}\tCHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation");

            if (children[boneIdx].Count == 0)
            {
                sw.WriteLine($"{indent}\tEnd Site");
                sw.WriteLine($"{indent}\t{{");
                sw.WriteLine($"{indent}\t\tOFFSET 0.000000 1.000000 0.000000");
                sw.WriteLine($"{indent}\t}}");
            }
            else
            {
                foreach (int childIdx in children[boneIdx])
                    WriteHierarchy(sw, childIdx, refSkeleton, children, depth + 1, cs);
            }

            sw.WriteLine($"{indent}}}");
        }

        private static void BuildTraversalOrder(int boneIdx, List<int>[] children, List<int> result)
        {
            result.Add(boneIdx);
            foreach (int child in children[boneIdx])
                BuildTraversalOrder(child, children, result);
        }

        private static Vector3 TransformPosition(Vector3 v, BVHCoordinateSystem cs) => cs switch
        {
            BVHCoordinateSystem.ZUpLeftHanded   => v,
            BVHCoordinateSystem.ZUpRightHanded  => new Vector3(v.X, -v.Y, v.Z),
            BVHCoordinateSystem.YUpLeftHanded   => new Vector3(v.Y, v.Z, v.X),
            BVHCoordinateSystem.YUpRightHanded  => new Vector3(v.Y, v.Z, -v.X),
            _ => throw new ArgumentOutOfRangeException(nameof(cs))
        };

        private static Quaternion SampleRotation(AnimTrack track, int frameIdx)
        {
            if (track.Rotations == null || track.Rotations.Count == 0)
                return Quaternion.Identity;
            int i = Math.Clamp(frameIdx, 0, track.Rotations.Count - 1);
            return Quaternion.Conjugate(track.Rotations[i]);
        }

        private static Vector3 SamplePosition(AnimTrack track, int frameIdx, Vector3 bindPosePosition)
        {
            if (track.Positions == null || track.Positions.Count == 0)
                return bindPosePosition;
            if (track.Positions.Count == 1)
                return track.Positions[0];
            int i = Math.Clamp(frameIdx, 0, track.Positions.Count - 1);
            return track.Positions[i];
        }

        private static (float rz, float rx, float ry) QuaternionToZXYEulerDegrees(Quaternion q, BVHCoordinateSystem cs)
        {
            var m = Matrix4x4.CreateFromQuaternion(q);
            return cs switch
            {
                BVHCoordinateSystem.ZUpLeftHanded =>
                    ExtractZXYEuler(m.M11,  m.M12,  m.M13,
                                    m.M21,  m.M22,  m.M23,
                                    m.M31,  m.M32,  m.M33),

                BVHCoordinateSystem.ZUpRightHanded =>
                    ExtractZXYEuler( m.M11, -m.M12,  m.M13,
                                    -m.M21,  m.M22, -m.M23,
                                     m.M31, -m.M32,  m.M33),

                BVHCoordinateSystem.YUpLeftHanded =>
                    ExtractZXYEuler(m.M22, m.M23,  m.M21,
                                    m.M32, m.M33,  m.M31,
                                    m.M12, m.M13,  m.M11),

                BVHCoordinateSystem.YUpRightHanded =>
                    ExtractZXYEuler( m.M22,  m.M23, -m.M21,
                                     m.M32,  m.M33, -m.M31,
                                    -m.M12, -m.M13,  m.M11),

                _ => throw new ArgumentOutOfRangeException(nameof(cs))
            };
        }

        // Imports a BVH file and returns a decompressed AnimSequence ready to be written into a package.
        // Joint names from the BVH HIERARCHY become the Bones list; the caller assigns animSeq.Name.
        public static AnimSequence ImportFromBVH(string filePath,
                                                 BVHCoordinateSystem coordinateSystem = BVHCoordinateSystem.YUpRightHanded)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            using var sr = new StreamReader(filePath, Encoding.ASCII);

            List<BvhJoint> joints = ParseBvhHierarchy(sr);
            ParseBvhMotion(sr, out int numFrames, out float frameTime, out var frameData);

            return BuildAnimSequenceFromBvh(joints, frameData, numFrames, frameTime, coordinateSystem);
        }

        // ── BVH joint descriptor ─────────────────────────────────────────────────────
        private sealed class BvhJoint
        {
            public string Name;
            public int ParentIndex;          // -1 for root
            public readonly List<string> Channels = [];
            public int ChannelOffset;        // first index into per-frame float array
        }

        // ── HIERARCHY parser ─────────────────────────────────────────────────────────
        private static List<BvhJoint> ParseBvhHierarchy(StreamReader sr)
        {
            var joints = new List<BvhJoint>();
            var stack = new Stack<int>(); // joint indices; -1 = End Site sentinel
            int totalChannels = 0;
            bool inEndSite = false;

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line == "{") continue;

                if (line == "}")
                {
                    if (stack.Count > 0) stack.Pop();
                    inEndSite = false;
                    continue;
                }

                if (inEndSite) continue; // skip OFFSET inside End Site block

                var tok = line.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length == 0) continue;

                switch (tok[0].ToUpperInvariant())
                {
                    case "HIERARCHY":
                        break;

                    case "MOTION":
                        return joints;

                    case "ROOT":
                    case "JOINT":
                    {
                        int parentIdx = stack.TryPeek(out int p) && p >= 0 ? p : -1;
                        var joint = new BvhJoint { Name = tok[1], ParentIndex = parentIdx, ChannelOffset = totalChannels };
                        stack.Push(joints.Count);
                        joints.Add(joint);
                        break;
                    }

                    case "END": // "End Site"
                        stack.Push(-1);
                        inEndSite = true;
                        break;

                    case "CHANNELS":
                    {
                        int n = int.Parse(tok[1]);
                        if (stack.TryPeek(out int jIdx) && jIdx >= 0)
                        {
                            joints[jIdx].ChannelOffset = totalChannels;
                            for (int i = 2; i < 2 + n && i < tok.Length; i++)
                                joints[jIdx].Channels.Add(tok[i]);
                        }
                        totalChannels += n;
                        break;
                    }
                    // OFFSET not needed for import
                }
            }
            return joints;
        }

        // ── MOTION parser ────────────────────────────────────────────────────────────
        // sr must be positioned immediately after the MOTION keyword line.
        private static void ParseBvhMotion(StreamReader sr,
            out int numFrames, out float frameTime, out List<float[]> frameData)
        {
            numFrames = 0;
            frameTime = 1f / 30f;
            frameData = [];

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("Frames:", StringComparison.OrdinalIgnoreCase))
                {
                    _ = int.TryParse(line["Frames:".Length..].Trim(), out numFrames);
                }
                else if (line.StartsWith("Frame Time:", StringComparison.OrdinalIgnoreCase))
                {
                    _ = float.TryParse(line["Frame Time:".Length..].Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out frameTime);
                }
                else
                {
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;
                    var values = new float[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                        _ = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
                    frameData.Add(values);
                }
            }
        }

        // ── AnimSequence builder ─────────────────────────────────────────────────────
        private static AnimSequence BuildAnimSequenceFromBvh(List<BvhJoint> joints, List<float[]> frameData,
            int numFrames, float frameTime, BVHCoordinateSystem cs)
        {
            int actualFrames = Math.Max(frameData.Count, numFrames);

            var boneNames = new List<string>(joints.Count);
            var animTracks = new List<AnimTrack>(joints.Count);

            foreach (var joint in joints)
            {
                bool hasPositionChannels = joint.Channels.Any(
                    c => c.EndsWith("position", StringComparison.OrdinalIgnoreCase));

                var positions = hasPositionChannels ? new List<Vector3>(actualFrames) : [];
                var rotations = new List<Quaternion>(actualFrames);

                foreach (float[] data in frameData)
                {
                    float px = 0, py = 0, pz = 0;
                    float rz = 0, rx = 0, ry = 0;

                    for (int ch = 0; ch < joint.Channels.Count; ch++)
                    {
                        int idx = joint.ChannelOffset + ch;
                        float val = idx < data.Length ? data[idx] : 0f;
                        switch (joint.Channels[ch].ToUpperInvariant())
                        {
                            case "XPOSITION": px = val; break;
                            case "YPOSITION": py = val; break;
                            case "ZPOSITION": pz = val; break;
                            case "XROTATION": rx = val; break;
                            case "YROTATION": ry = val; break;
                            case "ZROTATION": rz = val; break;
                        }
                    }

                    // Convert BVH rotation (BVH space) → UE3, then conjugate for storage.
                    rotations.Add(Quaternion.Conjugate(ZXYEulerToUE3Quaternion(rz, rx, ry, cs)));

                    if (hasPositionChannels)
                        positions.Add(InverseTransformPosition(new Vector3(px, py, pz), cs));
                }

                animTracks.Add(new AnimTrack { Positions = positions, Rotations = rotations });
                boneNames.Add(joint.Name);
            }

            var animSeq = AnimSequence.Create();
            animSeq.CompressedAnimationData = null;
            animSeq.Bones = boneNames;
            animSeq.RawAnimationData = animTracks;
            animSeq.NumFrames = actualFrames;
            animSeq.SequenceLength = actualFrames * frameTime;
            animSeq.RateScale = 1f;
            return animSeq;
        }

        // Inverse of TransformPosition: maps a position from the target coordinate system back to UE3.
        private static Vector3 InverseTransformPosition(Vector3 v, BVHCoordinateSystem cs) => cs switch
        {
            BVHCoordinateSystem.ZUpLeftHanded   => v,
            BVHCoordinateSystem.ZUpRightHanded  => new Vector3(v.X, -v.Y, v.Z),
            BVHCoordinateSystem.YUpLeftHanded   => new Vector3(v.Z, v.X, v.Y),
            BVHCoordinateSystem.YUpRightHanded  => new Vector3(-v.Z, v.X, v.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(cs))
        };
        private static Quaternion ZXYEulerToUE3Quaternion(float rz_deg, float rx_deg, float ry_deg, BVHCoordinateSystem cs)
        {
            float rx = rx_deg * DegToRad;
            float ry = ry_deg * DegToRad;
            float rz = rz_deg * DegToRad;

            float cz = MathF.Cos(rz), sz = MathF.Sin(rz);
            float cx = MathF.Cos(rx), sx = MathF.Sin(rx);
            float cy = MathF.Cos(ry), sy = MathF.Sin(ry);

            var m = new Matrix4x4(
                cz * cy - sz * sx * sy,sz * cy + cz * sx * sy, -cx * sy, 0,
                -sz * cx, cz * cx,sx, 0,
                cz * sy + sz * sx * cy,sz * sy - cz * sx * cy, cx * cy, 0,
                0, 0, 0, 1
            );

            Matrix4x4 ue3m = cs switch
            {
                BVHCoordinateSystem.ZUpLeftHanded => m,

                BVHCoordinateSystem.ZUpRightHanded => new Matrix4x4(
                     m.M11, -m.M12,  m.M13, 0,
                    -m.M21,  m.M22, -m.M23, 0,
                     m.M31, -m.M32,  m.M33, 0,
                     0, 0, 0, 1),

                BVHCoordinateSystem.YUpLeftHanded => new Matrix4x4(
                    m.M33, m.M31, m.M32, 0,
                    m.M13, m.M11, m.M12, 0,
                    m.M23, m.M21, m.M22, 0,
                    0, 0, 0, 1),

                BVHCoordinateSystem.YUpRightHanded => new Matrix4x4(
                     m.M33, -m.M31, -m.M32, 0,
                    -m.M13,  m.M11,  m.M12, 0,
                    -m.M23,  m.M21,  m.M22, 0,
                     0, 0, 0, 1),

                _ => throw new ArgumentOutOfRangeException(nameof(cs))
            };

            return Quaternion.CreateFromRotationMatrix(ue3m);
        }

        private static (float rz, float rx, float ry) ExtractZXYEuler(
            float M11, float M12, float M13,
            float M21, float M22, float M23,
            float M31, float M32, float M33)
        {
            float rx = MathF.Asin(Math.Clamp(M23, -1f, 1f));
            float rz, ry;
            if (MathF.Abs(M23) < 1f - 1e-6f)
            {
                rz = MathF.Atan2(-M21, M22);
                ry = MathF.Atan2(-M13, M33);
            }
            else
            {
                // Gimbal lock: ry and rz share a degree of freedom; collapse into rz.
                rz = MathF.Atan2(M12, M11);
                ry = 0f;
            }
            return (rz * RadToDeg, rx * RadToDeg, ry * RadToDeg);
        }
    }
}
