// Depth of field for filmed shots (FilmLens.cs): what stands at the focus distance stays
// sharp, and the picture softens the further things are from it, in front or behind.
Shader "Hidden/FilmLens"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D_float _CameraDepthTexture;
            float4 _MainTex_TexelSize;
            float _Focus, _Range, _MaxBlur;

            // How far out of focus a depth is: 0 inside the sharp band, 1 at full blur.
            float Coc(float depth)
            {
                return saturate((abs(depth - _Focus) - _Range * 0.35) / _Range);
            }

            float DepthAt(float2 uv)
            {
                return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float depth = DepthAt(i.uv);
                float coc = Coc(depth);
                float4 sum = tex2D(_MainTex, i.uv);
                float weight = 1.0;
                // A disc of taps on the golden angle: an even, round blur with no visible pattern.
                const float golden = 2.39996323;
                for (int k = 1; k <= 36; k++)
                {
                    float r = sqrt(k / 36.0);
                    float a = k * golden;
                    float2 offset = float2(cos(a), sin(a)) * r * _MaxBlur * _MainTex_TexelSize.xy;
                    float2 uv = i.uv + offset * coc;
                    float tapDepth = DepthAt(uv);
                    float tapCoc = Coc(tapDepth);
                    // A sharp thing in front must not smear over a soft background: a tap counts
                    // by how blurred it is itself, unless it lies behind this pixel.
                    float w = tapDepth >= depth ? 1.0 : saturate(tapCoc * 2.0 + 0.05);
                    sum += tex2D(_MainTex, uv) * w;
                    weight += w;
                }
                return sum / weight;
            }
            ENDCG
        }
    }
    Fallback Off
}
