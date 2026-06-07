Shader"Custom/URP2D/HeatWaves_Fake_NoScene"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _Tint ("Tint", Color) = (1.0, 0.82, 0.55, 1.0)

        _Alpha ("Alpha", Range(0,1)) = 0.45
        _Speed ("Speed", Range(-5,5)) = 1.6
        _NoiseScale ("Noise Scale", Range(0.1,20)) = 6.0

        _BandFrequency ("Band Frequency", Range(1,80)) = 30
        _BandSharpness ("Band Sharpness", Range(0.5,12)) = 5.0

        _XWarp ("Horizontal Warp", Range(0,0.2)) = 0.045
        _YFlow ("Vertical Flow", Range(0,3)) = 0.8

        _EdgeFade ("Edge Fade", Range(0.001,0.49)) = 0.18
        _VerticalPower ("Vertical Shape", Range(0.1,5)) = 1.8

        _Pulse ("Pulse", Range(0,3)) = 1.0
        _Softness ("Softness", Range(0.1,4)) = 1.2
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
Name"HeatWavesFakeNoScene"

            Cull
Off
            ZWrite
Off
            ZTest
LEqual
            Blend
SrcAlpha OneMinusSrcAlpha

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
float _NoiseScale;
float _BandFrequency;
float _BandSharpness;
float _XWarp;
float _YFlow;
float _EdgeFade;
float _VerticalPower;
float _Pulse;
float _Softness;

Varyings vert(Attributes v)
{
    Varyings o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.color = v.color;
    return o;
}

float EdgeFade(float2 uv)
{
    float left = smoothstep(0.0, _EdgeFade, uv.x);
    float right = smoothstep(0.0, _EdgeFade, 1.0 - uv.x);
    float bottom = smoothstep(0.0, _EdgeFade * 0.8, uv.y);
    float top = smoothstep(0.0, _EdgeFade * 1.5, 1.0 - uv.y);

    float verticalShape = pow(saturate(1.0 - uv.y), _VerticalPower);

    return left * right * bottom * top * verticalShape;
}

half4 frag(Varyings i) : SV_Target
{
    half4 spriteMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

    float t = _Time.y * _Speed;

    float2 noiseUV1 = i.uv * _NoiseScale + float2(0.0, t * _YFlow);
    float2 noiseUV2 = i.uv * (_NoiseScale * 1.73) + float2(t * 0.35, t * 1.25);

    float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV1).r;
    float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV2).g;

    float noise = lerp(n1, n2, 0.45);
    float centeredNoise = noise * 2.0 - 1.0;

    float xWarp = centeredNoise * _XWarp;

    float wavA = sin((i.uv.y + xWarp + noise * 0.10 + t * 0.90) * _BandFrequency);
    float wavB = sin((i.uv.y - xWarp * 0.6 + noise * 0.18 + t * 1.25) * (_BandFrequency * 0.62));
    float wavC = sin((i.uv.y + i.uv.x * 0.35 + t * 0.75) * (_BandFrequency * 1.35));

    float bandsA = pow(saturate(abs(wavA)), _BandSharpness);
    float bandsB = pow(saturate(abs(wavB)), _BandSharpness * 0.8);
    float bandsC = pow(saturate(abs(wavC)), _BandSharpness * 1.1);

    float bands = bandsA * 0.55 + bandsB * 0.25 + bandsC * 0.20;
    bands = pow(saturate(bands), _Softness);

    float shimmer = 0.75 + 0.25 * sin(t * 5.0 + i.uv.y * 18.0 + centeredNoise * 3.0);
    float fade = EdgeFade(i.uv);

    float finalAlpha = bands * shimmer * fade * spriteMask.a * _Alpha;

    float3 finalColor = _Tint.rgb * (0.75 + bands * 0.45 + shimmer * 0.12);

    return half4(finalColor, finalAlpha);
}
            ENDHLSL
        }
    }

FallBack"Sprites/Default"
}