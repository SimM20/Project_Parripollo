// Humo continuo de corte quemado. Pensado para la parrilla 2.5D:
// el quad se ancla con pivot abajo sobre la carne y la columna sube y se abre con la altura.
// Todo el ruido es procedural (sin _NoiseTex) para no depender del wrap mode de una textura.
Shader "Custom/URP2D/Grill_Smoke_Visible"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        _Tint ("Smoke Color", Color) = (0.62, 0.60, 0.58, 1)
        _Opacity ("Opacity", Range(0,1)) = 0.42
        _Intensity ("Density", Range(0,3)) = 1.15

        _RiseSpeed ("Rise Speed", Range(0,3)) = 0.55
        _NoiseScale ("Noise Scale", Range(0.5,12)) = 3.1
        _Detail ("Detail", Range(0,1)) = 0.75
        _Contrast ("Contrast", Range(0.5,4)) = 1.45

        _Width ("Base Width", Range(0.02,0.5)) = 0.14
        _Expand ("Expand With Height", Range(0,1)) = 0.32
        _Softness ("Edge Softness", Range(0.05,1)) = 0.6

        _SwayAmount ("Sway", Range(0,0.4)) = 0.1
        _SwaySpeed ("Sway Speed", Range(0,4)) = 0.85
        _Drift ("Horizontal Drift", Range(-0.5,0.5)) = 0.05

        _BottomFade ("Bottom Fade", Range(0,0.6)) = 0.08
        _TopFade ("Top Fade", Range(0.01,1)) = 0.55
        _Dissipate ("Dissipate With Height", Range(0,1)) = 0.7

        _Seed ("Random Seed", Float) = 0

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            float4 _Tint;
            float4 _Color;
            float4 _RendererColor;

            float _Opacity, _Intensity;
            float _RiseSpeed, _NoiseScale, _Detail, _Contrast;
            float _Width, _Expand, _Softness;
            float _SwayAmount, _SwaySpeed, _Drift;
            float _BottomFade, _TopFade, _Dissipate;
            float _Seed;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                return vnoise(p) * 0.55 + vnoise(p * 2.03) * 0.29 + vnoise(p * 4.11) * 0.16;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float h = saturate(uv.y);          // 0 = pegado a la carne, 1 = disipado arriba
                float t = _Time.y;
                float s = _Seed;

                // Serpenteo lateral: crece con la altura, nunca en la base.
                float sway = sin(h * 3.1 + t * _SwaySpeed + s * 6.2831) * 0.65
                           + sin(h * 6.7 - t * _SwaySpeed * 0.63 + s * 3.1) * 0.35;

                float cx = 0.5 + sway * _SwayAmount * h + _Drift * h;

                // Columna que se abre con la altura, con bordes suaves (nada de rectangulo).
                float w = _Width + _Expand * h;
                float d = abs(uv.x - cx) / max(0.001, w);
                float body = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);

                // Bocanadas encadenadas subiendo.
                float2 np = float2(uv.x * _NoiseScale, uv.y * _NoiseScale - t * _RiseSpeed) + s * 17.0;
                float n = fbm(np);
                n = lerp(0.62, n, _Detail);

                float fadeBottom = smoothstep(0.0, max(0.001, _BottomFade), h);
                float fadeTop    = 1.0 - smoothstep(1.0 - _TopFade, 1.0, h);
                float dissipate  = lerp(1.0, 1.0 - _Dissipate, h);

                float a = body * n * _Intensity * dissipate * fadeBottom * fadeTop;
                a = pow(saturate(a), _Contrast) * _Opacity * i.color.a;

                float3 col = _Tint.rgb * (0.85 + n * 0.3);
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
