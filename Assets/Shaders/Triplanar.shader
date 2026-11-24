// World-space triplanar texture (no UVs needed, so stretched boxes tile evenly), tinted by _Color.
Shader "OxfordRunner/Triplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Texture (tiling, mostly light grey)", 2D) = "white" {}
        _Scale ("Metres per tile", Float) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        float _Scale;
        half _Glossiness;
        struct Input { float3 worldPos; float3 worldNormal; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.worldNormal), 4);
            w /= (w.x + w.y + w.z);
            float3 p = IN.worldPos / _Scale;
            fixed3 c = tex2D(_MainTex, p.zy).rgb * w.x + tex2D(_MainTex, p.xz).rgb * w.y + tex2D(_MainTex, p.xy).rgb * w.z;
            o.Albedo = c * _Color.rgb;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
