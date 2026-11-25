using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Импортирует 3D-модель Слушателя (listener_mesh.bytes + 4K PBR текстуры)
    /// в нативные ассеты Unity (Listener_Mesh.asset и Mat_Listener_Model.mat).
    /// </summary>
    public static class ListenerModelSetup
    {
        private const string Folder = "Assets/_Project/Art/Models/Listener";
        private const string MeshBytesPath = Folder + "/listener_mesh.bytes";
        private const string MeshAssetPath = Folder + "/Listener_Mesh.asset";
        private const string MatAssetPath = Folder + "/Mat_Listener_Model.mat";

        public static bool LoadOrBuild(out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;

            if (!File.Exists(MeshBytesPath))
            {
                return false;
            }

            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (mesh == null)
            {
                mesh = BuildMeshAsset();
            }

            material = BuildMaterialAsset();
            return mesh != null && material != null;
        }

        private static Mesh BuildMeshAsset()
        {
            byte[] data = File.ReadAllBytes(MeshBytesPath);
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            int vertCount = br.ReadInt32();
            int idxCount = br.ReadInt32();

            var verts = new Vector3[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            }

            var normals = new Vector3[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                normals[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            }

            var uvs = new Vector2[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
            }

            var indices = new int[idxCount];
            for (int i = 0; i < idxCount; i++)
            {
                indices[i] = (int)br.ReadUInt32();
            }

            Mesh m = new Mesh();
            m.name = "Listener_HorrorMonster";
            m.indexFormat = IndexFormat.UInt32;
            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.SetTriangles(indices, 0, true);
            m.RecalculateBounds();
            m.RecalculateTangents();
            m.Optimize();

            AssetDatabase.CreateAsset(m, MeshAssetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
        }

        private static Material BuildMaterialAsset()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatAssetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatAssetPath);
            }
            else
            {
                mat.shader = shader;
            }

            string albedoPath = Folder + "/listener_albedo.jpg";
            string normalPath = Folder + "/listener_normal.jpg";
            string maskPath = Folder + "/listener_mask.png";

            if (File.Exists(normalPath))
            {
                TextureImporter ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
            }

            if (File.Exists(maskPath))
            {
                TextureImporter mi = AssetImporter.GetAtPath(maskPath) as TextureImporter;
                if (mi != null && mi.sRGBTexture)
                {
                    mi.sRGBTexture = false;
                    mi.SaveAndReimport();
                }
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            if (normal != null)
            {
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.15f);
            }

            if (mask != null)
            {
                if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1.0f);
                if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1.0f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
