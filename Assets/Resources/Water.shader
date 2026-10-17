Shader "ToonArena/Water"
{
    Properties
    {
        _Color ("Deep", Color) = (0.16,0.5,0.9,1)
        _Shallow ("Shallow", Color) = (0.4,0.8,1,1)
        _Foam ("Foam", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // Stylised water for a flat disc: rolling light bands, glints, and a
        // foam line that laps at the shore.
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color, _Shallow, _Foam;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float2 local : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.local = v.vertex.xz * 2;      // 0 at the centre, 1 at the rim
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float hash (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.wpos.xz;
                float t = _Time.y;
                float r = length(i.local);

                float wave = sin(p.x * 0.9 + t * 1.3) + sin(p.y * 1.1 - t * 1.1) + sin((p.x + p.y) * 0.6 + t * 0.7);
                fixed3 col = lerp(_Color.rgb, _Shallow.rgb, saturate(wave * 0.16 + 0.35 + r * r * 0.5));

                // Bands of light sliding over the surface.
                float band = smoothstep(1.55, 1.75, wave + sin(p.x * 2.3 - t * 0.6) * 0.4);
                col = lerp(col, _Foam.rgb, band * 0.45);

                // Glints that wink on and off.
                float2 cell = floor(p * 2.2);
                float glint = step(0.965, hash(cell)) * saturate(sin(t * 3 + hash(cell + 7) * 6.28) * 2 - 1);
                col += glint * 0.6;

                // Foam breathing in and out along the shore.
                float edge = 0.93 + 0.018 * sin(atan2(i.local.y, i.local.x) * 26 + t * 1.6) + 0.012 * sin(t * 0.9);
                col = lerp(col, _Foam.rgb, smoothstep(edge, edge + 0.012, r) * 0.9);

                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
