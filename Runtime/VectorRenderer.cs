using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class VectorRenderer : MonoBehaviour {

    #region Editable Properties
    [SerializeField] 
    [Range(0.0f, 1.0f)]
    private float radius = 0.3f;
    
    [SerializeField] 
    [Range(0.0f, 1.0f)]
    private float tipHeight = 0.7f;
    #endregion
    
    #region Internal Properties
    private static readonly string VectorMaterialName = "VectorMaterial";
    private static readonly int Capacity = 1023;
    
    private static int HeadProperty = Shader.PropertyToID("_Head");
    private static int TailProperty = Shader.PropertyToID("_Tail");
    private static int ColorProperty = Shader.PropertyToID("_Color");
    private static int RadiusProperty = Shader.PropertyToID("_Radius");
    private static int TipHeightProperty = Shader.PropertyToID("_TipHeight");
    
    [NonSerialized] private Material material;
    [NonSerialized] private Mesh mesh;

    [NonSerialized] private int idx = 0;
    [NonSerialized] private int length = 0;
    
    [NonSerialized] private MaterialPropertyBlock block;
    [NonSerialized] private List<Vector4> heads;
    [NonSerialized] private List<Vector4> tails;
    [NonSerialized] private List<Vector4> colors;
    #endregion

    #region Public API
    public void Begin() {
        idx = 0;
    }

    public void Draw(Vector3 from, Vector3 to, Color color) {
        tails[idx] = from;
        heads[idx] = to;
        colors[idx] = color;
        idx++;
    }

    public void End() {
        length = Math.Min(idx, Capacity);
        UpdateMeshBounds();
        UpdateMaterialPropertyBlock();
        Graphics.DrawMeshInstanced(mesh, 0, material, Matrices().Take(length).ToList(), block);
    }
    #endregion

    #region Unity Event Functions
    void Start() {
        RecreateMaterialAndMesh();
    }

    void OnValidate() {
        RecreateMaterialAndMesh();
    }
    #endregion

    #region Private Functions
    private void RecreateMaterialAndMesh() {
        HeadProperty = Shader.PropertyToID("_Head");
        TailProperty = Shader.PropertyToID("_Tail");
        ColorProperty = Shader.PropertyToID("_Color");
        RadiusProperty = Shader.PropertyToID("_Radius");
        TipHeightProperty = Shader.PropertyToID("_TipHeight");
        
        var vectorMat = Resources.Load<Material>(VectorMaterialName);
        if (vectorMat == null) {
            Debug.LogError($"Failed to locate '{VectorMaterialName}'");
            return;
        }
        material = vectorMat;
        
        mesh = new Mesh();
        UpdateMeshGeometry(mesh);
        
        block = new MaterialPropertyBlock();
        block.SetVectorArray(HeadProperty, heads = new Vector4[Capacity].ToList());
        block.SetVectorArray(TailProperty, tails = new Vector4[Capacity].ToList());
        block.SetVectorArray(ColorProperty, colors = ColorSequence().Take(Capacity).ToList());
        block.SetFloatArray(RadiusProperty, RadiusSequence().Take(Capacity).ToList());
        block.SetFloatArray(TipHeightProperty, TipHeightSequence().Take(Capacity).ToList());
    }

    private static IEnumerable<Vector4> ColorSequence() {
        while (true) {
            yield return Color.red;
            yield return Color.green;
            yield return Color.blue;
            yield return Color.magenta;
            yield return Color.yellow;
            yield return Color.cyan;
        }
    }
    
    private IEnumerable<float> RadiusSequence() {
        while (true) {
            yield return radius;
        }
    }
    
    private IEnumerable<float> TipHeightSequence() {
        while (true) {
            yield return tipHeight;
        }
    }
    
    private IEnumerable<Matrix4x4> Matrices() {
        while (true) {
            yield return Matrix4x4.identity;
        }
    }

    private void UpdateMaterialPropertyBlock() {
        block.SetVectorArray(HeadProperty, heads.Take(length).ToList());
        block.SetVectorArray(TailProperty, tails.Take(length).ToList());
        block.SetVectorArray(ColorProperty, colors.Take(length).ToList());
    }

    private void UpdateMeshBounds() {
        var min = (Vector3) heads[0];
        var max = min;

        for (int i = 0; i < length; i++) {
            var head = heads[i];
            var tail = tails[i];

            if (min.x > head.x) min.x = head.x;
            if (max.x < head.x) max.x = head.x;
            if (min.x > tail.x) min.x = tail.x;
            if (max.x < tail.x) max.x = tail.x;
            
            if (min.y > head.y) min.y = head.y;
            if (max.y < head.y) max.y = head.y;
            if (min.y > tail.y) min.y = tail.y;
            if (max.y < tail.y) max.y = tail.y;
            
            if (min.z > head.z) min.z = head.z;
            if (max.z < head.z) max.z = head.z;
            if (min.z > tail.z) min.z = tail.z;
            if (max.z < tail.z) max.z = tail.z;
        }
        
        mesh.bounds = new Bounds(0.5f * (max - min), max - min);
    }
    
    private static void UpdateMeshGeometry(Mesh mesh) {
        
        var vertices = new List<Vector3>();
        var normals  = new List<Vector3>();
        var colors   = new List<Color>();
        var tris     = new List<int>();
        
        vertices.Add(Vector3.zero); // 0
        normals.Add(Vector3.down);
        colors.Add(Color.black);

        const int nEdges = 10;
        const float iToRad = 2f * Mathf.PI / nEdges;
        for (int i = 0; i < nEdges; i++) {
            float angle = (i + 1) * iToRad;
            var pos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            
            int j = (i + 1) % nEdges * 7 + 1;
            int k = i * 7 + 1;
            
            vertices.Add(pos); // 1
            normals.Add(Vector3.down);
            colors.Add(new Color(.5f, 0f, 0f));
            tris.AddRange(new [] {0, j, k});
            
            vertices.Add(pos); // 2
            normals.Add(pos);
            colors.Add(new Color(.5f, 0f, 0f));
            
            vertices.Add(pos + Vector3.down); // 3
            normals.Add(pos);
            colors.Add(new Color(.5f, 1f, 0f));
            tris.AddRange(new [] {j + 1, j + 2, k + 2});
            tris.AddRange(new [] {j + 1, k + 2, k + 1});
            
            vertices.Add(pos + Vector3.down); // 4
            normals.Add(Vector3.down);
            colors.Add(new Color(.5f, 1f, 0f));

            vertices.Add(pos + Vector3.down); // 5
            normals.Add(Vector3.down);
            colors.Add(new Color(1f, 1f, 0f));
            tris.AddRange(new [] {j + 3, j + 4, k + 4});
            tris.AddRange(new [] {j + 3, k + 4, k + 3});

            Vector3 tipNormal = (pos + Vector3.up).normalized;
            vertices.Add(pos + Vector3.down); // 6
            normals.Add(pos);
            colors.Add(new Color(1f, 1f, 0f));
            
            vertices.Add(Vector3.zero); // 7
            normals.Add(tipNormal); // TODO: Not correct normal
            colors.Add(new Color(1f, 1f, 0f));
            tris.AddRange(new [] {j + 5, j + 6, k + 5});
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.colors = colors.ToArray();
        mesh.triangles = tris.ToArray();
    }
    #endregion
}
