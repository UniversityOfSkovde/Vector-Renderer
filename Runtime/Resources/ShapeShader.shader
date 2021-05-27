Shader "Unlit/ShapeShader"
{
    Properties
    {
        [PerRendererData] _Position ("Position", Vector) = (0, 0, 0, 1)         // XYZ (W not currently used)
        [PerRendererData] _Rotation ("Rotation", Vector) = (0, 0, 0, 1)         // Quaternion, 4 components
        [PerRendererData] _Scale ("Scale", Vector)       = (1, 1, 1, 1)         // XYZ (W not currently used)
        [PerRendererData] _Color ("Color", Color)        = (0.2, 0.2, 0.2, 1)   // RGB (alpha not currently used)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata {
                float3 position : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Position)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Rotation)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Scale)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Create a matrix from the direction
                float4 positionPacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Position);
                float4 scalePacked = UNITY_ACCESS_INSTANCED_PROP(Props, _Scale);
                float4 q = UNITY_ACCESS_INSTANCED_PROP(Props, _Rotation);
                float q0 = q.w;
                float q1 = q.x;
                float q2 = q.y;
                float q3 = q.z;

                // Quaternion to matrix
                float3x3 orient = float3x3(
                    q0*q0 + q1*q1 - q2*q2 - q3*q3, 2.0 * (q1*q2 - q0*q3),         2.0 * (q1*q3 + q0*q2),
                    2.0 * (q0*q3 + q1*q2),         q0*q0 - q1*q1 + q2*q2 - q3*q3, 2.0 * (q2*q3 - q0*q1),
                    2.0 * (q1*q3 - q0*q2),         2.0 * (q0*q1 + q2*q3),         q0*q0 - q1*q1 - q2*q2 + q3*q3
                );

                float3 pos = mul(orient, float3(
                    v.position.x * scalePacked.x,
                    v.position.y * scalePacked.y,
                    v.position.z * scalePacked.z
                )) + positionPacked.xyz;

                float3 nor = mul(orient, v.normal.xyz);
                
                o.vertex = UnityObjectToClipPos(pos);
                o.viewDir = normalize(pos - _WorldSpaceCameraPos);
                o.normal = nor;
                
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 normal = normalize(-i.normal);
                float3 viewDir = normalize(i.viewDir);
                float diffuse = clamp(dot(normal, viewDir), 0.0f, 1.0f);
                col = lerp(0.5f * col, col, diffuse);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
