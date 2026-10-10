// A procedural skin for the grey box, so the street reads as Mirpur before any art exists.
// One shader, a _Mode per material:
//   0 facade    concrete frame blocks: a window row per storey (from the mesh's uv.y = storeys),
//               shutters and signboards on the ground floor, a random tint per block (uv.x = seed),
//               slab lines, grime that climbs from the pavement and streaks down from the roof
//   1 asphalt   patched dark tarmac
//   2 pavement  pale concrete slabs
//   3 concrete  the viaduct and the stations
//   4 dirt      the ground between things
// Everything is hashed from world position: no textures, nothing to download, nothing to license.
// The route runs north to south (z from 0 at Mirpur 12 to -11,200 at Azimpur), and the facades change
// by zone along it (zoneOf): Mirpur's mixed residential rows; Agargaon's government blocks, concrete
// and white, few shops; Farmgate and Karwan Bazar, shops and signboards on every ground floor, grimy;
// the university stretch at Shahbagh and TSC, red brick and old white, hardly a shutter; Nilkhet and
// Azimpur, old Dhaka's lime and pastel, book stalls and shops, the dampest walls. The bands are world
// z thresholds between the named stops (stops.json); the characters are the builder's reading of the
// districts, to be checked against street photographs (S-026) when the art pass comes.
// Lit by the main light with shadows and the sky's ambient; URP, SRP-batcher compatible.
Shader "Twenty Tons/Surface"
{
    Properties
    {
        _Mode ("Mode (0 facade 1 asphalt 2 pavement 3 concrete 4 dirt)", Float) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            // ---- noise: cheap hashes of world coordinates
            float hash1(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float hash1(float p) { return hash1(float2(p, p * 1.37 + 0.11)); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash1(i), b = hash1(i + float2(1, 0)), c = hash1(i + float2(0, 1)), d = hash1(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm(float2 p) { return 0.5 * vnoise(p) + 0.25 * vnoise(p * 2.03) + 0.125 * vnoise(p * 4.1) + 0.0625 * vnoise(p * 8.3); }

            // ---- palettes seen on Mirpur blocks: raw concrete, lime wash, pale yellow, salmon, sky, brick
            float3 wallColour(float seed)
            {
                float k = seed * 7.999;
                if (k < 1.0) return float3(0.62, 0.60, 0.56);   // raw concrete
                if (k < 2.0) return float3(0.86, 0.84, 0.76);   // lime wash
                if (k < 3.0) return float3(0.88, 0.80, 0.52);   // pale yellow
                if (k < 4.0) return float3(0.84, 0.62, 0.52);   // salmon
                if (k < 5.0) return float3(0.66, 0.76, 0.84);   // sky
                if (k < 6.0) return float3(0.58, 0.36, 0.30);   // exposed brick
                if (k < 7.0) return float3(0.78, 0.78, 0.74);   // old white
                return float3(0.70, 0.74, 0.60);                // faded green
            }
            float3 shutterColour(float r)
            {
                float k = r * 5.999;
                if (k < 1.0) return float3(0.15, 0.30, 0.60);
                if (k < 2.0) return float3(0.20, 0.45, 0.30);
                if (k < 3.0) return float3(0.60, 0.18, 0.15);
                if (k < 4.0) return float3(0.55, 0.55, 0.52);
                if (k < 5.0) return float3(0.75, 0.55, 0.15);
                return float3(0.30, 0.30, 0.32);
            }

            // Which stretch of the route a point is in, by world z: 0 Mirpur, 1 Agargaon, 2 Farmgate and
            // Karwan Bazar, 3 the university, 4 Nilkhet and Azimpur.
            float zoneOf(float3 p)
            {
                if (p.z > -4800.0) return 0.0;
                if (p.z > -7300.0) return 1.0;
                if (p.z > -9500.0) return 2.0;
                if (p.z > -10700.0) return 3.0;
                return 4.0;
            }

            // Rule learned the hard way: the Mac shader compiler process dies on a noise call inside a
            // branch. Every hash and noise value is computed up front; the branches only pick colours.
            float3 facade(float3 p, float3 n, float seed, float storeys, out float gloss)
            {
                gloss = 0.0;
                // The zone's character: which colours its walls draw from, how many ground floors are shops,
                // how many of those hang a signboard, how dirty the walls are.
                float zone = zoneOf(p);
                float wallSeed = seed, shopShare = 0.55, signShare = 0.6, grime = 1.0;
                if (zone > 0.5 && zone < 1.5) { wallSeed = seed * 0.25 + 0.75 * step(0.7, seed); shopShare = 0.2; signShare = 0.4; grime = 0.7; }        // concrete, lime wash, old white
                else if (zone > 1.5 && zone < 2.5) { wallSeed = 0.25 + seed * 0.75; shopShare = 0.9; signShare = 0.85; grime = 1.3; }                  // yellow, salmon, brick, sky
                else if (zone > 2.5 && zone < 3.5) { wallSeed = 0.625 + seed * 0.25; shopShare = 0.15; signShare = 0.3; grime = 0.8; }                 // red brick and old white
                else if (zone > 3.5) { wallSeed = 0.125 + seed * 0.4; shopShare = 0.75; signShare = 0.7; grime = 1.4; }                                 // lime, yellow, salmon
                float3 wall = wallColour(wallSeed) * (0.85 + 0.3 * hash1(seed + 0.7));
                float along = p.x;
                if (abs(n.x) > abs(n.z)) along = p.z;
                float up = p.y;
                float floorH = 3.0;
                float storey = floor(up / floorH);
                float fy = frac(up / floorH);                       // 0 at the slab, 1 at the next slab
                float cellW = 2.6 + 0.8 * hash1(seed + 3.1);
                float cx = frac(along / cellW);
                float cellId = floor(along / cellW);

                // Everything random, decided before any branch.
                float shopR = hash1(float2(cellId, seed * 91.0));
                float3 shutter = shutterColour(shopR);
                float slats = 0.85 + 0.15 * step(0.5, frac(up * 6.0));
                float letters = step(0.55, hash1(float2(floor(along * 4.0), floor(up * 7.0) + seed)));
                float lit = hash1(float2(cellId, storey * 7.0 + seed));
                float balcony = step(0.6, hash1(float2(cellId * 3.0, storey + seed * 17.0)));
                float streak = fbm(float2(along * 0.6, up * 0.08));
                float stain = fbm(float2(along * 0.25, up * 0.25)) * 0.25;
                float damp = saturate(1.0 - up / 3.0) * 0.45 * grime;
                float grille = step(frac(along * 3.0), 0.12) + step(frac(up * 3.0), 0.12);
                float3 board = float3(0.8, 0.1, 0.1);
                if (shopR < 0.33) board = float3(0.1, 0.3, 0.7);
                else if (shopR < 0.66) board = float3(0.95, 0.75, 0.1);
                bool shops = hash1(seed + 5.3) < shopShare;            // a row of shops, or a house with a gate
                bool signboard = hash1(float2(cellId + 7.0, seed)) < signShare;
                bool gate = hash1(float2(cellId * 1.7, seed + 2.2)) < 0.3;

                float3 col = wall;
                bool hasFloors = storeys > 0.5;
                if (hasFloors && storey < 0.5 && shops)
                {
                    // Ground floor of a shop row: roller shutters below, a signboard above on most.
                    if (fy < 0.78 && cx > 0.06 && cx < 0.94) col = shutter * slats;
                    else if (signboard && fy >= 0.80 && fy < 0.98 && cx > 0.03 && cx < 0.97) col = lerp(board, float3(0.95, 0.95, 0.9), letters * 0.8);
                    else col = wall * 0.8;
                }
                else if (hasFloors && storey < 0.5)
                {
                    // Ground floor of a house: a boundary wall's worth of plain, a steel gate on some cells,
                    // a barred window on the rest.
                    float wx = abs(cx - 0.5);
                    if (gate && fy < 0.8 && wx < 0.35) col = float3(0.22, 0.24, 0.27) * (0.9 + 0.1 * step(0.5, frac(along * 2.0)));
                    else if (!gate && wx < 0.18 && fy > 0.35 && fy < 0.7) col = float3(0.12, 0.14, 0.17) + (grille > 0.5 ? float3(0.1, 0.1, 0.1) : 0.0);
                    else col = wall * 0.85;
                    if (fy > 0.95) col = wall * 0.7;
                }
                else if (hasFloors && storey < storeys)
                {
                    // Upper floors: a window per cell, a grille on it, a balcony on some, a slab line.
                    float wx = abs(cx - 0.5), wy = fy - 0.45;
                    bool window = wx < 0.22 && wy > -0.17 && wy < 0.22;
                    bool frame = wx < 0.26 && wy > -0.21 && wy < 0.26;
                    if (window)
                    {
                        col = float3(0.12, 0.14, 0.17) + 0.08 * lit;
                        if (grille > 0.5) col = float3(0.2, 0.2, 0.2);
                        gloss = 0.6;
                    }
                    else if (frame) col = wall * 1.15;
                    if (balcony > 0.5 && fy < 0.30 && wx < 0.42) col = wall * 0.72;
                    if (fy < 0.05 || fy > 0.97) col = wall * 0.70;
                }
                else if (hasFloors)
                {
                    col = wall * 0.9;                                   // parapet and stair head
                }
                if (hasFloors && (cx < 0.03 || cx > 0.97)) col = lerp(col, float3(0.55, 0.53, 0.5), 0.6);   // the frame's columns
                col *= 1.0 - damp - stain * 0.9 * grime - 0.2 * streak * grime;
                return col;
            }

            float3 surfaceOf(float3 p, float3 n, float mode, out float gloss)
            {
                gloss = 0.0;
                float2 q;
                if (abs(n.y) > 0.5) q = float2(p.x, p.z);
                else if (abs(n.x) > abs(n.z)) q = float2(p.z, p.y);
                else q = float2(p.x, p.y);
                // All noise first, then the pick (see the note above facade).
                float n1 = fbm(q * 0.7), n2 = fbm(q * 1.3), n3 = fbm(q * 0.5), n4 = fbm(q * 0.2);
                float patch = step(0.72, vnoise(q * 0.08));
                float rust = 0.25 * fbm(float2(q.x * 2.0, q.y * 0.15)) * step(abs(n.y), 0.5);
                float2 slab = frac(q / 0.6);
                float joint = saturate(step(slab.x, 0.06) + step(slab.y, 0.06));
                float2 form = frac(q / 2.4);
                float panel = saturate(step(form.x, 0.03) + step(form.y, 0.03));
                if (mode < 1.5)
                {
                    float g = 0.11 + 0.05 * n1 + 0.04 * patch;           // asphalt, patched
                    return float3(g, g, g * 1.08);
                }
                if (mode < 2.5)
                {
                    float g = 0.46 + 0.1 * n2;                            // pavement slabs with joints
                    return lerp(float3(g, g, g * 0.95), float3(0.35, 0.36, 0.3), joint);
                }
                if (mode < 3.5)
                {
                    float g = 0.55 + 0.12 * n3;                           // concrete, formwork, rust down the piers
                    float3 col = float3(g, g - rust * 0.5, g * 1.02 - rust);
                    return lerp(col, col * 0.7, panel);
                }
                float d = 0.5 + 0.15 * n4;                                // dirt
                return float3(d, d * 0.92, d * 0.78);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float gloss;
                float3 albedo;
                if (_Mode < 0.5)
                {
                    // Roofs of blocks are concrete slabs with grime; walls get the facade.
                    if (abs(n.y) > 0.5) albedo = surfaceOf(IN.positionWS, n, 3.0, gloss) * 0.55;
                    else albedo = facade(IN.positionWS, n, IN.uv.x, IN.uv.y, gloss);
                }
                else albedo = surfaceOf(IN.positionWS, n, _Mode, gloss);
                albedo *= _Tint.rgb;

                // Lighting: main light with shadows, sky ambient, a touch of glass glint.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light light = GetMainLight(shadowCoord);
                float ndl = saturate(dot(n, light.direction));
                float3 ambient = SampleSH(n) * 0.45;
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 h = normalize(light.direction + viewDir);
                float spec = pow(saturate(dot(n, h)), 48.0) * gloss;
                float3 col = albedo * (light.color * ndl * light.shadowAttenuation + ambient) + light.color * spec * 0.3;
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
