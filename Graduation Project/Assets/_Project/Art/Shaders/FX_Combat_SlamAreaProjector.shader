// FX_Combat_SlamAreaProjector — RetroFPS project shader (authored with Claude Code).
// Purpose: unlit box projector for ground attack telegraphs (Penitent slam area).
//   Drawn as the back faces of a unit cube: each pixel reads the scene depth, rebuilds the world position and
//   paints it only when it lies inside the box AND faces up within the allowed slope. Walls and step risers
//   stay clean; floors, steps and ramps at any height inside the box are covered with crisp per-pixel edges.
// Pipeline: URP 17 (Unity 6.5), Forward / Forward+. Needs the camera depth texture (PC_RPAsset: Depth Texture on).
//   Does not use the URP Decal renderer feature.
// Target / budget: PC. Only pixels covered by the box run the fragment: 5 depth loads, no texture samples,
//   no loops. One draw call per active telegraph (per-instance values come from a MaterialPropertyBlock).
// Variants: none (no keywords).
Shader "RetroFPS/SlamAreaProjector"
{
    Properties
    {
        [Header(Colours (premultiplied over each other))]
        [HDR] _AreaColor ("Area Background", Color) = (0.38, 0.025, 0.015, 0.24)
        [HDR] _BorderColor ("Area Border", Color) = (1, 0.16, 0.03, 0.9)
        [HDR] _FillColor ("Charge Fill", Color) = (1, 0.18, 0.035, 0.65)
        [HDR] _ImpactColor ("Impact Flash", Color) = (1, 0.85, 0.6, 0.9)

        [Header(State (driven per frame by PenitentView))]
        _Charge ("Charge Visible (0 or 1)", Range(0, 1)) = 1
        _Fill ("Charge Fill Progress (near to far)", Range(0, 1)) = 0
        _Impact ("Impact Flash Strength", Range(0, 1)) = 0

        [Header(Shape)]
        _BorderSize ("Border Size (x: lateral, y: forward; fractions of the box)", Vector) = (0.035, 0.012, 0, 0)
        _MinUpDot ("Min Surface Up Dot (cos of max slope)", Range(0, 1)) = 0.5
        _SlopeSoftness ("Slope Cut-off Softness (up-dot units)", Range(0.001, 0.2)) = 0.04
        _NearFade ("Camera Near Fade Distance (m)", Float) = 1.2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SlamAreaProjector"
            Tags { "LightMode" = "UniversalForward" }

            // Back faces + ZTest Always: still draws when the camera stands inside the box.
            // Occlusion comes from the scene depth itself, so nothing shows through walls.
            Cull Front
            ZTest Always
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AreaColor;
                float4 _BorderColor;
                float4 _FillColor;
                float4 _ImpactColor;
                float4 _BorderSize;
                float _Charge;
                float _Fill;
                float _Impact;
                float _MinUpDot;
                float _SlopeSoftness;
                float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float3 SceneWorldPosition(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            // Premultiplied "over" compositing.
            half4 Over(half4 top, half4 bottom)
            {
                return top + bottom * (1.0h - top.a);
            }

            half4 Premultiply(float4 color, float strength)
            {
                half alpha = (half)saturate(color.a * strength);
                return half4((half3)color.rgb * alpha, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 world = SceneWorldPosition(uv);

                // Inside the unit box only (object space -0.5..0.5 on every axis).
                float3 local = TransformWorldToObject(world);
                clip(0.5 - abs(local));

                // Fade out right next to the camera so the viewmodel and the player's own feet stay clean:
                // 0 at half the fade distance, 1 at the full fade distance.
                float3 toCamera = _WorldSpaceCameraPos - world;
                float halfFade = max(_NearFade * 0.5, 0.0001);
                float nearMask = saturate((length(toCamera) - halfFade) / halfFade);
                clip(nearMask - 0.001);

                // Surface normal from neighbouring depths. Taking the shorter step on each axis avoids
                // false normals along silhouettes, where one neighbour belongs to a different surface.
                float2 texel = _CameraDepthTexture_TexelSize.xy;
                float3 left = SceneWorldPosition(uv - float2(texel.x, 0));
                float3 right = SceneWorldPosition(uv + float2(texel.x, 0));
                float3 down = SceneWorldPosition(uv - float2(0, texel.y));
                float3 up = SceneWorldPosition(uv + float2(0, texel.y));
                float3 dx = distance(right, world) < distance(world, left) ? right - world : world - left;
                float3 dy = distance(up, world) < distance(world, down) ? up - world : world - down;
                float3 normal = normalize(cross(dy, dx));
                normal *= dot(normal, toCamera) < 0 ? -1.0 : 1.0;   // face the camera (select, not a branch)

                // Reject walls, risers and steep slopes; soften the cut-off slightly to avoid shimmer.
                clip(normal.y - _MinUpDot);
                float slopeMask = saturate((normal.y - _MinUpDot) / _SlopeSoftness);

                float forward = local.z + 0.5;           // 0 near edge .. 1 far edge
                float lateral = abs(local.x) * 2.0;      // 0 centre line .. 1 side edge
                float borderMask = max(step(1.0 - _BorderSize.x, lateral),
                    max(step(forward, _BorderSize.y), step(1.0 - _BorderSize.y, forward)));
                float fillMask = step(forward, _Fill) * step(0.0001, _Fill);

                half4 color = Premultiply(_AreaColor, _Charge);
                color = Over(Premultiply(_FillColor, _Charge * fillMask), color);
                color = Over(Premultiply(_BorderColor, _Charge * borderMask), color);
                color = Over(Premultiply(_ImpactColor, _Impact), color);
                return color * (half)(nearMask * slopeMask);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
