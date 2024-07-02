Shader "Horus/Lit/Decoration"
{
    Properties
    {
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _MetallicGlossMap("Metallic (R) Gloss (A) Map", 2D) = "white" {}
        _Normal("Normal Map", 2D) = "bump" {}
        _DecorGlossiness("Decor Smoothness", Range(0, 1)) = 0.5
        _DecorMetallic("Decor Metallic", Range(0,1)) = 0.0
        _Decor("Decoration", 2D) = "white" {}
        _Mask("Decoration Mask", 2D) = "black" {}
        _MulStrength("Multiply Strength", Range(-10, 10)) = 1.0
        [Enum(Multiply, 1, Blend, 0)] _MulOrBlend("Color Mix Mode", Float) = 1
        _Opacity("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }
        LOD 500

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows

        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _MetallicGlossMap;
        sampler2D _Normal;
        sampler2D _Decor;
        sampler2D _Mask;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_Normal;
            float2 uv_Decor;
            float2 uv_Mask;
        };

        half4 _Color;
        half _Glossiness;
        half _Metallic;
        half _DecorGlossiness;
        half _DecorMetallic;
        half _Opacity;
        half _MulStrength;
        fixed _MulOrBlend;
        
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            c = c * _Color;
            fixed4 decor = tex2D(_Decor, IN.uv_Decor);
            const fixed decor_mask = tex2D(_Mask, IN.uv_Mask).r;
            const half mask = _Opacity * decor_mask * decor.a;

            decor = decor * decor_mask;

            const fixed4 fully_decor = lerp(decor * _MulStrength, c * decor * _MulStrength, _MulOrBlend);
            c = lerp(c, fully_decor, mask);

            o.Albedo = c.rgb;

            const half metallic = lerp(_Metallic, _DecorMetallic, mask);
            const half glossiness = lerp(_Glossiness, _DecorGlossiness, mask);
            const float4 metallic_glossiness = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = metallic * metallic_glossiness.r;
            o.Smoothness = glossiness * metallic_glossiness.a;
            o.Alpha = c.a;
            fixed3 normal = UnpackNormal(tex2D(_Normal, IN.uv_Normal));
            o.Normal = normal.rgb;
        }
        ENDCG
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        UsePass "Horus/Unlit/Decoration/MAIN"
    }
    FallBack "Diffuse"
}