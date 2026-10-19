// UI shader for the mission briefing's huge explosion, drawn over a square Image: a blinding fireball on the ground,
// a shockwave ring racing out, a dust surge rolling along the ground, and the mushroom cloud whose cap rises on its stem,
// billowing and cooling from white heat through fire and embers to dark smoke before it fades. _Progress 0 shows
// nothing, 1 is the end; the view sets it on its own copy of the material.
Shader "HeroesOfMetalAge/UI/NukeExplosion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 3.7
        _GroundY ("Ground height (-1 bottom, 1 top)", Range(-1, 1)) = -0.55
        _HotColor ("White heat", Color) = (1, 0.97, 0.86, 1)
        _FireColor ("Fire", Color) = (1, 0.58, 0.14, 1)
        _EmberColor ("Embers", Color) = (0.62, 0.13, 0.04, 1)
        _SmokeColor ("Smoke", Color) = (0.2, 0.17, 0.15, 1)

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
            float _Progress;
            float _Seed;
            float _GroundY;
            fixed4 _HotColor;
            fixed4 _FireColor;
            fixed4 _EmberColor;
            fixed4 _SmokeColor;

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

            // 0..1 billowing noise
            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int octave = 0; octave < 5; octave++)
                {
                    value += amplitude * Noise(p);
                    p = p * 2.02 + 11.7;
                    amplitude *= 0.5;
                }

                return value / 0.96875;
            }

            // smoke, embers, fire, white heat
            float3 Ramp(float heat)
            {
                float3 color = lerp(_SmokeColor.rgb, _EmberColor.rgb, saturate(heat / 0.3));
                color = lerp(color, _FireColor.rgb, saturate((heat - 0.3) / 0.3));
                return lerp(color, _HotColor.rgb, saturate((heat - 0.65) / 0.3));
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
                float t = saturate(_Progress);
                clip(t - 0.0001);
                float2 p = IN.texcoord * 2.0 - 1.0;
                float time = _Time.y;
                float2 ground = float2(0.0, _GroundY);

                // turbulence that boils upwards and rolls outwards
                float boil = Fbm(p * 3.2 + float2(_Seed, -time * 0.55 + _Seed));
                float roll = Fbm(p * 6.5 + float2(time * 0.25 - _Seed, -time * 0.9));

                // the first blinding fireball on the ground, gone after a fifth
                float ballRadius = lerp(0.04, 0.34, saturate(t * 7.0));
                float2 ballCenter = ground + float2(0.0, ballRadius * 0.6);
                float ball = saturate((1.0 - length(p - ballCenter) / ballRadius) * 3.0 + (boil - 0.5)) * (1.0 - smoothstep(0.12, 0.3, t));

                // cauliflower lumps: ridged noise, bright crests and dark crevices
                float cauli = 1.0 - abs(2.0 * Fbm(p * 5.0 + float2(_Seed * 2.0, -time * 0.35)) - 1.0);

                // the cap rises on its stem and swells, wider than tall
                float rise = smoothstep(0.03, 0.7, t);
                float capRadius = lerp(0.12, 0.52, 1.0 - exp(-t * 4.5));
                float2 capCenter = float2(0.0, lerp(_GroundY + 0.28, 0.42, rise));
                float2 capOffset = (p - capCenter) / float2(capRadius * 1.35, capRadius * 0.78);
                float capDistance = length(capOffset);
                float cap = saturate((1.0 - capDistance) * 4.0 + (boil - 0.5) * 2.4 + (cauli - 0.5) * 1.3) * smoothstep(0.03, 0.12, t);

                // the stem from the ground to the cap's underside, thicker at its foot
                float stemTop = capCenter.y - capRadius * 0.4;
                float along = saturate((p.y - _GroundY) / max(0.01, stemTop - _GroundY));
                float stemWidth = lerp(0.04, 0.12, rise) * lerp(1.7, 0.85, along) * (0.7 + 0.6 * roll);
                float stem = step(_GroundY, p.y) * step(p.y, stemTop) * saturate((1.0 - abs(p.x) / stemWidth) * 2.5 + (cauli - 0.5) * 0.8)
                    * smoothstep(0.06, 0.2, t);

                // the dust surge spreading low along the ground
                float surgeRadius = lerp(0.05, 1.05, 1.0 - exp(-t * 3.2));
                float2 surgeOffset = (p - ground - float2(0.0, 0.04)) / float2(surgeRadius, surgeRadius * 0.22 + 0.03);
                float surge = saturate((1.0 - length(surgeOffset)) * 2.6 + (roll - 0.5) * 1.6 + (cauli - 0.5)) * smoothstep(0.02, 0.12, t);

                // the shockwave ring racing out
                float ringRadius = t * 3.2;
                float ring = saturate(1.0 - abs(length((p - ground) * float2(1.0, 1.6)) - ringRadius) / 0.035) * (1.0 - saturate(t * 3.5));

                float density = saturate(max(max(cap, stem), max(surge, ball)));
                // hot early, deep in the cap and on its underside lit by the fire below; cooling to smoke
                float core = saturate(1.0 - capDistance) * cap;
                float under = saturate((capCenter.y - p.y) / max(0.01, capRadius) * 0.8 + 0.3);
                float heat = saturate((1.0 - t * 1.25) * 0.8 + core * 0.6 * (1.0 - t * 0.8) + under * 0.3 * (1.0 - t) + ball) * (0.6 + 0.55 * boil);
                float3 color = Ramp(heat);
                color *= 0.5 + 0.8 * cauli * (0.6 + 0.4 * boil);
                color *= lerp(0.75, 1.15, saturate((p.y - capCenter.y) / max(0.01, capRadius) * 0.5 + 0.5));
                float alpha = density * (1.0 - smoothstep(0.75, 1.0, t));

                color = lerp(color, _HotColor.rgb, ring * 0.8);
                alpha = max(alpha, ring * 0.75);
                fixed4 result = fixed4(color, alpha * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
