using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vectors {
    public sealed class VectorRenderer : MonoBehaviour, IHasScope {
        
        #region Editable Properties
        [SerializeField] 
        [Range(0.0f, 1.0f)]
        [Tooltip("The radius of the vectors (this value is overridden if a value is sent directly to Draw())")]
        private float radius = 0.3f;
        
        [SerializeField]
        [Range(0.0f, 1.0f)]
        [Tooltip("The size of the tip of the vectors (this value is overridden if a value is sent directly to Draw())")]
        private float tipHeight = 0.7f;

        [SerializeField] 
        [Tooltip("The layer to use when rendering the vectors")]
        private int layer = 0;

        [SerializeField] 
        [Tooltip("The camera used for rendering. If this is null, then vectors are rendered in all cameras.")] 
        private new Camera camera;
        
        #endregion
        
        #region Public Properties
        
        /**
         * The total number of vectors drawn across all batches. End() has to
         * have been called for this value to be accurate.
         */
        public int Length => batches.Sum(b => b.Length);
        
        #endregion
        
        #region Internal Properties
        private static readonly string VectorMaterialName = "VectorMaterial";
        private static readonly int Capacity = 511;
        
        private static int HeadProperty = Shader.PropertyToID("_Head");
        private static int TailProperty = Shader.PropertyToID("_Tail");
        private static int ColorProperty = Shader.PropertyToID("_Color");

        [NonSerialized] private MaterialPropertyBlock block;
        [NonSerialized] private Material material;
        [NonSerialized] private Mesh mesh;
        [NonSerialized] private int batchIdx = 0;
        
        private sealed class VectorBatch {

            private readonly VectorRenderer owner;
            private readonly Vector4[] heads;  // xyz = position, w = radius
            private readonly Vector4[] tails;  // xyz = position, w = tip height
            private readonly Vector4[] colors; // color rgb- (alpha not used)
            private Matrix4x4[] matrices;
            
            private int idx = 0;
            private bool dirty = true;
            private int length;

            public int Length => length;

            public VectorBatch(VectorRenderer owner) {
                this.owner = owner;
                
                heads = new Vector4[Capacity];
                tails = new Vector4[Capacity];
                colors = new Vector4[Capacity];
                
                owner.block.SetVectorArray(HeadProperty, heads);
                owner.block.SetVectorArray(TailProperty, tails);
                owner.block.SetVectorArray(ColorProperty, colors);
            }

            public void Begin() {
                idx = 0;
            }

            public bool HasCapacity() {
                return idx < Capacity;
            }

            public void Draw(Vector3 tail, Vector3 head, Color color, float radius, float tip) {
                var packedHead = new Vector4(head.x, head.y, head.z, radius);
                var packedTail = new Vector4(tail.x, tail.y, tail.z, tip);
                if (packedHead != heads[idx] 
                ||  packedTail != tails[idx] 
                ||  colors[idx] != (Vector4) color) {
                    heads[idx] = packedHead;
                    tails[idx] = packedTail;
                    colors[idx] = color;
                    dirty = true;
                }
                idx++;
            }

            public void End() {
                length = idx;
                if (length == 0) return;
                
                if (dirty) {
                    owner.block.SetVectorArray(HeadProperty, heads);
                    owner.block.SetVectorArray(TailProperty, tails);
                    owner.block.SetVectorArray(ColorProperty, colors);

                    if (matrices == null || length > matrices.Length) {
                        matrices = Matrices().Take(length).ToArray();
                    }
                    
                    dirty = false;
                }
                
                Graphics.DrawMeshInstanced(
                //Graphics.DrawMeshInstancedProcedural(
                    owner.mesh,      // The mesh
                    0,               // Submesh index
                    owner.material,  // The material
                    //Bounds,
                    matrices,        // Array of matrices
                    length,          // Number of matrices
                    owner.block,
                    ShadowCastingMode.Off,  // Don't cast shadows
                    false,                  // Don't receive shadows
                    owner.layer,            // Render layer, maybe make this configurable?
                    owner.camera,           // The camera (null = all cameras)
                    LightProbeUsage.Off,    // Don't use light probes
                    null                    // Don't use LightProbeProxyVolume
                );
            }

            public Bounds Bounds {
                get {
                    var min = (Vector3) heads[0];
                    var max = min;

                    for (int i = 0; i < length; i++) {
                        var head = (Vector3) heads[i];
                        var tail = (Vector3) tails[i];

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
            
                    return new Bounds(0.5f * (max + min), (max - min) * 1.1f);
                }
            }

            private static IEnumerable<Matrix4x4> Matrices() {
                while (true) {
                    yield return Matrix4x4.identity;
                }
            }
        }

        [NonSerialized]
        private readonly List<VectorBatch> batches;

        public VectorRenderer() {
            batches = new List<VectorBatch>();
        }
        
        #endregion

        #region Public API
        
        /**
         * Wrap this function call in a 'using' structure so that you don't
         * forget to call 'end()'. Otherwise nothing will be drawn.
         */
        public AutoEnder Begin() {
            if (mesh == null || material == null || block == null) {
                RecreateMaterialAndMesh();
            }
            batchIdx = 0;
            batches[0].Begin();
            return new AutoEnder(this);
        }

        /**
         * Draws a vector in the scene between the two specified world space
         * coordinates.
         * @param from   the point in world coordinates to draw from
         * @param to     the point in world coordinates to draw to
         * @param color  color to use when drawing
         */
        public void Draw(Vector3 from, Vector3 to, Color color) {
            var batch = EnsureCapacity();
            batch.Draw(from, to, color, radius, tipHeight);
        }
        
        /**
         * Draws a vector in the scene between the two specified world space
         * coordinates.
         * @param from    the point in world coordinates to draw from
         * @param to      the point in world coordinates to draw to
         * @param color   color to use when drawing
         * @param radius  radius of the vector
         */
        public void Draw(Vector3 from, Vector3 to, Color color, float radius) {
            var batch = EnsureCapacity();
            batch.Draw(from, to, color, radius, tipHeight);
        }
        
        /**
         * Draws a vector in the scene between the two specified world space
         * coordinates.
         * @param from       the point in world coordinates to draw from
         * @param to         the point in world coordinates to draw to
         * @param color      color to use when drawing
         * @param radius     radius of the vector
         * @param tipHeight  the (relative) length of the tip compared to the
         *                   default vector size
         */
        public void Draw(Vector3 from, Vector3 to, Color color, float radius, float tipHeight) {
            var batch = EnsureCapacity();
            batch.Draw(from, to, color, radius, tipHeight);
        }

        public void End() {
            EndCurrentBatch();
            UpdateMeshBounds();
        }
        #endregion

        #region Unity Event Functions
        void OnEnable() {
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

            if (block == null) {
                block = new MaterialPropertyBlock();
            }
            
            if (material == null) {
                var vectorMat = Resources.Load<Material>(VectorMaterialName);
                if (vectorMat == null) {
                    Debug.LogError($"Failed to locate '{VectorMaterialName}'");
                    return;
                }
                material = vectorMat;
            }
            
            if (mesh == null) {
                mesh = new Mesh();
            }
            
            UpdateMeshGeometry(mesh);
            
            batches.Clear();
            batches.Add(new VectorBatch(this));
            batchIdx = 0;
        }
        
        private VectorBatch EnsureCapacity() {
            if (batchIdx < batches.Count) {
                var currentBatch = batches[batchIdx];
                if (currentBatch.HasCapacity()) {
                    return currentBatch;
                }
                currentBatch.End();
                batchIdx++;
            }

            if (batchIdx < batches.Count) {
                var currentBatch = batches[batchIdx];
                currentBatch.Begin();
                return currentBatch;
            }

            var newBatch = new VectorBatch(this);
            batches.Add(newBatch);
            newBatch.Begin();
            return newBatch;
        }

        private void EndCurrentBatch() {
            if (batchIdx >= batches.Count) return;
            var lastBatch = batches[batchIdx];
            lastBatch.End();
            batchIdx = 0;
        }

        private void UpdateMeshBounds() {
            if (batches.Count == 0) return;
            var bounds = batches[0].Bounds;
            if (!ValidFloats(bounds.min) || !ValidFloats(bounds.max)) {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }
            
            for (int i = 1; i < batches.Count; i++) {
                var bb = batches[i].Bounds;
                
                if (!ValidFloats(bb.min) || !ValidFloats(bb.max))
                    continue;

                bounds.min = new Vector3(
                    Mathf.Min(bounds.min.x, bb.min.x),
                    Mathf.Min(bounds.min.y, bb.min.y),
                    Mathf.Min(bounds.min.z, bb.min.z));
                
                bounds.max = new Vector3(
                    Mathf.Max(bounds.max.x, bb.max.x),
                    Mathf.Max(bounds.max.y, bb.max.y),
                    Mathf.Max(bounds.max.z, bb.max.z));
            }
            
            mesh.bounds = bounds;
        }

        private static bool ValidFloats(Vector3 v) {
            return !(float.IsInfinity(v.x) || float.IsNaN(v.x)
                  || float.IsInfinity(v.y) || float.IsNaN(v.y)
                  || float.IsInfinity(v.z) || float.IsNaN(v.z));
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
                normals.Add(tipNormal); // Not correct normal, but good enough
                colors.Add(new Color(1f, 1f, 0f));
                tris.AddRange(new [] {j + 5, j + 6, k + 5});
            }

            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = tris.ToArray();
        }
        #endregion
    }
}
