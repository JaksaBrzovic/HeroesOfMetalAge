// UI shader for the intro's black curtain: it burns away like paper from an origin, with a ragged charred rim, a
// glowing flickering edge and a few embers drifting off the hole's border. The burn is driven by the vertex alpha
// (Image.color.a): 1 is the whole curtain, 0 is burnt away. Burnt pixels are discarded, so a Mask on the same Image
// cuts its children (the title) out of the holes too.
Shader "HeroesOfMetalAge/UI/IntroBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Origin ("Burn origin (UV)", Vector) = (0.42, 0.32, 0, 0)
        _Aspect ("Width / height", Float) = 1.7778
        _NoiseScale ("Noise scale", Float) = 5
        _Raggedness ("Raggedness", Range(0, 1)) = 0.45
        _GlowWidth ("Glow width", Range(0.001, 0.2)) = 0.03
        _CharWidth ("Char width", Range(0.001, 0.3)) = 0.09
        _CharColor ("Charred", Color) = (0.22, 0.08, 0.02, 1)
        _GlowColor ("Glow", Color) = (1, 0.38, 0.05, 1)
        _HotColor ("Hot edge", Color) = (1, 0.88, 0.45, 1)
        _EmberAmount ("Embers", Range(0, 1)) = 0.6

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
            #pragma target 3.0

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

            float4 _ClipRect;
            float4 _Origin;
            float _Aspect;
            float _NoiseScale;
            float _Raggedness;
            float _GlowWidth;
            float _CharWidth;
            fixed4 _CharColor;
            fixed4 _GlowColor;
            fixed4 _HotColor;
            float _EmberAmount;

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 0..1 cloudy noise; its fine octaves make the rim ragged
            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int octave = 0; octave < 5; octave++)
                {
                    value += amplitude * Noise(p);
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }

                return value / 0.96875;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float progress = 1.0 - IN.color.a;
                float2 p = float2(IN.texcoord.x * _Aspect, IN.texcoord.y);
                float2 origin = float2(_Origin.x * _Aspect, _Origin.y);
                // the farthest corner is reached exactly as the burn completes
                float farthest = max(max(distance(origin, float2(0.0, 0.0)), distance(origin, float2(_Aspect, 0.0))),
                    max(distance(origin, float2(0.0, 1.0)), distance(origin, float2(_Aspect, 1.0))));
                float field = lerp(distance(p, origin) / farthest, Fbm(p * _NoiseScale), _Raggedness);

                // the front starts before 0 so no rim shows on the intact curtain, and passes 1 so nothing is left
                float front = lerp(-(_GlowWidth + _CharWidth), 1.01, progress);
                float edge = field - front;
                fixed4 color;
                if (edge < 0.0)
                {
                    // burnt: empty, except embers flying off near the border
                    float2 cell = floor(p * 180.0 + float2(0.0, -_Time.y * 6.0));
                    float speck = step(1.0 - 0.06 * _EmberAmount, Hash(cell));
                    float nearRim = saturate(1.0 + edge / (_GlowWidth * 4.0));
                    float flicker = 0.6 + 0.4 * Noise(cell + _Time.y * 9.0);
                    color = fixed4(lerp(_GlowColor.rgb, _HotColor.rgb, flicker), speck * nearRim * flicker * saturate((1.0 - progress) * 8.0));
                }
                else
                {
                    float glow = 1.0 - saturate(edge / _GlowWidth);
                    float charred = 1.0 - saturate((edge - _GlowWidth) / _CharWidth);
                    float flicker = 0.75 + 0.25 * Noise(p * 40.0 + _Time.y * 5.0);
                    color = fixed4(IN.color.rgb, 1.0);
                    color.rgb = lerp(color.rgb, _CharColor.rgb, charred * charred);
                    color.rgb = lerp(color.rgb, lerp(_GlowColor.rgb, _HotColor.rgb, glow * glow) * flicker, glow);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // always discard the holes, so a Mask on this Image never writes its stencil there
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
