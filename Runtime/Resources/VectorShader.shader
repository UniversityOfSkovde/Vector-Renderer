﻿Shader "Unlit/VectorShader"
{
    Properties
    {
        [PerRendererData] _Head ("Head", Vector) = (0, 1, 0, 0.3)
        [PerRendererData] _Tail ("Tail", Vector) = (0, 0, 0, 0.5)
        [PerRendererData] _Color ("Color", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector" = "True" "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline" "ShaderModel"="4.5" }
        LOD 100
        
        Blend One Zero
        ZWrite [_ZWrite]
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Head)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tail)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Create a matrix from the direction
                float4 headPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Head);
                float4 tailPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Tail);
                float3 head = headPacked.xyz;
                float3 tail = tailPacked.xyz;
                float3 dir = head - tail;

                float radius    = headPacked.w;
                float tipHeight = tailPacked.w;
                
                float3 z = normalize(dir);
                float3 y = float3(0, 1, 0);
                float3 x = float3(1, 0, 0);
                if (abs(dot(z, float3(0, 1, 0))) > 0.99f) {
                    y = normalize(cross(z, x));
                    x = normalize(cross(y, z));
                } else {
                    x = normalize(cross(float3(0, 1, 0), z));
                    y = normalize(cross(z, x));
                }

                x *= radius * v.color.x;
                y *= radius * v.color.x;

                float3x3 orient = {
                    x.x, z.x, y.x,
                    x.y, z.y, y.y,
                    x.z, z.z, y.z
                };

                float3 pos = mul(orient, float3(v.position.x, v.position.y * tipHeight, v.position.z)) +
                    tail + dir * v.color.y;

                float3 nor = mul(orient, float3(v.normal.x, v.normal.y * tipHeight, v.normal.z));
                
                o.vertex = UnityObjectToClipPos(pos);
                o.viewDir = normalize(pos - _WorldSpaceCameraPos);
                o.normal = nor;
                
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // sample the texture
                fixed4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 normal = normalize(-i.normal);
                float3 viewDir = normalize(i.viewDir);
                float diffuse = clamp(dot(normal, viewDir), 0.0f, 1.0f);
                col = lerp(0.5f * col, col, diffuse);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                UNITY_OPAQUE_ALPHA(col.a);
                return col;
            }
            ENDCG
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex vert_depth
            #pragma fragment frag_depth

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "UnityCG.cginc"
            
            struct depth_appdata {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct depth_v2f {
                float4 vertex : SV_POSITION;
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Head)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tail)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            depth_v2f vert_depth (depth_appdata v) {
                depth_v2f o;

                UNITY_SETUP_INSTANCE_ID(v);

                // Create a matrix from the direction
                float4 headPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Head);
                float4 tailPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Tail);
                float3 head = headPacked.xyz;
                float3 tail = tailPacked.xyz;
                float3 dir = head - tail;

                float radius    = headPacked.w;
                float tipHeight = tailPacked.w;
                
                float3 z = normalize(dir);
                float3 y = float3(0, 1, 0);
                float3 x = float3(1, 0, 0);
                if (abs(dot(z, float3(0, 1, 0))) > 0.99f) {
                    y = normalize(cross(z, x));
                    x = normalize(cross(y, z));
                } else {
                    x = normalize(cross(float3(0, 1, 0), z));
                    y = normalize(cross(z, x));
                }

                x *= radius * v.color.x;
                y *= radius * v.color.x;

                float3x3 orient = {
                    x.x, z.x, y.x,
                    x.y, z.y, y.y,
                    x.z, z.z, y.z
                };

                float3 pos = mul(orient, float3(v.position.x, v.position.y * tipHeight, v.position.z)) +
                    tail + dir * v.color.y;

                o.vertex = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag_depth (depth_v2f i) : SV_Target
            {
                return 0;
            }
            
            ENDCG
        }
        
        Pass
        {
            Name "DepthNormalsOnly"
            Tags{"LightMode" = "DepthNormalsOnly"}

            ZWrite On

            CGPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex vert_depth_normals
            #pragma fragment frag_depth_normals

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "UnityCG.cginc"
            
            struct depth_normals_appdata {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct depth_normals_v2f {
                float4 vertex : SV_POSITION;
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Head)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tail)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            depth_normals_v2f vert_depth_normals (depth_normals_appdata v) {
                depth_normals_v2f o;

                UNITY_SETUP_INSTANCE_ID(v);

                // Create a matrix from the direction
                float4 headPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Head);
                float4 tailPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Tail);
                float3 head = headPacked.xyz;
                float3 tail = tailPacked.xyz;
                float3 dir = head - tail;

                float radius    = headPacked.w;
                float tipHeight = tailPacked.w;
                
                float3 z = normalize(dir);
                float3 y = float3(0, 1, 0);
                float3 x = float3(1, 0, 0);
                if (abs(dot(z, float3(0, 1, 0))) > 0.99f) {
                    y = normalize(cross(z, x));
                    x = normalize(cross(y, z));
                } else {
                    x = normalize(cross(float3(0, 1, 0), z));
                    y = normalize(cross(z, x));
                }

                x *= radius * v.color.x;
                y *= radius * v.color.x;

                float3x3 orient = {
                    x.x, z.x, y.x,
                    x.y, z.y, y.y,
                    x.z, z.z, y.z
                };

                float3 pos = mul(orient, float3(v.position.x, v.position.y * tipHeight, v.position.z)) +
                    tail + dir * v.color.y;

                o.vertex = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag_depth_normals (depth_normals_v2f i) : SV_Target
            {
                return 0;
            }
            
            ENDCG
        }
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Head)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tail)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Create a matrix from the direction
                float4 headPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Head);
                float4 tailPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Tail);
                float3 head = headPacked.xyz;
                float3 tail = tailPacked.xyz;
                float3 dir = head - tail;

                float radius    = headPacked.w;
                float tipHeight = tailPacked.w;
                
                float3 z = normalize(dir);
                float3 y = float3(0, 1, 0);
                float3 x = float3(1, 0, 0);
                if (abs(dot(z, float3(0, 1, 0))) > 0.99f) {
                    y = normalize(cross(z, x));
                    x = normalize(cross(y, z));
                } else {
                    x = normalize(cross(float3(0, 1, 0), z));
                    y = normalize(cross(z, x));
                }

                x *= radius * v.color.x;
                y *= radius * v.color.x;

                float3x3 orient = {
                    x.x, z.x, y.x,
                    x.y, z.y, y.y,
                    x.z, z.z, y.z
                };

                float3 pos = mul(orient, float3(v.position.x, v.position.y * tipHeight, v.position.z)) +
                    tail + dir * v.color.y;

                float3 nor = mul(orient, float3(v.normal.x, v.normal.y * tipHeight, v.normal.z));
                
                o.vertex = UnityObjectToClipPos(pos);
                o.viewDir = normalize(pos - _WorldSpaceCameraPos);
                o.normal = nor;
                
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // sample the texture
                fixed4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 normal = normalize(-i.normal);
                float3 viewDir = normalize(i.viewDir);
                float diffuse = clamp(dot(normal, viewDir), 0.0f, 1.0f);
                col = lerp(0.5f * col, col, diffuse);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                UNITY_OPAQUE_ALPHA(col.a);
                return col;
            }
            ENDCG
        }
    }
}
