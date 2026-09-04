Shader "Strategy/UtilitySparkle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Seed ("Seed", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _Seed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Tiny plus-shaped glint. Brightness twinkles; the tile itself does not scale.
            float SparkleAt(float2 uv, float2 cell, float seed)
            {
                float2 h = Hash22(cell + seed);
                if (h.x < 0.42)
                    return 0;

                float2 pos = (cell + 0.5 + (h - 0.5) * 0.55) / 5.0;
                float2 d = uv - pos;
                float twinkle = 0.5 + 0.5 * sin(_Time.y * (2.4 + h.y * 4.2) + h.x * 6.2831);
                twinkle = twinkle * twinkle * twinkle;
                float core = smoothstep(0.038, 0.006, length(d));
                float armX = smoothstep(0.01, 0.0, abs(d.x)) * smoothstep(0.065, 0.0, abs(d.y));
                float armY = smoothstep(0.01, 0.0, abs(d.y)) * smoothstep(0.065, 0.0, abs(d.x));
                return (core + 0.7 * max(armX, armY)) * twinkle;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.texcoord).a;
                float spark = 0;
                [unroll]
                for (int y = 0; y < 5; y++)
                {
                    [unroll]
                    for (int x = 0; x < 5; x++)
                        spark += SparkleAt(i.texcoord, float2(x, y), _Seed);
                }

                float3 gold = float3(1.0, 0.94, 0.62);
                float3 white = float3(1.0, 1.0, 1.0);
                float3 col = lerp(gold, white, saturate(spark * 0.4));
                float alpha = saturate(spark) * i.color.a * mask;
                return float4(col * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
