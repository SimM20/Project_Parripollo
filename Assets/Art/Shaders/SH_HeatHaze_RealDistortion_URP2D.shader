Shader"Custom/URP2D/HeatHaze_RealDistortion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        _DistortionStrength ("Horizontal Distortion", Range(0, 0.08)) = 0.018
        _VerticalDistortion ("Vertical Distortion", Range(0, 0.04)) = 0.004

        _Speed ("Speed", Range(-3, 3)) = 0.45
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 3.0
        _WaveFrequency ("Wave Frequency", Range(1, 60)) = 16.0

        _Opacity ("Distortion Opacity", Range(0, 1)) = 1.0
        _EdgeFade ("Edge Fade", Range(0.001, 0.49)) = 0.18
        _VerticalPower ("Vertical Shape", Range(0.1, 5)) = 1.35

        _HeatTint ("Heat Tint", Color) = (1, 0.96, 0.88, 1)
        _TintAmount ("Tint Amount", Range(0, 0.25)) = 0.03
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
Name"HeatHazeRealDistortion"

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
    float4 screenPos : TEXCOORD1;
};

sampler2D _MainTex;
sampler2D _NoiseTex;

            // Esta textura la genera el 2D Renderer.
sampler2D _CameraSortingLayerTexture;

float _DistortionStrength;
float _VerticalDistortion;
float _Speed;
float _NoiseScale;
float _WaveFrequency;
float _Opacity;
float _EdgeFade;
float _VerticalPower;
fixed4 _HeatTint;
float _TintAmount;

v2f vert(appdata v)
{
    v2f o;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.color = v.color;
    o.screenPos = ComputeScreenPos(o.vertex);

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
    fixed4 maskTex = tex2D(_MainTex, i.uv) * i.color;

    float mask = GetEdgeFade(i.uv) * maskTex.a * _Opacity;

    float time = _Time.y * _Speed;

    float2 noiseUV1 = i.uv * _NoiseScale;
    noiseUV1 += float2(time * 0.08, time * 1.00);

    float2 noiseUV2 = i.uv * (_NoiseScale * 1.75);
    noiseUV2 += float2(-time * 0.05, time * 1.37);

    float n1 = tex2D(_NoiseTex, noiseUV1).r * 2.0 - 1.0;
    float n2 = tex2D(_NoiseTex, noiseUV2).g * 2.0 - 1.0;

    float waveA = sin((i.uv.y * _WaveFrequency + time * 2.2 + n1 * 1.4) * 6.28318);
    float waveB = sin((i.uv.y * (_WaveFrequency * 0.63) + time * 1.35 + n2 * 1.1) * 6.28318);

    float horizontalWave = waveA * 0.65 + waveB * 0.25 + n1 * 0.25;
    float verticalWave = n2 * 0.65 + waveB * 0.15;

    float2 distortionOffset;
    distortionOffset.x = horizontalWave * _DistortionStrength;
    distortionOffset.y = verticalWave * _VerticalDistortion;

    distortionOffset *= mask;

    float2 screenUV = i.screenPos.xy / i.screenPos.w;

    float2 distortedUV = screenUV + distortionOffset;
    distortedUV = clamp(distortedUV, float2(0.001, 0.001), float2(0.999, 0.999));

    fixed4 distortedScene = tex2D(_CameraSortingLayerTexture, distortedUV);

    distortedScene.rgb = lerp(
                    distortedScene.rgb,
                    distortedScene.rgb * _HeatTint.rgb,
                    _TintAmount * mask
                );

    distortedScene.a = mask;

    return distortedScene;
}

            ENDCG
        }
    }

FallBack"Sprites/Default"
}