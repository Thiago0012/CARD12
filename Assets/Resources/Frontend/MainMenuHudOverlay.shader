Shader "ArcaneArena/UI/MainMenuHudOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MainMenuHudOverlay"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float InsideRange(float value, float minimum, float maximum)
            {
                return step(minimum, value) * step(value, maximum);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                float2 uv = input.texcoord;

                // Quatro janelas laterais e a janela de configuracoes.
                float inLeftButtonArea =
                    InsideRange(uv.x, 0.060, 0.310) *
                    InsideRange(uv.y, 0.145, 0.575);
                float inSettingsArea =
                    InsideRange(uv.x, 0.935, 0.982) *
                    InsideRange(uv.y, 0.915, 0.993);

                float brightest = max(color.r, max(color.g, color.b));
                float darkest = min(color.r, min(color.g, color.b));
                float lowChroma = 1.0 - smoothstep(0.055, 0.16, brightest - darkest);
                float nearWhite = smoothstep(0.72, 0.94, darkest) * lowChroma;
                float cutout = saturate(inLeftButtonArea + inSettingsArea) * nearWhite;
                color.a *= 1.0 - cutout;

                return color;
            }
            ENDCG
        }
    }
}
