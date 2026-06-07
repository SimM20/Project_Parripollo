Shader"Gemini/Pro_Heat_Distortion_Clean"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        
        _Speed ("Scroll Speed", Range(0, 5)) = 2.0
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 2.0
        _DistortionStrength ("Warp Strength", Range(0, 0.1)) = 0.03
        _DistortionSharpness ("Warp Sharpness", Range(1, 10)) = 2.0
        _EdgeFade ("Edge Fade", Range(0.001, 0.49)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1" 
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

Blend SrcAlpha
OneMinusSrcAlpha
        Cull
Off
        ZWriteOff

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 screenPos : TEXCOORD0;
    float2 uv : TEXCOORD1;
    half4 color : COLOR;
};

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
float _Speed;
float _NoiseScale;
float _DistortionStrength;
float _DistortionSharpness;
float _EdgeFade;
CBUFFER_END

            Varyingsvert(
Attributes input)
            {
Varyings output;
VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                output.positionCS = vertexInput.
positionCS;
                output.uv = input.
uv;
                output.color = input.
color;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                
                return
output;
            }

half4 frag(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float3 time = _Time.y * _Speed;

    float2 noiseUV = uv * _NoiseScale + float2(0.0, -time.x);
    half noise = pow(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r, _DistortionSharpness);

    float2 fade2D = smoothstep(0.0, _EdgeFade, uv) * smoothstep(1.0, 1.0 - _EdgeFade, uv);
    float edgeFade = fade2D.x * fade2D.y;
                
    float finalAlpha = edgeFade * input.color.a;

    float2 screenUV = input.screenPos.xy / input.screenPos.w;
    float2 warp = (noise - 0.5) * _DistortionStrength * finalAlpha;
    float2 finalScreenUV = screenUV + warp;

    half3 backgroundSample = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, finalScreenUV).rgb;

    return half4(backgroundSample, finalAlpha);
}
            ENDHLSL
        }
    }
}