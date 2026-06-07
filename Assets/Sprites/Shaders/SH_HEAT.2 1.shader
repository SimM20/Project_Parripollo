Shader"Custom/URP2D/Grill_Smoke_Visible"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _Tint ("Smoke Color", Color) = (0.75, 0.70, 0.60, 1)

        _Alpha ("Alpha", Range(0,1)) = 0.75
        _Speed ("Speed", Range(-5,5)) = 0.8
        _Scale ("Noise Scale", Range(0.1,30)) = 4.0

        _SoftCut ("Soft Cut", Range(0,1)) = 0.25
        _Contrast ("Contrast", Range(0.1,5)) = 1.4

        _Wobble ("Wobble", Range(0,0.5)) = 0.12
        _EdgeFade ("Edge Fade", Range(0.001,0.5)) = 0.18
        _BottomFade ("Bottom Fade", Range(0.001,0.5)) = 0.04
        _TopFade ("Top Fade", Range(0.001,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
Cull Off

ZWrite Off

ZTest LEqual

Blend SrcAlpha
OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

float4 _MainTex_ST;
float4 _Tint;

float _Alpha;
float _Speed;
float _Scale;
float _SoftCut;
float _Contrast;
float _Wobble;
float _EdgeFade;
float _BottomFade;
float _TopFade;

Varyings vert(Attributes v)
{
    Varyings o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.color = v.color;
    return o;
}

float Fade(float2 uv)
{
    float x =
                    smoothstep(0.0, _EdgeFade, uv.x) *
                    smoothstep(0.0, _EdgeFade, 1.0 - uv.x);

    float y =
                    smoothstep(0.0, _BottomFade, uv.y) *
                    smoothstep(0.0, _TopFade, 1.0 - uv.y);

    return x * y;
}

half4 frag(Varyings i) : SV_Target
{
    float mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;

    float t = _Time.y * _Speed;

    float2 uv = i.uv;

    float wobble =
                    sin(uv.y * 4.0 + t * 1.2) * 0.6 +
                    sin(uv.y * 9.0 - t * 0.7) * 0.4;

    uv.x += wobble * _Wobble * uv.y;

    float2 uv1 = uv * _Scale + float2(0.0, t);
    float2 uv2 = uv * (_Scale * 0.55) + float2(0.31, t * 0.55);
    float2 uv3 = uv * (_Scale * 1.8) + float2(-0.17, t * 1.35);

    float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
    float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
    float n3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv3).r;

    float smoke = n1 * 0.5 + n2 * 0.35 + n3 * 0.15;

    smoke = saturate((smoke - _SoftCut) / max(0.001, 1.0 - _SoftCut));
    smoke = pow(smoke, _Contrast);

    float vertical = pow(saturate(1.0 - i.uv.y), 0.35);
    float alpha = smoke * Fade(i.uv) * vertical * mask * _Alpha;

    float3 col = _Tint.rgb * (0.8 + smoke * 0.3);

    return half4(col, alpha);
}

            ENDHLSL
        }
    }
}