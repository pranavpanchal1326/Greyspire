Shader "ToonArena/Toon"
{
    // The game's one surface shader. It began as a banded cartoon shader and keeps the
    // name; it now lights things the way light falls: smooth diffuse with soft shadows,
    // sky light from above and bounce from below, normal-mapped surface detail, and a
    // highlight whose tightness follows how glossy the surface is.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Bump ("Normal Map Strength", Range(0,2)) = 0
        [NoScaleOffset] _OrmMap ("Occlusion (R) Roughness (G) Metal (B)", 2D) = "white" {}
        _Orm ("Surface Map: 0 none, 1 occlusion-roughness-metal, 2 roughness only", Range(0,2)) = 0
        _Skin ("Skin: light bleeds red through the edge of the shade", Range(0,1)) = 0
        _Shade ("Shadow Depth", Range(0,1)) = 0.4
        _OutlineColor ("Aura Color", Color) = (0.08,0.07,0.12,1)
        _OutlineWidth ("Aura Width (below 0.025 draws nothing)", Range(0,0.1)) = 0
        _Rim ("Sky Sheen At Grazing Angles", Range(0,1)) = 0.16
        _Spec ("Gloss", Range(0,1)) = 0.14
        _Flash ("Hit Flash", Range(0,1)) = 0
        _World ("Drained By Gloom", Range(0,1)) = 0
        _Sway ("Wind Sway", Range(0,1)) = 0
        _Wash ("Wash (rgb = snow or ash, a = how much)", Color) = (1,1,1,0)
        _NearFade ("Dissolve Near Camera", Range(0,1)) = 0
        _Cutoff ("Alpha Cutout (leaves)", Range(0,1)) = 0
        _Dissolve ("Fading Away", Range(0,1)) = 0
        _Ground ("Ground Disc (mottled, frayed rim)", Range(0,1)) = 0
        _Color2 ("Ground Second Colour", Color) = (1,1,1,1)
        _Fray ("Ground Rim Fray (metres)", Float) = 0
        _Radius ("Ground Disc Radius (metres)", Float) = 1
        _Tile ("Ground Texture Repeats Per Metre", Float) = 0
        _Emit ("Lit From Inside (rgb, a = how much)", Color) = (0,0,0,0)
        [NoScaleOffset] _EmitMap ("Glow Map", 2D) = "black" {}
        _EmitMapOn ("Glow Map Strength", Range(0,4)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGINCLUDE
        #include "UnityCG.cginc"
        float _Sway;
        float4 _HeroPos;    // global: where the hero is standing

        // World position with wind: the higher above the object's base, the more it leans.
        float3 SwayedWorldPos(float4 vertex)
        {
            float3 wpos = mul(unity_ObjectToWorld, vertex).xyz;
            float h = max(0, wpos.y - unity_ObjectToWorld._m13);
            float wave = sin(_Time.y * 1.7 + wpos.x * 0.35 + wpos.z * 0.3) + 0.5 * sin(_Time.y * 2.9 + wpos.z * 0.6);
            wpos.xz += float2(0.8, 0.6) * (wave * h * h * 0.012 * _Sway);

            // Grass (anything with a strong sway) bends away from the hero's feet.
            float2 away = wpos.xz - _HeroPos.xz;
            float dist = length(away);
            float press = saturate(1 - dist / 1.5) * step(8, _Sway) * saturate(h * 4);
            wpos.xz += away / max(dist, 0.001) * press * 0.5;
            wpos.y -= press * h * 0.55;
            return wpos;
        }
        ENDCG

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            sampler2D _BumpMap;
            sampler2D _EmitMap;
            sampler2D _OrmMap;
            half _Orm;
            half _Skin;
            half _Bump;
            half _EmitMapOn;
            fixed _Shade;
            fixed _Flash;
            fixed _Rim;
            fixed _Spec;
            fixed _World;
            fixed _Cutoff;
            fixed _NearFade;
            fixed _Dissolve;
            fixed4 _Wash;
            float _WorldSat;
            fixed _Ground;
            fixed4 _Color2;
            float _Fray;
            float _Radius;
            float _Tile;
            fixed4 _Emit;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
                float2 uv : TEXCOORD4;
                float rim : TEXCOORD5;      // ground discs: metres in from the rim
                float3 tangent : TEXCOORD6;
                float3 binormal : TEXCOORD7;
            };

            // Cheap value noise over the ground plane.
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

            v2f vert (appdata_tan v)
            {
                v2f o;
                o.wpos = SwayedWorldPos(v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, float4(o.wpos, 1));
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.binormal = cross(o.normal, o.tangent) * (v.tangent.w * unity_WorldTransformParams.w);
                o.uv = v.texcoord.xy;
                // The disc mesh has radius 0.5 in its own space.
                o.rim = (1 - 2 * length(v.vertex.xz)) * _Radius;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                // Ground wears its texture by where it lies in the world, not by the disc's own mapping.
                float ground = step(0.5, _Ground);
                float2 uv = lerp(i.uv, i.wpos.xz * _Tile, ground * step(0.0001, _Tile));

                // Leaf cards are cut out of their texture and lit from both sides.
                fixed4 texel = tex2D(_MainTex, uv);
                clip(texel.a - _Cutoff);

                // Trees and rocks dissolve as the camera pushes into them, instead of filling the screen.
                float nearness = saturate((distance(_WorldSpaceCameraPos, i.wpos) - 1.2) / 3.2);
                float grain = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                clip(lerp(1, nearness - grain, _NearFade));
                clip(grain + 0.001 - _Dissolve * 1.01);

                // Surface detail from the normal map (a flat one, at strength nought, changes nothing).
                float side = facing >= 0 ? 1 : -1;
                float3 flat = normalize(i.normal) * side;
                half3 tn = UnpackNormal(tex2D(_BumpMap, uv));
                tn.xy *= _Bump;
                float3 onMesh = normalize(i.tangent) * tn.x + normalize(i.binormal) * tn.y + flat * tn.z;
                float3 onGround = float3(tn.x, tn.z, tn.y);
                float3 n = normalize(lerp(lerp(flat, onMesh, step(0.001, _Bump)), onGround, ground * step(0.001, _Bump)));

                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 view = normalize(_WorldSpaceCameraPos - i.wpos);
                fixed atten = SHADOW_ATTENUATION(i);

                fixed3 albedo = lerp(_Color.rgb * texel.rgb, _Wash.rgb, _Wash.a);

                // Ground: broad patches of a second colour, a finer grain over both, and a rim
                // that frays away unevenly, so one land runs into the next.
                float2 gp = i.wpos.xz;
                float broad = VNoise(gp * 0.085) * 0.65 + VNoise(gp * 0.31 + 17.3) * 0.35;
                float grainy = VNoise(gp * 1.7 + 5.1);
                clip(lerp(1, i.rim - _Fray * (VNoise(gp * 0.23 + 31.7) * 0.7 + grainy * 0.3), ground));
                fixed3 mottled = lerp(albedo, _Color2.rgb * texel.rgb, smoothstep(0.42, 0.6, broad) * 0.9) * (0.9 + 0.2 * grainy);
                albedo = lerp(albedo, mottled, ground);

                // Sunlight: smooth, with a little wrap so the edge of the shade is not a hard line.
                // Leaves let some light through from behind.
                float ndl = dot(n, l);
                float diffuse = saturate((ndl + 0.12) / 1.12);
                float leaf = step(0.01, _Cutoff);
                diffuse = lerp(diffuse, max(diffuse, 0.42 + 0.3 * saturate(-ndl)), leaf);
                float sun = diffuse * atten;

                // Sky light: cool and full from above, dim and a little warm from the ground below.
                float up = n.y * 0.5 + 0.5;
                fixed3 sky = UNITY_LIGHTMODEL_AMBIENT.rgb;
                fixed3 ambient = lerp(sky * fixed3(0.62, 0.56, 0.5) * 0.6, sky, up) * lerp(0.9, 0.56, _Shade);

                // The surface map, where the kit made one: creases are darker, rough places dull, metal bright.
                fixed4 orm = tex2D(_OrmMap, uv);
                float mapped = step(0.5, _Orm), full = mapped * step(_Orm, 1.5);
                float occlusion = lerp(1, orm.r, full);
                float metal = orm.b * full;
                float gloss = lerp(_Spec, (1 - orm.g) * (1 - orm.g) * 0.9 + metal * 0.25, mapped);
                ambient *= lerp(1, occlusion, 0.85);

                fixed3 col = albedo * (ambient + _LightColor0.rgb * sun * 0.82) * (1 - metal * 0.45);

                // Skin: where light gives way to shade it glows warm, as light does through flesh.
                float scatter = saturate((ndl + 0.45) / 1.45) - saturate(ndl);
                col += albedo * _LightColor0.rgb * fixed3(1.0, 0.32, 0.2) * (scatter * _Skin * 0.9 * lerp(0.5, 1, atten));

                // Highlight, tighter and brighter the glossier the surface (metal reflects in its own
                // colour); and the sky's sheen at grazing angles.
                float ndh = saturate(dot(n, normalize(l + view)));
                fixed3 glint = lerp(fixed3(1, 1, 1), albedo * 1.6, metal);
                col += _LightColor0.rgb * glint * (pow(ndh, lerp(6, 160, gloss)) * gloss * 1.6 * sun * occlusion);
                float graze = 1 - saturate(dot(n, view));
                col += sky * (graze * graze * graze * graze * max(_Rim, metal * 0.35) * 0.9 * occlusion);

                float drain = _World * (1 - saturate(_WorldSat));
                float grey = dot(col, float3(0.3, 0.59, 0.11));
                col = lerp(col, grey * fixed3(0.8, 0.8, 0.9), drain);

                // Window glass and embers glow of themselves.
                col += _Emit.rgb * _Emit.a;
                col += tex2D(_EmitMap, uv).rgb * _EmitMapOn;

                col = lerp(col, fixed3(1, 1, 1), _Flash);
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        // An aura round things that matter: elites, the Gloom King, a hero whose Colour Burst is
        // ready. Ordinary things have no line round them at all.
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            fixed _NearFade;
            fixed _Dissolve;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                float3 wpos = SwayedWorldPos(v.vertex);
                // Thicken with distance so the line keeps roughly the same width on screen.
                float grow = clamp(distance(_WorldSpaceCameraPos, wpos) * 0.11, 1.0, 4.0);
                wpos += UnityObjectToWorldNormal(v.normal) * _OutlineWidth * grow;
                o.wpos = wpos;
                // No aura wanted: push the hull off screen entirely.
                o.pos = _OutlineWidth < 0.025 ? float4(2, 2, 2, 1) : mul(UNITY_MATRIX_VP, float4(wpos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float nearness = saturate((distance(_WorldSpaceCameraPos, i.wpos) - 1.2) / 3.2);
                float grain = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                clip(lerp(1, nearness - grain, _NearFade));
                clip(grain + 0.001 - _Dissolve * 1.01);
                return _OutlineColor;
            }
            ENDCG
        }

        // Shadows follow the wind and the leaf cut-outs, so a tree casts the shape of its leaves.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _Cutoff;
            fixed _Dissolve;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                v.vertex = mul(unity_WorldToObject, float4(SwayedWorldPos(v.vertex), 1));
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a - _Cutoff);
                clip(0.5 - _Dissolve);      // a body half gone casts no shadow
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
