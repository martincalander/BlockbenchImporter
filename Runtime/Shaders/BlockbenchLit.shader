Shader "Blockbench/Lit"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull [_Cull]

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;
        half _Cutoff;

        struct Attributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        fixed4 SampleSurface(float2 uv)
        {
            fixed4 color = tex2D(_MainTex, uv) * _Color;
            #if defined(_ALPHATEST_ON)
                clip(color.a - _Cutoff);
            #endif
            return color;
        }

        #if !defined(UNITY_PASS_SHADOWCASTER)
        #include "Lighting.cginc"
        #include "AutoLight.cginc"

        struct Varyings
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 worldPosition : TEXCOORD1;
            half3 worldNormal : TEXCOORD2;
            LIGHTING_COORDS(3, 4)
            UNITY_FOG_COORDS(5)
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes v)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(Varyings, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.pos = UnityObjectToClipPos(v.vertex);
            output.uv = TRANSFORM_TEX(v.uv, _MainTex);
            output.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
            output.worldNormal = UnityObjectToWorldNormal(v.normal);
            TRANSFER_VERTEX_TO_FRAGMENT(output);
            UNITY_TRANSFER_FOG(output, output.pos);
            return output;
        }

        fixed4 Frag(Varyings input, fixed facing : VFACE) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            fixed4 color = SampleSurface(input.uv);
            half3 normal = normalize(input.worldNormal) * (facing >= 0 ? 1 : -1);
            half3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
            UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
            half3 lighting = _LightColor0.rgb * saturate(dot(normal, lightDirection)) * attenuation;
            #if !defined(UNITY_PASS_FORWARDADD)
                lighting += max(half3(0, 0, 0), ShadeSH9(half4(normal, 1)));
            #endif
            color = fixed4(color.rgb * lighting, 1);
            #if defined(UNITY_PASS_FORWARDADD)
                UNITY_APPLY_FOG_COLOR(input.fogCoord, color, fixed4(0, 0, 0, 0));
            #else
                UNITY_APPLY_FOG(input.fogCoord, color);
            #endif
            return color;
        }
        #endif
        ENDCG

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            ENDCG
        }

        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct ShadowVaryings
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes v)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(ShadowVaryings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = TRANSFORM_TEX(v.uv, _MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(output);
                return output;
            }

            float4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SampleSurface(input.uv);
                SHADOW_CASTER_FRAGMENT(input);
            }
            ENDCG
        }
    }
}
