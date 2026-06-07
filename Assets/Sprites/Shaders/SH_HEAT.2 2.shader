Shader"Custom/URP2D/Grill_Smoke_Visible"
{
    Properties
    {
        // Textura principal del Sprite (usada solo para definir la forma/máscara del calor)
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        
        // Textura de ruido para la distorsión (recomiendo un ruido suave/perlin)
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        [Header(Animation)]
        _Speed ("Scroll Speed", Range(0, 5)) = 2.0
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 2.0

        [Header(Distortion)]
        // Qué tanto se refracta el fondo
        _DistortionStrength ("Warp Strength", Range(0, 0.1)) = 0.03
        // Qué tan nítida es la distorsión
        _DistortionSharpness ("Warp Sharpness", Range(1, 10)) = 2.0

        [Header(Fading)]
        // Suavizado de los bordes del sprite
        _EdgeFade ("Edge Fade", Range(0.001, 0.49)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            // Importante: Dibujar después de los opacos para poder capturar el fondo
            "Queue" = "Transparent+1" 
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        // El blending no es estrictamente necesario si solo distorsionamos, 
        // pero lo dejamos por si queremos mezclar con color.
Blend SrcAlpha
OneMinusSrcAlpha
        Cull
Off
        ZWriteOff

        Pass
        {
Name"HeatDistortionPass"

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
                // Coordenadas de la pantalla (donde está el pixel respecto a la cámara)
    float4 screenPos : TEXCOORD0;
    float2 uv : TEXCOORD1;
    half4 color : COLOR;
};

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            
            // Esta es la textura mágica que activa Unity al marcar "Opaque Texture"
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
                
                // Calcula la posición del vértice en la pantalla (0 a 1)
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                
                return
output;
            }

half4 frag(Varyings input) : SV_Target
{
                // 1. Preparar UVs y Tiempo
    float2 uv = input.uv;
    float3 time = _Time.y * _Speed;

                // 2. Generar Ruido Animado
    float2 noiseUV = uv * _NoiseScale + float2(0.0, -time.x);
                // Usamos pow para hacer el ruido más "picudo" o suave
    half noise = pow(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r, _DistortionSharpness);

                // 3. Crear Máscara de bordes (para que el cuadrado del sprite no corte la distorsión)
    float2 fade2D = smoothstep(0.0, _EdgeFade, uv) * smoothstep(1.0, 1.0 - _EdgeFade, uv);
    float edgeFade = fade2D.x * fade2D.y;
                
                // Multiplicar por el alpha del Sprite Renderer (por si quieres desvanecerlo desde Unity)
    float finalAlpha = edgeFade * input.color.a;

                // 4. Calcular Distorsión de Pantalla (La Refracción)
                // Proyectar coordenadas de pantalla
    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // Crear el desplazamiento (warp) vectorial basado en el ruido y la fuerza
    float2 warp = (noise - 0.5) * _DistortionStrength * finalAlpha;
                
                // Aplicar el desplazamiento a las coordenadas de pantalla originales
    float2 finalScreenUV = screenUV + warp;

                // 5. Capturar el Fondo Distorsionado
    half3 backgroundSample = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, finalScreenUV).rgb;

                // 6. Resultado Final
                // Devolvemos el fondo capturado con el alpha del sprite
    return half4(backgroundSample, finalAlpha);
}
            ENDHLSL
        }
    }
}