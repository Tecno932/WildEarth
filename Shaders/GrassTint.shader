Shader "URP/GrassTint"
{
    Properties
    {
        _MainTex("Atlas Texture", 2D) = "white" {}
        _TintColor("Grass Tint Color", Color) = (0.25, 0.85, 0.35, 1)
        _TintStrength("Tint Strength", Range(0,1)) = 1.0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags{ "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // ✅ URP-compatible texture declarations
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _TintColor;
            float _TintStrength;
            float _Cutoff;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 🟢 Leer el color del atlas
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 🧮 Calcular qué tan gris/blanco es el pixel
                half gray = dot(col.rgb, half3(0.333, 0.333, 0.333));

                // 🟢 Generar máscara (solo partes claras se tiñen)
                half mask = saturate((gray - 0.5) * 2.0);

                // 🌿 Mezclar verde según intensidad de gris
                half3 tinted = lerp(col.rgb, _TintColor.rgb * gray, mask * _TintStrength);

                // ❌ Descartar si es transparente
                if (col.a < _Cutoff) discard;

                return half4(tinted, col.a);
            }
            ENDHLSL
        }
    }
}
