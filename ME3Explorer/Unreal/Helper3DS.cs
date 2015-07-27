using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using lib3ds.Net;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.Unreal
{
    public static class Helper3DS
    {
        public static string loc;

        public static Lib3dsFile EmptyFile()
        {
            return LIB3DS.lib3ds_file_open(loc + "\\exec\\cube.3ds");
        }

        public static void ConvertPSKto3DS(PSKFile f, string path)
        {
            Lib3dsFile res = EmptyFile();
            AddMeshTo3DS(res, f, Matrix.Identity);
            ClearFirstMesh(res);
            if (!LIB3DS.lib3ds_file_save(res, path))
                MessageBox.Show("Error!");
        }

        public static void ClearFirstMesh(Lib3dsFile f)
        {
            for (int i = 0; i < f.meshes[0].nvertices; i++)
                f.meshes[0].vertices[i] = new Lib3dsVertex();
        }

        public static void AddMeshTo3DS(Lib3dsFile res, PSKFile f, Matrix m)
        {
            Lib3dsMesh mesh = new Lib3dsMesh();
            string name =  "Box00" + res.meshes.Count.ToString();
            mesh.name = name;
            mesh.matrix = Matrix2FA(Matrix.Identity);
            mesh.vertices = new List<Lib3dsVertex>();
            foreach (PSKFile.PSKPoint p in f.psk.points)
            {
                Vector3 v = p.ToVector3();
                v = Vector3.TransformCoordinate(v, m);
                mesh.vertices.Add(new Lib3dsVertex(v.X, -v.Y, v.Z));
            }
            mesh.texcos = new List<Lib3dsTexturecoordinate>();
            for (int i = 0; i < f.psk.points.Count; i++)
                foreach (PSKFile.PSKEdge e in f.psk.edges)
                    if (e.index == i)
                        mesh.texcos.Add(new Lib3dsTexturecoordinate(e.U, e.V));
            mesh.faces = new List<Lib3dsFace>();
            foreach (PSKFile.PSKFace face in f.psk.faces)
            {
                Lib3dsFace ff = new Lib3dsFace();
                ff.flags = 6;
                ff.index = new ushort[3];
                ff.index[0] = (ushort)f.psk.edges[face.v0].index;
                ff.index[1] = (ushort)f.psk.edges[face.v2].index;
                ff.index[2] = (ushort)f.psk.edges[face.v1].index;
                mesh.faces.Add(ff);
            }
            mesh.nfaces = (ushort)mesh.faces.Count;
            mesh.nvertices = (ushort)mesh.vertices.Count;
            mesh.map_type = Lib3dsMapType.LIB3DS_MAP_NONE;
            mesh.object_flags = 0;
            mesh.color = 128;
            res.meshes.Add(mesh);
            Lib3dsNode node = new Lib3dsMeshInstanceNode();
            node.matrixNode = Matrix2FA(Matrix.Identity);
            node.parent = null;
            node.parent_id = 0xffff;
            node.hasNodeID = true;
            node.type = Lib3dsNodeType.LIB3DS_NODE_MESH_INSTANCE;
            node.flags = res.nodes[0].flags;
            node.node_id = (ushort)(res.meshes.Count() - 1);
            node.name = name;            
            res.nodes.Add(node);
        }

        public static Matrix FloatArray2M(float[,] m)
        {
            Matrix res = new Matrix();
            res.M11 = m[0, 0];
            res.M21 = m[0, 1];
            res.M31 = m[0, 2];
            res.M41 = m[0, 3];

            res.M12 = m[1, 0];
            res.M22 = m[1, 1];
            res.M32 = m[1, 2];
            res.M42 = m[1, 3];

            res.M13 = m[2, 0];
            res.M23 = m[2, 1];
            res.M33 = m[2, 2];
            res.M43 = m[2, 3];

            res.M14 = m[3, 0];
            res.M24 = m[3, 1];
            res.M34 = m[3, 2];
            res.M44 = m[3, 3];
            return res;
        }

        public static float[,] Matrix2FA(Matrix m)
        {
            float[,] res = new float[4, 4];
            res[0, 0] = m.M11;
            res[0, 1] = m.M12;
            res[0, 2] = m.M13;
            res[0, 3] = m.M14;

            res[1, 0] = m.M21;
            res[1, 1] = m.M22;
            res[1, 2] = m.M23;
            res[1, 3] = m.M24;

            res[2, 0] = m.M31;
            res[2, 1] = m.M32;
            res[2, 2] = m.M33;
            res[2, 3] = m.M34;

            res[3, 0] = m.M41;
            res[3, 1] = m.M42;
            res[3, 2] = m.M43;
            res[3, 3] = m.M44;
            return res;
        }
    }
}
