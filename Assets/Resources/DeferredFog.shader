// Scene fog (RenderSettings, exponential) for the deferred camera: built-in deferred shading skips fog on
// opaque objects, so DeferredFog.cs applies it from the depth buffer after the opaque pass. Sky is left clear,
// like forward fog. Transparent objects still fog themselves in their forward pass.
Shader "Hidden/OxfordRunner/DeferredFog"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
            #if defined(UNITY_REVERSED_Z)
                if (raw <= 0.000001) return c;   // sky
            #else
                if (raw >= 0.999999) return c;
            #endif
                float z = LinearEyeDepth(raw);
                float keep = saturate(exp2(-unity_FogParams.y * z));   // exp fog: y = density / ln 2
                c.rgb = lerp(unity_FogColor.rgb, c.rgb, keep);
                return c;
            }
            ENDCG
        }
    }
}
