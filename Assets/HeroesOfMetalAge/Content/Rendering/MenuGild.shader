// UI shader for the New Game title: molten gold pours over it from the left. Behind the wavy front the title turns to
// bright, shimmering gold; at the front a hot white-gold band runs along. _GildProgress 0 draws the sprite unchanged,
// 1 leaves it fully gilded. The view sets it on its own copy of the material.
Shader "HeroesOfMetalAge/UI/MenuGild"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GildProgress ("Gild progress", Range(0, 1)) = 0
        _FrontWidth ("Front width", Range(0.01, 0.5)) = 0.12
        _Slant ("Slant of the front", Range(-1, 1)) = 0.35
        _GoldDark ("Gold dark", Color) = (0.75, 0.42, 0.05, 1)
        _GoldLight ("Gold light", Color) = (1, 0.9, 0.45, 1)
        _HotColor ("Front", Color) = (1, 0.98, 0.85, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;
            float _GildProgress;
            float _FrontWidth;
            float _Slant;
            fixed4 _GoldDark;
            fixed4 _GoldLight;
            fixed4 _HotColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                float2 uv = IN.texcoord;
                // a slanted, slightly wavy front from left to right
                float along = uv.x + (uv.y - 0.5) * _Slant + sin(uv.y * 18.0 + _Time.y * 6.0) * 0.015;
                float front = lerp(-_FrontWidth - abs(_Slant) * 0.5, 1.0 + abs(_Slant) * 0.5, _GildProgress);
                float behind = saturate((front - along) / _FrontWidth);
                float band = saturate(1.0 - abs(front - along) / (_FrontWidth * 0.5)) * step(0.001, _GildProgress) * step(_GildProgress, 0.999);

                // the gold keeps the title's own shading: dark gold in its shadows, light gold on its highlights
                float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
                float shimmer = 0.9 + 0.1 * sin(uv.x * 40.0 - _Time.y * 8.0 + uv.y * 10.0);
                fixed3 gold = lerp(_GoldDark.rgb, _GoldLight.rgb, saturate(luminance * 1.4)) * (0.8 + 0.6 * luminance) * shimmer;
                color.rgb = lerp(color.rgb, gold, behind * 0.85);
                color.rgb = lerp(color.rgb, _HotColor.rgb, band * 0.8 * saturate(luminance * 2.0));

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
