// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Parripollo/UI/Shop Dither Background"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		[IntRange]_ColorLevels("Color Levels", Range( 2 , 16)) = 6
		[IntRange]_PixelSize("Pixel Size (1080p)", Range( 1 , 8)) = 3
		[ToggleUI]_Pixelate("Pixelate", Float) = 1
		_DitherStrength("Dither Strength", Range( 0 , 1)) = 1
		[ToggleUI]_PreserveColors("Preserve Colors", Float) = 1

	}

	SubShader
	{
		LOD 0

		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		
		Stencil
		{
			Ref [_Stencil]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
			CompFront [_StencilComp]
			PassFront [_StencilOp]
			FailFront Keep
			ZFailFront Keep
			CompBack Always
			PassBack Keep
			FailBack Keep
			ZFailBack Keep
		}


		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		
		Pass
		{
			Name "Default"
		CGPROGRAM
			
			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile __ UNITY_UI_CLIP_RECT
			#pragma multi_compile __ UNITY_UI_ALPHACLIP
			
			#include "UnityShaderVariables.cginc"
			#define ASE_NEEDS_FRAG_COLOR

			
			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				float4 ase_texcoord2 : TEXCOORD2;
			};
			
			uniform fixed4 _Color;
			uniform fixed4 _TextureSampleAdd;
			uniform float4 _ClipRect;
			uniform sampler2D _MainTex;
			uniform sampler2D _ShopBackdropTex;
			uniform float _PixelSize;
			uniform float _Pixelate;
			uniform float4 _ShopBackdropParallax;
			uniform float _PreserveColors;
			uniform float _ColorLevels;
			uniform float _DitherStrength;
			float Bayer8x8( float2 Cell )
			{
				// Bayer 8x8 ordenado, armado con tres niveles de 2x2 (solo floats: compila en cualquier target)
				// Modulo con floor: tambien vale para celdas negativas (el parallax corre el contenido)
				float2 h0 = floor( Cell );
				float2 h1 = floor( h0 * 0.5 );
				float2 h2 = floor( h0 * 0.25 );
				float2 c0 = h0 - 2.0 * h1;
				float2 c1 = h1 - 2.0 * floor( h1 * 0.5 );
				float2 c2 = h2 - 2.0 * floor( h2 * 0.5 );
				float b0 = frac( c0.x * 0.5 + c0.y * 0.75 );
				float b1 = frac( c1.x * 0.5 + c1.y * 0.75 );
				float b2 = frac( c2.x * 0.5 + c2.y * 0.75 );
				// 64 umbrales equiespaciados y centrados en (0,1): (k + 0.5) / 64
				return b0 + b1 * 0.25 + b2 * 0.0625 + 0.5 / 64.0;
			}
			

			
			v2f vert( appdata_t IN  )
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID( IN );
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				OUT.worldPosition = IN.vertex;
				float4 ase_clipPos = UnityObjectToClipPos(IN.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				OUT.ase_texcoord2 = screenPos;
				
				
				OUT.worldPosition.xyz +=  float3( 0, 0, 0 ) ;
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

				OUT.texcoord = IN.texcoord;
				
				OUT.color = IN.color * _Color;
				return OUT;
			}

			fixed4 frag(v2f IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float4 screenPos = IN.ase_texcoord2;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float2 temp_output_11_0 = (ase_screenPosNorm).xy;
				float2 temp_output_13_0 = (_ScreenParams).xy;
				float2 temp_output_14_0 = ( temp_output_11_0 * temp_output_13_0 );
				float temp_output_15_0 = ( _ScreenParams.y / 1080.0 );
				float temp_output_18_0 = max( round( ( _PixelSize * temp_output_15_0 ) ) , 1.0 );
				float2 temp_output_20_0 = floor( ( temp_output_14_0 / temp_output_18_0 ) );
				float2 temp_output_25_0 = ( ( ( ( ( temp_output_20_0 + float2( 0.5,0.5 ) ) * temp_output_18_0 ) - temp_output_14_0 ) * _Pixelate ) / temp_output_13_0 );
				float2 temp_output_104_0 = round( ( ( (_ShopBackdropParallax).xy * temp_output_15_0 ) / temp_output_18_0 ) );
				float2 texCoord40 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 temp_output_53_0 = ( tex2Dlod( _MainTex, float4( ( texCoord40 + ( ( ( ddx( texCoord40 ) + ddy( texCoord40 ) ) / ( ddx( temp_output_11_0 ) + ddy( temp_output_11_0 ) ) ) * temp_output_25_0 ) ), 0, 0.0) ) * IN.color );
				float3 lerpResult56 = lerp( (tex2Dlod( _ShopBackdropTex, float4( ( ( temp_output_11_0 + temp_output_25_0 ) - ( ( temp_output_104_0 * temp_output_18_0 ) / temp_output_13_0 ) ), 0, 0.0) )).rgb , (temp_output_53_0).rgb , (temp_output_53_0).a);
				float3 temp_output_70_0 = saturate( lerpResult56 );
				float3 break90 = temp_output_70_0;
				float temp_output_92_0 = max( max( break90.x , break90.y ) , break90.z );
				float3 temp_cast_0 = (temp_output_92_0).xxx;
				float3 lerpResult93 = lerp( temp_output_70_0 , temp_cast_0 , _PreserveColors);
				half3 linearToGamma71 = lerpResult93;
				linearToGamma71 = half3( LinearToGammaSpaceExact(linearToGamma71.r), LinearToGammaSpaceExact(linearToGamma71.g), LinearToGammaSpaceExact(linearToGamma71.b) );
				float temp_output_72_0 = ( _ColorLevels - 1.0 );
				float3 temp_output_74_0 = floor( ( linearToGamma71 * temp_output_72_0 ) );
				half3 gammaToLinear79 = ( temp_output_74_0 / temp_output_72_0 );
				gammaToLinear79 = half3( GammaToLinearSpaceExact(gammaToLinear79.r), GammaToLinearSpaceExact(gammaToLinear79.g), GammaToLinearSpaceExact(gammaToLinear79.b) );
				half3 gammaToLinear80 = saturate( ( ( temp_output_74_0 + float3( 1,1,1 ) ) / temp_output_72_0 ) );
				gammaToLinear80 = half3( GammaToLinearSpaceExact(gammaToLinear80.r), GammaToLinearSpaceExact(gammaToLinear80.g), GammaToLinearSpaceExact(gammaToLinear80.b) );
				float2 Cell60 = ( temp_output_20_0 - temp_output_104_0 );
				float localBayer8x860 = Bayer8x8( Cell60 );
				float lerpResult61 = lerp( 0.5 , localBayer8x860 , _DitherStrength);
				float3 temp_cast_1 = (lerpResult61).xxx;
				float3 lerpResult86 = lerp( gammaToLinear79 , gammaToLinear80 , step( temp_cast_1 , ( ( lerpResult93 - gammaToLinear79 ) / max( ( gammaToLinear80 - gammaToLinear79 ) , float3( 0.0001,0.0001,0.0001 ) ) ) ));
				float3 lerpResult98 = lerp( lerpResult86 , ( temp_output_70_0 * ( (lerpResult86).x / max( temp_output_92_0 , 0.0001 ) ) ) , _PreserveColors);
				float4 appendResult87 = (float4(lerpResult98 , 1.0));
				
				half4 color = appendResult87;
				
				#ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
				
				#ifdef UNITY_UI_ALPHACLIP
				clip (color.a - 0.001);
				#endif

				return color;
			}
		ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18900
136.6667;80;567.3334;637.6667;2261.833;504.6666;0.5;False;False
Node;AmplifyShaderEditor.ScreenParams;12;-4200,-520;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;2;-3960,-340;Inherit;False;Property;_PixelSize;Pixel Size (1080p);1;1;[IntRange];Create;False;0;0;0;False;0;False;3;3;1;8;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;15;-3960,-440;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1080;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;16;-3720,-420;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;10;-4200,-720;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RoundOpNode;17;-3560,-420;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;11;-3960,-720;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;13;-3960,-560;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;-3720,-660;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;18;-3400,-420;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;19;-3240,-620;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FloorOpNode;20;-3080,-620;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;100;-4200,-260;Inherit;False;Global;_ShopBackdropParallax;Shop Backdrop Parallax;5;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;21;-2920,-620;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;-2760,-560;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;40;-2920,80;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;101;-3960,-260;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;3;-2760,-420;Inherit;False;Property;_Pixelate;Pixelate;2;1;[ToggleUI];Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;23;-2600,-620;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DdxOpNode;41;-2600,140;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DdyOpNode;45;-2600,400;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DdyOpNode;42;-2600,220;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DdxOpNode;44;-2600,320;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;102;-3720,-260;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;103;-3240,-260;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;24;-2440,-560;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;46;-2440,360;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;43;-2440,180;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;47;-2280,260;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RoundOpNode;104;-3080,-260;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;25;-2280,-560;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;48;-2120,200;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;106;-2760,-260;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;50;-1960,-20;Inherit;False;0;0;_MainTex;Shader;False;0;5;SAMPLER2D;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;49;-1960,120;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;26;-2120,-660;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;107;-2440,-260;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;108;-2040,-500;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.VertexColorNode;52;-1720,280;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;51;-1720,40;Inherit;True;Property;_TextureSample1;Texture Sample 1;5;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;MipLevel;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;53;-1400,120;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;30;-1880,-720;Inherit;True;Global;_ShopBackdropTex;Shop Backdrop Tex;4;0;Create;False;0;0;0;False;0;False;-1;None;;True;0;False;black;Auto;False;Object;-1;MipLevel;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;31;-1560,-720;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;55;-1240,180;Inherit;False;False;False;False;True;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;54;-1240,60;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;56;-1000,-300;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;70;-760,-300;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;90;-1000,-560;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMaxOpNode;91;-840,-580;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;92;-680,-560;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;5;-1240,-520;Inherit;False;Property;_PreserveColors;Preserve Colors;5;1;[ToggleUI];Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;93;-520,-360;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;1;-2240,560;Inherit;False;Property;_ColorLevels;Color Levels;0;1;[IntRange];Create;False;0;0;0;False;0;False;6;6;2;16;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;72;-1960,580;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LinearToGammaNode;71;-600,-200;Inherit;False;1;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;73;-360,-160;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FloorOpNode;74;-200,-160;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;76;-40,-80;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;1,1,1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;77;120,-80;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;75;-40,-240;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;78;280,-80;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GammaToLinearNode;80;440,-80;Inherit;False;1;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GammaToLinearNode;79;280,-240;Inherit;False;1;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;82;600,-160;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;105;-2920,-380;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CustomExpressionNode;60;-2760,700;Inherit;False;// Bayer 8x8 ordenado, armado con tres niveles de 2x2 (solo floats: compila en cualquier target)$// Modulo con floor: tambien vale para celdas negativas (el parallax corre el contenido)$float2 h0 = floor( Cell )@$float2 h1 = floor( h0 * 0.5 )@$float2 h2 = floor( h0 * 0.25 )@$float2 c0 = h0 - 2.0 * h1@$float2 c1 = h1 - 2.0 * floor( h1 * 0.5 )@$float2 c2 = h2 - 2.0 * floor( h2 * 0.5 )@$float b0 = frac( c0.x * 0.5 + c0.y * 0.75 )@$float b1 = frac( c1.x * 0.5 + c1.y * 0.75 )@$float b2 = frac( c2.x * 0.5 + c2.y * 0.75 )@$// 64 umbrales equiespaciados y centrados en (0,1): (k + 0.5) / 64$return b0 + b1 * 0.25 + b2 * 0.0625 + 0.5 / 64.0@;1;False;1;True;Cell;FLOAT2;0,0;In;;Inherit;False;Bayer8x8;False;False;0;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;83;760,-160;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0.0001,0.0001,0.0001;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;4;-2760,860;Inherit;False;Property;_DitherStrength;Dither Strength;3;0;Create;False;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;81;600,-320;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;61;-2440,760;Inherit;False;3;0;FLOAT;0.5;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;84;920,-280;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StepOpNode;85;1080,-120;Inherit;False;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;86;1240,-200;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;94;1400,-40;Inherit;False;True;False;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;95;1400,60;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.0001;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;96;1560,0;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;97;1720,-80;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;98;1880,-200;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;87;2040,-200;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;2280,-200;Float;False;True;-1;2;ASEMaterialInspector;0;7;Parripollo/UI/Shop Dither Background;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;Default;2;False;True;2;5;False;-1;10;False;-1;0;1;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;True;True;True;True;True;0;True;-9;False;False;False;False;False;False;False;True;True;0;True;-5;255;True;-8;255;True;-7;0;True;-4;0;True;-6;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;0;True;-11;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;15;0;12;2
WireConnection;16;0;2;0
WireConnection;16;1;15;0
WireConnection;17;0;16;0
WireConnection;11;0;10;0
WireConnection;13;0;12;0
WireConnection;14;0;11;0
WireConnection;14;1;13;0
WireConnection;18;0;17;0
WireConnection;19;0;14;0
WireConnection;19;1;18;0
WireConnection;20;0;19;0
WireConnection;21;0;20;0
WireConnection;22;0;21;0
WireConnection;22;1;18;0
WireConnection;101;0;100;0
WireConnection;23;0;22;0
WireConnection;23;1;14;0
WireConnection;41;0;40;0
WireConnection;45;0;11;0
WireConnection;42;0;40;0
WireConnection;44;0;11;0
WireConnection;102;0;101;0
WireConnection;102;1;15;0
WireConnection;103;0;102;0
WireConnection;103;1;18;0
WireConnection;24;0;23;0
WireConnection;24;1;3;0
WireConnection;46;0;44;0
WireConnection;46;1;45;0
WireConnection;43;0;41;0
WireConnection;43;1;42;0
WireConnection;47;0;43;0
WireConnection;47;1;46;0
WireConnection;104;0;103;0
WireConnection;25;0;24;0
WireConnection;25;1;13;0
WireConnection;48;0;47;0
WireConnection;48;1;25;0
WireConnection;106;0;104;0
WireConnection;106;1;18;0
WireConnection;49;0;40;0
WireConnection;49;1;48;0
WireConnection;26;0;11;0
WireConnection;26;1;25;0
WireConnection;107;0;106;0
WireConnection;107;1;13;0
WireConnection;108;0;26;0
WireConnection;108;1;107;0
WireConnection;51;0;50;0
WireConnection;51;1;49;0
WireConnection;53;0;51;0
WireConnection;53;1;52;0
WireConnection;30;1;108;0
WireConnection;31;0;30;0
WireConnection;55;0;53;0
WireConnection;54;0;53;0
WireConnection;56;0;31;0
WireConnection;56;1;54;0
WireConnection;56;2;55;0
WireConnection;70;0;56;0
WireConnection;90;0;70;0
WireConnection;91;0;90;0
WireConnection;91;1;90;1
WireConnection;92;0;91;0
WireConnection;92;1;90;2
WireConnection;93;0;70;0
WireConnection;93;1;92;0
WireConnection;93;2;5;0
WireConnection;72;0;1;0
WireConnection;71;0;93;0
WireConnection;73;0;71;0
WireConnection;73;1;72;0
WireConnection;74;0;73;0
WireConnection;76;0;74;0
WireConnection;77;0;76;0
WireConnection;77;1;72;0
WireConnection;75;0;74;0
WireConnection;75;1;72;0
WireConnection;78;0;77;0
WireConnection;80;0;78;0
WireConnection;79;0;75;0
WireConnection;82;0;80;0
WireConnection;82;1;79;0
WireConnection;105;0;20;0
WireConnection;105;1;104;0
WireConnection;60;0;105;0
WireConnection;83;0;82;0
WireConnection;81;0;93;0
WireConnection;81;1;79;0
WireConnection;61;1;60;0
WireConnection;61;2;4;0
WireConnection;84;0;81;0
WireConnection;84;1;83;0
WireConnection;85;0;61;0
WireConnection;85;1;84;0
WireConnection;86;0;79;0
WireConnection;86;1;80;0
WireConnection;86;2;85;0
WireConnection;94;0;86;0
WireConnection;95;0;92;0
WireConnection;96;0;94;0
WireConnection;96;1;95;0
WireConnection;97;0;70;0
WireConnection;97;1;96;0
WireConnection;98;0;86;0
WireConnection;98;1;97;0
WireConnection;98;2;5;0
WireConnection;87;0;98;0
WireConnection;0;0;87;0
ASEEND*/
//CHKSM=F93425197EB86F93999BBE79B45723FFB274B76E