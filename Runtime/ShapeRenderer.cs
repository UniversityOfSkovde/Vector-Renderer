using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vectors {
    public sealed class ShapeRenderer : MonoBehaviour, IHasScope {
        
        #region Editable Properties

        [SerializeField] 
        [Tooltip("The layer to use when rendering the vectors")]
        private int layer = 0;

        [SerializeField] 
        [Tooltip("The camera used for rendering. If this is null, then vectors are rendered in all cameras.")] 
        private new Camera camera;
        
        #endregion
        
        #region Internal Properties
        
        private static readonly string ShapeMaterialName = "ShapeMaterial";
        private static readonly int Capacity = 511;
        
        private static int PositionProperty = Shader.PropertyToID("_Position");
        private static int RotationProperty = Shader.PropertyToID("_Rotation");
        private static int ScaleProperty = Shader.PropertyToID("_Scale");
        private static int ColorProperty = Shader.PropertyToID("_Color");

        [NonSerialized] private Material trsMaterial;
        [NonSerialized] private ShapeBatcher cubeBatcher;
        [NonSerialized] private ShapeBatcher cylinderBatcher;

        private sealed class ShapeBatcher {
            
            private readonly List<ShapeBatch> batches;
            private readonly ShapeRenderer owner;
            
            internal readonly Mesh mesh;
            internal readonly MaterialPropertyBlock block;
            internal Material TRSMaterial => owner.trsMaterial;
            internal Camera camera => owner.camera;
            internal int layer => owner.layer;

            private int batchIdx = 0;
            private bool open;

            public ShapeBatcher(ShapeRenderer owner, Mesh mesh) {
                block   = new MaterialPropertyBlock();
                batches = new List<ShapeBatch>();
                this.owner = owner;
                this.mesh = mesh;
            }

            public void Clear() {
                batches.Clear();
                batches.Add(new ShapeBatch(this));
            }

            public void Begin() {
                if (open) {
                    throw new InvalidOperationException("Begin was called twice without ending the current batch!");
                }
                
                open = true;
                batchIdx = 0;
                batches[0].Begin();
            }

            public void End() {
                if (!open) {
                    throw new InvalidOperationException("End was called yet no batch is open.");
                }

                EndCurrentBatch();
                UpdateMeshBounds();
                open = false;
            }
            
            public void Draw(Vector3 position, Quaternion rotation, Vector3 scale, Color color) {
                var batch = EnsureCapacity();
                batch.Draw(position, rotation, scale, color);
            }
            
            private ShapeBatch EnsureCapacity() {
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

                var newBatch = new ShapeBatch(this);
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
                return float.IsInfinity(v.x) || float.IsNaN(v.x)
                    || float.IsInfinity(v.y) || float.IsNaN(v.y)
                    || float.IsInfinity(v.z) || float.IsNaN(v.z);
            }
        }
        
        private sealed class ShapeBatch {

            private readonly ShapeBatcher owner;
            private readonly Vector4[] positions;  // xyz = position, w = unused
            private readonly Vector4[] rotations;  // xyzw = quaternion
            private readonly Vector4[] scales;     // xyz = position, w = unused
            private readonly Vector4[] colors;     // color rgb (a unused)
            private Matrix4x4[] matrices;
            
            private int idx = 0;
            private bool dirty = true;
            private int length;

            public int Length => length;

            public ShapeBatch(ShapeBatcher owner) {
                this.owner = owner;
                
                positions = new Vector4[Capacity];
                rotations = new Vector4[Capacity];
                scales    = new Vector4[Capacity];
                colors    = new Vector4[Capacity];
                
                owner.block.SetVectorArray(PositionProperty, positions);
                owner.block.SetVectorArray(RotationProperty, rotations);
                owner.block.SetVectorArray(ScaleProperty, scales);
                owner.block.SetVectorArray(ColorProperty, colors);
            }

            public void Begin() {
                idx = 0;
            }

            public bool HasCapacity() {
                return idx < Capacity;
            }

            public void Draw(Vector3 pos, Quaternion rot, Vector3 scl, Color color) {
                var packedPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
                var packedRot = new Vector4(rot.x, rot.y, rot.z, rot.w);
                var packedScl = new Vector4(scl.x, scl.y, scl.z, 1.0f);
                if (packedPos != positions[idx] 
                ||  packedRot != rotations[idx] 
                ||  packedScl != scales[idx] 
                ||  colors[idx] != (Vector4) color) {
                    positions[idx] = packedPos;
                    rotations[idx] = packedRot;
                    scales[idx] = packedScl;
                    colors[idx] = color;
                    dirty = true;
                }
                idx++;
            }

            public void End() {
                length = idx;
                if (length == 0) return;
                
                if (dirty) {
                    owner.block.SetVectorArray(PositionProperty, positions);
                    owner.block.SetVectorArray(RotationProperty, rotations);
                    owner.block.SetVectorArray(ScaleProperty, scales);
                    owner.block.SetVectorArray(ColorProperty, colors);

                    if (matrices == null || length > matrices.Length) {
                        matrices = Matrices().Take(length).ToArray();
                    }
                    
                    dirty = false;
                }
                
                Graphics.DrawMeshInstanced(
                    owner.mesh,      // The mesh
                    0,               // Submesh index
                    owner.TRSMaterial,  // The material
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
                    var min = (Vector3) positions[0];
                    var max = min;

                    for (int i = 0; i < length; i++) {
                        var p = (Vector3) positions[i];
                        var r = rotations[i];
                        var s = 0.5f * (Vector3) scales[i];
                        var b = new Quaternion(r.x, r.y, r.z, r.w) * s;
                        var p0 = p + b;
                        var p1 = p - b;

                        if (min.x > p0.x) min.x = p0.x;
                        if (max.x < p0.x) max.x = p0.x;
                        if (min.x > p1.x) min.x = p1.x;
                        if (max.x < p1.x) max.x = p1.x;
                
                        if (min.y > p0.y) min.y = p0.y;
                        if (max.y < p0.y) max.y = p0.y;
                        if (min.y > p1.y) min.y = p1.y;
                        if (max.y < p1.y) max.y = p1.y;
                
                        if (min.z > p0.z) min.z = p0.z;
                        if (max.z < p0.z) max.z = p0.z;
                        if (min.z > p1.z) min.z = p1.z;
                        if (max.z < p1.z) max.z = p1.z;
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

        #endregion

        #region Public API
        
        /**
         * Wrap this function call in a 'using' structure so that you don't
         * forget to call 'end()'. Otherwise nothing will be drawn.
         */
        public AutoEnder Begin() {
            if (trsMaterial == null) {
                RecreateMaterialAndMesh();
            }
            cubeBatcher.Begin();
            cylinderBatcher.Begin();
            return new AutoEnder(this);
        }
        
        /**
         * Draws a cube in the scene.
         */
        public void DrawCube(Vector3 position, Color color) {
            cubeBatcher.Draw(position, Quaternion.identity, Vector3.one, color);
        }
        
        /**
         * Draws a cube in the scene.
         */
        public void DrawCube(Vector3 position, Vector3 size, Color color) {
            cubeBatcher.Draw(position, Quaternion.identity, size, color);
        }

        /**
         * Draws a cube in the scene.
         */
        public void DrawCube(Vector3 position, Quaternion rotation, Vector3 size, Color color) {
            cubeBatcher.Draw(position, rotation, size, color);
        }
        
        /**
         * Draws a cylinder in the scene.
         */
        public void DrawCylinder(Vector3 tail, Vector3 head, float radius, Color color) {
            var pos = 0.5f * (head + tail);
            var dir = head - tail;
            var rot = Quaternion.LookRotation(dir.normalized)
                * Quaternion.AngleAxis(90, Vector3.right);
            var scale = new Vector3(radius * 2.0f, dir.magnitude, radius * 2.0f);
            cylinderBatcher.Draw(pos, rot, scale, color);
        }
        
        public void End() {
            cubeBatcher.End();
            cylinderBatcher.End();
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
            PositionProperty = Shader.PropertyToID("_Position");
            RotationProperty = Shader.PropertyToID("_Rotation");
            ScaleProperty    = Shader.PropertyToID("_Scale");
            ColorProperty    = Shader.PropertyToID("_Color");

            if (trsMaterial == null) {
                var shapeMat = Resources.Load<Material>(ShapeMaterialName);
                if (shapeMat == null) {
                    Debug.LogError($"Failed to locate '{ShapeMaterialName}'");
                    return;
                }
                trsMaterial = shapeMat;
            }
            
            if (cubeBatcher == null) {
                var mesh = new Mesh {
                    name = "CubeShape", 
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = IndexFormat.UInt16
                };

                GenerateCubeGeometry(mesh);
                cubeBatcher = new ShapeBatcher(this, mesh);
            }
            
            if (cylinderBatcher == null) {
                var mesh = new Mesh {
                    name = "CylinderShape", 
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = IndexFormat.UInt16
                };

                GenerateCylinderGeometry(mesh, 24);
                cylinderBatcher = new ShapeBatcher(this, mesh);
            }
            
            cubeBatcher.Clear();
            cylinderBatcher.Clear();
        }
        
        private static void GenerateCubeGeometry(Mesh mesh) {
            
            var vertices = new List<Vector3>();
            var normals  = new List<Vector3>();
            var tris     = new List<int>();
            
            var sides = new [] {
                Quaternion.identity,
                Quaternion.AngleAxis(90, Vector3.right),
                Quaternion.AngleAxis(180, Vector3.right),
                Quaternion.AngleAxis(270, Vector3.right),
                Quaternion.AngleAxis(90, Vector3.up),
                Quaternion.AngleAxis(270, Vector3.up)
            };

            var vertCount = 0;
            foreach (var side in sides) {
                vertices.AddRange(new [] {
                    side * new Vector3(-0.5f, -0.5f, -0.5f), 
                    side * new Vector3(-0.5f,  0.5f, -0.5f), 
                    side * new Vector3( 0.5f,  0.5f, -0.5f), 
                    side * new Vector3( 0.5f, -0.5f, -0.5f) 
                });
                normals.AddRange(new [] {
                    side * new Vector3(0, 0, -1),
                    side * new Vector3(0, 0, -1),
                    side * new Vector3(0, 0, -1),
                    side * new Vector3(0, 0, -1)
                });
                tris.AddRange(new [] {
                    0, 1, 2, 0, 2, 3
                }.Select(i => i + vertCount));
                vertCount += 4;
            }
            
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds(MeshUpdateFlags.Default);
        }
        
        private static void GenerateCylinderGeometry(Mesh mesh, int slices) {
            
            var vertices = new List<Vector3>();
            var normals  = new List<Vector3>();
            var tris     = new List<int>();
            
            vertices.Add(new Vector3(0.0f, 0.5f, 0.0f));  // 0 top-center
            vertices.Add(new Vector3(0.0f, -0.5f, 0.0f)); // 1 bottom-center
            vertices.Add(new Vector3(0.5f, 0.5f, 0.0f));  // 2 top-edge
            vertices.Add(new Vector3(0.5f, -0.5f, 0.0f)); // 3 bottom-edge
            vertices.Add(new Vector3(0.5f, 0.5f, 0.0f));  // 4 side-top
            vertices.Add(new Vector3(0.5f, -0.5f, 0.0f)); // 5 side-bottom
            normals.Add(Vector3.up);
            normals.Add(Vector3.down);
            normals.Add(Vector3.up);
            normals.Add(Vector3.down);
            normals.Add(Vector3.right);
            normals.Add(Vector3.right);
            
            for (var i = 0; i < slices; i++) {
                var cosT = Mathf.Cos(Mathf.PI * 2.0f / slices * (i + 1));
                var sinT = Mathf.Sin(Mathf.PI * 2.0f / slices * (i + 1));
                
                var top = new Vector3(cosT * .5f, .5f, sinT * .5f);
                var bottom = new Vector3(cosT * .5f, -.5f, sinT * .5f);
                var normal = new Vector3(cosT, 0.0f, sinT);
                
                vertices.Add(top);    // 6
                vertices.Add(bottom); // 7
                vertices.Add(top);    // 8
                vertices.Add(bottom); // 9
                
                normals.Add(Vector3.up);
                normals.Add(Vector3.down);
                normals.Add(normal);
                normals.Add(normal);
                
                tris.AddRange(new [] {
                    0, 6 + i * 4, 2 + i * 4,
                    1, 3 + i * 4, 7 + i * 4,
                    4 + i * 4, 8 + i * 4, 5 + i * 4,
                    5 + i * 4, 8 + i * 4, 9 + i * 4
                });
            }

            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds(MeshUpdateFlags.Default);
        }
        #endregion
    }
}
