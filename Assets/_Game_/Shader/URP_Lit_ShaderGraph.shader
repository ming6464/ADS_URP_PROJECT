Shader "Custom/URP_Lit_ShaderGraph"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MetallicMap("Metallic Map", 2D) = "black" {}
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _NormalMap("Normal Map", 2D) = "bump" {}
        _HeightMap("Height Map", 2D) = "black" {}
        _OcclusionMap("Occlusion Map", 2D) = "black" {}
        _EmissionMap("Emission Map", 2D) = "black" {}
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            half _Smoothness;
            float4 _EmissionColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv;
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.tangent = TransformObjectToWorldNormal(input.tangent);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, input.uv).r;
                half smoothness = _Smoothness;
                half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, input.uv).r;
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv) * _EmissionColor;

                SurfaceData surfaceData;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.alpha = baseColor.a;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normal = normal.xyz;
                surfaceData.height = height;
                surfaceData.occlusion = occlusion;
                surfaceData.emission = emission.rgb;
                //
                InputData a ;
                half4 color = UniversalFragmentPBR(surfaceData);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
