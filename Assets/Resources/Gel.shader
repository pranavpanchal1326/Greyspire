Shader "ToonArena/Gel"
{
    // Slime: a body of wet, half-clear jelly. Light glances off its skin, glows through
    // its thin edges, and what is inside it shows darker through the middle. The body
    // never holds quite still.
    Properties
    {
        _Color ("Colour", Color) = (0.3,0.55,0.2,1)
        _Deep ("Colour Seen Through The Thick Of It", Color) = (0.06,0.14,0.05,1)
        _Flash ("Hit Flash", Range(0,1)) = 0
        _OutlineColor ("Aura Color", Color) = (0,0,0,1)
        _OutlineWidth ("Aura Width (below 0.025 none)", Range(0,0.1)) = 0
        _Wobble ("Wobble", Range(0,1)) = 0.5
    }
    SubShader
    {
        // Drawn after the solid world, but still counted as solid for depth and shadows.
        Tags { "Queue"="AlphaTest+40" "RenderType"="Opaque" }

        CGINCLUDE
        #include "UnityCG.cginc"
        float _Wobble;

        float3 Wobbled (float3 v)
        {
            float t = _Time.y;
            float phase = unity_ObjectToWorld._m03 * 1.7 + unity_ObjectToWorld._m23 * 1.3;
            float up = saturate(v.y * 1.4 + 0.2);
            v.xz *= 1 + _Wobble * 0.06 * up * sin(t * 4.3 + phase + v.y * 5);
            v.y *= 1 + _Wobble * 0.05 * sin(t * 3.1 + phase * 1.3 + v.x * 4 + v.z * 3);
            v.x += _Wobble * 0.03 * up * sin(t * 2.3 + v.z * 6 + phase);
            return v;
        }
        ENDCG

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color, _Deep, _OutlineColor;
            fixed _Flash;
            float _OutlineWidth;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
                float height : TEXCOORD4;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                v.vertex.xyz = Wobbled(v.vertex.xyz);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.height = v.vertex.y;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 view = normalize(_WorldSpaceCameraPos - i.wpos);
                float facing = saturate(dot(n, view));
                float edge = 1 - facing;

                // Thick in the middle and at the foot, thin and bright round the rim and the crown.
                float thick = facing * facing * saturate(1.15 - i.height);
                fixed3 body = lerp(_Color.rgb, _Deep.rgb, thick * 0.85);

                float ndl = dot(n, l);
                float wrap = saturate((ndl + 0.6) / 1.6);
                fixed3 sky = UNITY_LIGHTMODEL_AMBIENT.rgb;
                fixed3 col = body * (sky * (0.55 + 0.45 * n.y) + _LightColor0.rgb * wrap);

                // Light coming through from behind sets the rim glowing in the slime's own colour.
                float through = pow(saturate(dot(view, -l) * 0.5 + 0.5), 3) * edge;
                col += _Color.rgb * _LightColor0.rgb * through * 0.8;

                // Wet skin: one tight highlight, one broad one, and the sky all round the edge.
                float ndh = saturate(dot(n, normalize(l + view)));
                col += _LightColor0.rgb * (pow(ndh, 260) * 1.0 + pow(ndh, 24) * 0.14);
                col += sky * pow(edge, 3) * 0.75;

                // Elites carry a glow of their own.
                col += _OutlineColor.rgb * step(0.025, _OutlineWidth) * pow(edge, 2) * 1.2;

                col = lerp(col, fixed3(1, 1, 1), _Flash);
                fixed4 c = fixed4(col, lerp(0.84, 1, saturate(edge * 1.3 + thick * 0.6)));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert (appdata_base v)
            {
                v2f o;
                v.vertex.xyz = Wobbled(v.vertex.xyz);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
