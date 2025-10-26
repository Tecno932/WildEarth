Shader "URP/GrassTint"
{
    Properties
    {
        _MainTex ("Atlas Texture", 2D) = "white" {}
        _TintStrength ("Tint Strength", Range(0,1)) = 1.0
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Off
            Blend One Zero

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _TintStrength;
            float _AlphaCutoff;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(col.a - _AlphaCutoff);

                // --- Calcular diferencia de canales y luminancia
                float diff = abs(col.r - col.g) + abs(col.g - col.b) + abs(col.b - col.r);
                float gray = (col.r + col.g + col.b) / 3.0;

                // --- Detecta solo grises (sin marrones)
                bool isGray = (diff < 0.10 && gray > 0.35 && gray < 0.9);

                if (isGray)
                {
                    // Color verde estilo Minecraft (bioma Plains #91BD59 ≈ 0.57, 0.74, 0.35)
                    float3 minecraftGrass = float3(0.57, 0.74, 0.35);

                    // Leve variación según gris para mantener detalle
                    float variation = saturate((gray - 0.5) * 2.0);
                    float3 grassTint = lerp(minecraftGrass * 0.9, minecraftGrass * 1.1, variation);

                    col.rgb = lerp(col.rgb, grassTint, _TintStrength);
                }

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
