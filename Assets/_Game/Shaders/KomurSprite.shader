// Unlit sprite shader for Kömür.
// - Stencil: scenery clears the stencil, characters write 1; the hidden-silhouette material draws only
//   where a character's pixels were covered afterwards (stencil != 1), giving an exact "see through walls".
// - Plagued look for rats: grey/green tint, 1-texel outline, hit flash (_HitEffectBlend), dissolve (_FadeAmount).
Shader "Komur/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.05

        _GreyBlend ("Grey Blend", Range(0,1)) = 0
        _GreyTint ("Grey Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color (alpha 0 = off)", Color) = (0,0,0,0)
        _OutlineWidth ("Outline Width (texels)", Range(1,3)) = 1

        _HitEffectColor ("Hit Flash Color", Color) = (1,1,1,1)
        _HitEffectBlend ("Hit Flash", Range(0,1)) = 0

        _FadeTex ("Dissolve Noise", 2D) = "white" {}
        _FadeAmount ("Dissolve", Range(-0.1,1)) = -0.1
        _FadeBurnColor ("Dissolve Edge Color", Color) = (0.5,1,0.2,1)
        _FadeBurnWidth ("Dissolve Edge Width", Range(0,0.3)) = 0.08

        _Silhouette ("Silhouette Mode", Float) = 0
        _SilhouetteColor ("Silhouette Color", Color) = (1,1,1,0.5)

        _StencilRef ("Stencil Ref", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Op", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass [_StencilOp]
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Cutoff;
            float _GreyBlend;
            fixed4 _GreyTint;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            fixed4 _HitEffectColor;
            float _HitEffectBlend;
            sampler2D _FadeTex;
            float _FadeAmount;
            fixed4 _FadeBurnColor;
            float _FadeBurnWidth;
            float _Silhouette;
            fixed4 _SilhouetteColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                // 1-texel outline around the sprite's shape
                if (_OutlineColor.a > 0 && c.a < 0.1)
                {
                    float2 t = _MainTex_TexelSize.xy;
                    float n = tex2D(_MainTex, i.uv + float2(t.x, 0)).a + tex2D(_MainTex, i.uv - float2(t.x, 0)).a
                            + tex2D(_MainTex, i.uv + float2(0, t.y)).a + tex2D(_MainTex, i.uv - float2(0, t.y)).a;
                    if (_OutlineWidth > 1.5)
                    {
                        float2 w = t * floor(_OutlineWidth + 0.5);
                        n += tex2D(_MainTex, i.uv + float2(w.x, 0)).a + tex2D(_MainTex, i.uv - float2(w.x, 0)).a
                           + tex2D(_MainTex, i.uv + float2(0, w.y)).a + tex2D(_MainTex, i.uv - float2(0, w.y)).a
                           + tex2D(_MainTex, i.uv + t).a + tex2D(_MainTex, i.uv - t).a
                           + tex2D(_MainTex, i.uv + float2(t.x, -t.y)).a + tex2D(_MainTex, i.uv + float2(-t.x, t.y)).a;
                    }
                    if (n > 0.1) c = fixed4(_OutlineColor.rgb, _OutlineColor.a * i.color.a);
                }

                clip(c.a - _Cutoff);

                float grey = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, grey * _GreyTint.rgb, _GreyBlend);
                c.rgb = lerp(c.rgb, _HitEffectColor.rgb, _HitEffectBlend);

                // dissolve
                float noise = tex2D(_FadeTex, i.uv * 3.0).r;
                clip(noise - _FadeAmount);
                if (_FadeAmount > -0.05 && noise - _FadeAmount < _FadeBurnWidth) c.rgb = _FadeBurnColor.rgb;

                if (_Silhouette > 0.5) c = fixed4(_SilhouetteColor.rgb, _SilhouetteColor.a);
                return c;
            }
            ENDCG
        }
    }
}
