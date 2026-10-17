Shader "Hidden/ToonArena/Post"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;
        float2 _Dir;
        float _Threshold, _Intensity, _Saturation, _Contrast, _Vignette;
        fixed4 _Tint;
        sampler2D _RayTex;
        float4 _SunPos;     // xy: where the sun sits on screen, z: how far each blur step reaches
        float _Rays;
        float _Filmic, _Exposure, _Impact, _Lines, _Aberr, _Aspect;
        sampler2D _AoTex;
        sampler2D_float _CameraDepthTexture;
        float4 _AoParams;   // x: radius in metres, y: strength, z and w: the projection's x and y scale
        float _Ao, _Grain;

        // Where a pixel is in front of the camera, from the depth buffer.
        float3 ViewPos (float2 uv)
        {
            float d = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            return float3((uv * 2 - 1) / _AoParams.zw, 1) * d;
        }

        // ACES filmic curve (Narkowicz fit): highlights roll off instead of clipping.
        half3 Aces (half3 x)
        {
            return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
        }
        ENDCG

        // 0: keep only the bright parts of the frame.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                half lum = dot(c, half3(0.3, 0.59, 0.11));
                half k = saturate((lum - _Threshold) / max(0.0001, 1 - _Threshold));
                return half4(min(c, 4) * k, 1);
            }
            ENDCG
        }

        // 1: one direction of a separable gaussian blur.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                float2 d = _Dir * _MainTex_TexelSize.xy;
                half3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
                c += tex2D(_MainTex, i.uv + d * 1.385).rgb * 0.316;
                c += tex2D(_MainTex, i.uv - d * 1.385).rgb * 0.316;
                c += tex2D(_MainTex, i.uv + d * 3.231).rgb * 0.07;
                c += tex2D(_MainTex, i.uv - d * 3.231).rgb * 0.07;
                return half4(c, 1);
            }
            ENDCG
        }

        // 2: add the glow back and grade the final colour.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                float2 buv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) buv.y = 1 - buv.y;
                #endif
                float2 q = i.uv - 0.5;

                // A hard hit pulls the colour channels apart for a moment, most at the edges.
                float2 split = q * _Aberr * 0.02;
                half3 c;
                c.r = tex2D(_MainTex, i.uv + split).r;
                c.g = tex2D(_MainTex, i.uv).g;
                c.b = tex2D(_MainTex, i.uv - split).b;
                // Corners, creases and the ground under things are darker: less of the sky reaches them.
                c *= lerp(1, tex2D(_AoTex, buv).r, _Ao);
                c += tex2D(_BloomTex, buv).rgb * _Intensity;
                c += tex2D(_RayTex, buv).rgb * _Rays;

                // The frame is stored gamma-encoded: straighten it, run the film curve, bend it back.
                half3 film = pow(Aces(pow(max(c, 0), 2.2) * _Exposure), 1.0 / 2.2);
                c = lerp(saturate(c), film, _Filmic);

                half grey = dot(c, half3(0.3, 0.59, 0.11));
                c = lerp(grey.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= _Tint.rgb;
                c *= 1 - dot(q, q) * _Vignette;

                // A breath of film grain, so flat colour never looks like flat plastic.
                float grain = frac(sin(dot(i.uv * _ScreenParams.xy + frac(_Time.y) * 61.7, float2(12.9898, 78.233))) * 43758.5453);
                c += (grain - 0.5) * _Grain;

                // Speed lines: white wedges racing in from the edge of the frame, redrawn many times a second.
                float2 qa = q * float2(_Aspect, 1);
                float ang = atan2(qa.y, qa.x) * (36.0 / 3.14159265);
                float seg = floor(ang);
                float rnd = frac(sin(seg * 12.9898 + floor(_Time.y * 18.0) * 78.233) * 43758.5453);
                float across = frac(ang);
                float wedge = smoothstep(0.0, 0.3, across) * smoothstep(1.0, 0.7, across);
                float from = 0.3 + rnd * 0.3;
                float streak = step(0.62, rnd) * wedge * smoothstep(from, from + 0.2, length(qa));
                c = lerp(c, half3(1, 1, 1), saturate(streak * _Lines));

                // Impact frame: for an instant the picture is ink and paper.
                half lum = dot(saturate(c), half3(0.3, 0.59, 0.11));
                half3 ink = lerp(half3(0.05, 0.04, 0.1), half3(1, 1, 1), smoothstep(0.36, 0.44, lum));
                c = lerp(c, ink, _Impact);
                return half4(saturate(c), 1);
            }
            ENDCG
        }

        // 3: light shafts. Smears the bright parts of the frame away from the sun, so
        // whatever stands in front of it (trees, towers, the hero) cuts beams out of the light.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 reach = (_SunPos.xy - uv) * _SunPos.z;
                half3 c = 0;
                half w = 1, total = 0;
                for (int k = 0; k < 14; k++)
                {
                    c += tex2D(_MainTex, uv).rgb * w;
                    total += w;
                    w *= 0.9;
                    uv += reach;
                }
                return half4(c / total, 1);
            }
            ENDCG
        }
        // 4: ambient occlusion. For each pixel, look at its neighbours in the depth buffer: the
        // more of them stand in front of the surface, the less sky it sees.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            half4 frag (v2f_img i) : SV_Target
            {
                float3 p = ViewPos(i.uv);
                if (p.z > 120) return half4(1, 1, 1, 1);
                float3 n = normalize(cross(ddy(p), ddx(p)));
                if (dot(n, p) > 0) n = -n;

                float turn = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715)))) * 6.2831853;
                float2 reach = _AoParams.x * _AoParams.zw * 0.5 / p.z;
                float limit = _AoParams.x * _AoParams.x * 4;
                float occ = 0;
                for (int k = 0; k < 10; k++)
                {
                    float a = turn + k * 2.399963;
                    float r = sqrt((k + 0.5) / 10.0);
                    float3 v = ViewPos(i.uv + float2(cos(a), sin(a)) * r * reach) - p;
                    float d2 = dot(v, v);
                    occ += max(0, dot(n, v) - 0.015 * p.z - 0.02) / (d2 + 0.02) * step(d2, limit);
                }
                float ao = saturate(1 - occ * _AoParams.y * _AoParams.x / 10.0);
                ao = lerp(ao, 1, saturate((p.z - 60) / 60));
                return half4(ao, ao, ao, 1);
            }
            ENDCG
        }
    }
}
