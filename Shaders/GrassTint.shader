Shader "URP/GrassTintFixed"
{
    Properties
    {
        _BaseMap ("Atlas Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.57, 0.74, 0.35, 1)
        _TintStrength ("Tint Strength", Range(0,1)) = 1.0
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.01

        // Ajustes de detección
        _GrayTolerance ("Gray tolerance", Range(0,1)) = 0.25
        _MinBrightness ("Min Brightness", Range(0,1)) = 0.20
        _MaxBrightness ("Max Brightness", Range(0,1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _TintColor;
            float _TintStrength;
            float _AlphaCutoff;
            float _GrayTolerance;
            float _MinBrightness;
            float _MaxBrightness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                clip(col.a - _AlphaCutoff);

                // --- Cálculo de diferencia entre canales ---
                float3 rgb = col.rgb;
                float diff = abs(rgb.r - rgb.g) + abs(rgb.g - rgb.b) + abs(rgb.b - rgb.r);

                // --- Promedio de brillo ---
                float brightness = (rgb.r + rgb.g + rgb.b) / 3.0;

                // --- Detección más permisiva ---
                bool couldBeGrass = (diff < _GrayTolerance) && 
                                    (brightness > _MinBrightness) && 
                                    (brightness < _MaxBrightness);

                if (couldBeGrass)
                {
                    // Variación suave según brillo
                    float variation = saturate((brightness - 0.5) * 1.5);
                    float3 grassTint = lerp(_TintColor.rgb * 0.85, _TintColor.rgb * 1.15, variation);

                    // Aplicar color
                    col.rgb = lerp(col.rgb, grassTint, _TintStrength);
                }

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
