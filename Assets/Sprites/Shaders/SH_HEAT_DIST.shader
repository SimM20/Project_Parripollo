Shader"Custom/URP2D/HeatHaze_Fake"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}

        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _Tint ("Heat Tint", Color) = (1, 0.75, 0.35, 1)

        _Alpha ("Opacity", Range(0, 1)) = 0.22
        _Speed ("Speed", Range(-3, 3)) = 0.35
        _Scale ("Noise Scale", Range(0.1, 30)) = 4.0

        _WaveAmount ("Wave Amount", Range(0, 1)) = 0.45
        _WaveFrequency ("Wave Frequency", Range(1, 80)) = 18

        _EdgeFade ("Edge Fade", Range(0.001, 0.49)) = 0.16
        _VerticalPower ("Vertical Fade Power", Range(0.1, 5)) = 1.2
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
Name"HeatHazeFake"

            Cull
Off
            ZWrite
Off
            ZTest
LEqual
            Blend
SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

#include "UnityCG.cginc"

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
};

struct v2f
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    fixed4 color : COLOR;
};

sampler2D _MainTex;
sampler2D _NoiseTex;

fixed4 _Tint;

float _Alpha;
float _Speed;
float _Scale;
float _WaveAmount;
float _WaveFrequency;
float _EdgeFade;
float _VerticalPower;

v2f vert(appdata v)
{
    v2f o;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.color = v.color;

    return o;
}

float GetEdgeFade(float2 uv)
{
    float left = smoothstep(0.0, _EdgeFade, uv.x);
    float right = smoothstep(0.0, _EdgeFade, 1.0 - uv.x);
    float bottom = smoothstep(0.0, _EdgeFade, uv.y);
    float top = smoothstep(0.0, _EdgeFade, 1.0 - uv.y);

    float verticalShape = saturate(sin(uv.y * 3.14159265));
    verticalShape = pow(verticalShape, _VerticalPower);

    return left * right * bottom * top * verticalShape;
}

fixed4 frag(v2f i) : SV_Target
{
    fixed4 spriteMask = tex2D(_MainTex, i.uv) * i.color;

    float time = _Time.y * _Speed;

    float2 noiseUV1 = i.uv * _Scale;
    noiseUV1.y += time;

    float2 noiseUV2 = i.uv * (_Scale * 1.7);
    noiseUV2.x += time * 0.2;
    noiseUV2.y += time * 1.35;

    float n1 = tex2D(_NoiseTex, noiseUV1).r;
    float n2 = tex2D(_NoiseTex, noiseUV2).g;

    float noise = n1 * 0.65 + n2 * 0.35;

    float wave = sin((i.uv.y + time * 0.25 + noise * 0.2) * _WaveFrequency);
    wave = wave * 0.5 + 0.5;

    float heatShape = lerp(noise, wave, _WaveAmount);

    float fade = GetEdgeFade(i.uv);

    float finalAlpha = heatShape * fade * spriteMask.a * _Alpha;

    fixed3 finalColor = _Tint.rgb;

    return fixed4(finalColor, finalAlpha);
}

            ENDCG
        }
    }

FallBack"Sprites/Default"
}