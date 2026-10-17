Shader "ToonArena/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.3,0.55,1,1)
        _Bottom ("Horizon", Color) = (0.75,0.9,1,1)
        _CloudLit ("Cloud, Sunlit", Color) = (1,1,1,1)
        _CloudDark ("Cloud, Shaded", Color) = (0.62,0.68,0.78,1)
        _Cover ("Cloud Cover", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" }
        Cull Front
        ZWrite Off

        // An inside-out sphere that follows the camera: a gradient from the horizon's haze up
        // to the zenith, with drifting cloud laid over it.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Bottom;
            fixed4 _CloudLit;
            fixed4 _CloudDark;
            float _Cover;
            float _WorldSat;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm (float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += a * VNoise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;
                // The horizon is pale and hazy; the blue deepens quickly above it.
                fixed3 col = lerp(_Bottom.rgb, _Top.rgb, smoothstep(0.0, 0.5, h));
                col = lerp(col, _Bottom.rgb * 1.04, exp(-max(h, 0) * 9) * 0.5);

                // Cloud: the sky seen as a flat ceiling a long way up, drifting on the wind.
                float2 p = dir.xz / (max(h, 0) + 0.16) * 0.85 + _Time.y * float2(0.006, 0.0025);
                float n = Fbm(p);
                float edge = lerp(0.72, 0.38, _Cover);
                float cloud = smoothstep(edge, edge + 0.22, n);
                // Thick cloud is darker underneath; thin edges catch the light.
                float thick = smoothstep(edge + 0.05, edge + 0.4, n);
                fixed3 cc = lerp(_CloudLit.rgb, _CloudDark.rgb, thick * 0.85);
                cloud *= smoothstep(0.015, 0.16, h);
                col = lerp(col, cc, cloud * 0.92);

                float grey = dot(col, float3(0.3, 0.59, 0.11));
                col = lerp(grey * fixed3(0.8, 0.8, 0.9), col, saturate(_WorldSat));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
