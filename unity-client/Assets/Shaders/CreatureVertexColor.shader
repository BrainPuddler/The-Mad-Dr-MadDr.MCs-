// docs/39 §11 item 2: one shared, SRP-batcher-friendly material for
// every creature's merged body mesh -- LabMeshBuilder.AttachChunksMerged
// bakes each chunk's own flat (color, gloss, emissive) into per-VERTEX
// color instead of one Material/Renderer per chunk (packages/creature-
// mesh's own dedup already collapses same-material primitives into one
// chunk each; this shader is what lets DIFFERENT-colored chunks share
// ONE mesh + ONE material too). 12-23 renderers per creature (docs/39
// §4.1's measured baseline) collapses to 2-3: one merged opaque mesh,
// one merged emissive-parts mesh (eyes, neon, heart bolts -- rare
// enough per creature that approximating their individual Emissive
// STRENGTH with this material's one shared _EmissionStrength value is
// an acceptable "cheaper way to get the same read at 70 m" trade, same
// as every other approximation docs/39 sanctions), and any translucent
// chunk (glass dome, blob gelatin) kept unmerged exactly as docs/39
// item 2 says to ("the translucent blob shell stays a second
// renderer") -- that one still uses LabMeshBuilder.ToMaterial's
// original per-chunk URP/Lit path, not this shader.
//
// Built on Unity's own stock "Universal Render Pipeline/Lit"
// shadow/depth passes via UsePass (reused verbatim, not reimplemented,
// same reasoning and same dummy-property set as Assets/Shaders/
// WindowGrid.shader, docs/33's precedent for a hand-authored shader in
// this codebase) -- only the ForwardLit pass below is hand-authored.
//
// 2026-09-16 (creator report, real Editor session: "flat shaded at all
// LOD levels"): the first version of this shader dropped the specular
// term entirely, reasoning it as "matching WindowGrid's plain-diffuse
// model" -- wrong call for THIS subject. WindowGrid shades flat building
// facades, where a highlight adds nothing; every creature chunk this
// shader replaced used to carry its own real Gloss value into URP/Lit's
// full PBR specular response, and losing that entirely (not just
// approximating it) is what read as "flat" -- pure Lambertian diffuse
// on a curved organic/mechanical surface has none of the roundness cue
// a highlight provides. Restored below as a hand-rolled Blinn-Phong
// term (not a call into URP's own BRDF/PBR helpers, whose exact
// signatures for this URP version can't be confirmed without an Editor
// -- this uses only `dot`/`pow`/`normalize` and the always-available
// `_WorldSpaceCameraPos` global, so it doesn't depend on guessing an
// API this environment still has no way to compile-check). One shared
// `_Smoothness` per material group (opaque vs. emissive), same
// "shared uniform approximates per-chunk variation" trade as
// `_EmissionStrength` already makes -- not the per-chunk Gloss value
// URP/Lit used to read, but real specular response instead of none.
Shader "MadDr/CreatureVertexColor"
{
    Properties
    {
        // Declared only because the ShadowCaster/DepthOnly/DepthNormals
        // passes reused via UsePass below expect a _BaseMap/_Cutoff to
        // exist on the shader they came from -- same reasoning as
        // WindowGrid.shader's identical pair. Never sampled by the
        // ForwardLit pass here.
        _BaseMap("Unused (kept for UsePass compatibility)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff (unused, opaque)", Range(0,1)) = 0.5

        // [MainColor] so any future damage-tint pass that follows
        // RuntimeCityBuilder's existing convention (set _BaseColor via
        // MaterialPropertyBlock) would work here too -- not used by any
        // creature system today, kept for consistency with the rest of
        // this codebase's hand-authored shaders (WindowGrid.shader's
        // own _BaseColor plays the same role).
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)

        // 0 for the shared OPAQUE-group material instance, >0 for the
        // shared EMISSIVE-group instance (LabMeshBuilder picks the
        // value; see its own header comment for why one shared constant
        // approximates every emissive chunk's individual Emissive
        // strength rather than reproducing it exactly).
        _EmissionStrength("Emission Strength (0 = opaque-group material)", Float) = 0

        // Blinn-Phong specular response (see the 2026-09-16 header note)
        // -- one shared value per material group, LabMeshBuilder-set,
        // same approximation trade as _EmissionStrength.
        _Smoothness("Smoothness (drives specular highlight tightness/strength)", Range(0,1)) = 0.35

        // docs/40 §3 item 3: a cool rim/fresnel term for night-silhouette
        // pop -- monsters are 26 px at the default zoom (docs/39 §1.1,
        // roughly a third of Empire of Sin's own 30-60 px characters), so
        // contrast against a dark background has to do more of the read
        // than pixel count can. Cool blue-white, matching the same
        // night-ambient tint color docs/28 row 31 already settled on for
        // this project's whole night palette, so a rim-lit monster reads
        // as part of the same lighting mood as the city around it rather
        // than a mismatched new color. Strength is scaled at runtime by
        // the GLOBAL _MadDrNightAmount below, not this per-material
        // value alone -- this Range is a per-material-group ceiling
        // multiplier, same "one shared value" idiom as _Smoothness.
        _RimColor("Rim Color", Color) = (0.55, 0.72, 1, 1)
        _RimPower("Rim Power (fresnel falloff sharpness)", Range(0.5, 8)) = 2.5
        _RimIntensity("Rim Intensity (ceiling, scaled by night amount at runtime)", Range(0,3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex CreatureVertexColorVertex
            #pragma fragment CreatureVertexColorFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EmissionStrength;
                half _Smoothness;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
            CBUFFER_END

            // docs/40 §3 item 3: a true global, NOT inside the per-
            // material CBUFFER above -- set once per frame from
            // LumenCycleController.ApplyBlend via Shader.SetGlobalFloat,
            // read identically by every creature material with zero
            // per-instance update cost. Declaring it outside
            // UnityPerMaterial is what keeps this SRP-Batcher-safe (a
            // per-object override would defeat batching per docs/39 §7;
            // this is the same category as Unity's own built-in globals
            // like _WorldSpaceCameraPos, not a per-draw property).
            half _MadDrNightAmount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Per-vertex baked chunk color (LabMeshBuilder.ToMergedMesh)
                // -- this is the whole point of this shader: URP/Lit itself
                // has no vertex-color input, so DIFFERENT-colored chunks
                // could never share one mesh/material without it.
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 color       : COLOR;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            Varyings CreatureVertexColorVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.color = IN.color;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 CreatureVertexColorFragment(Varyings IN) : SV_Target
            {
                half3 albedo = IN.color.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                half3 ambient = SampleSH(normalWS);
                half lightAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = albedo * (ambient + mainLight.color * (ndotl * lightAtten));

                // Blinn-Phong specular -- see the 2026-09-16 header note
                // ("flat shaded" fix). viewDirWS from _WorldSpaceCameraPos
                // rather than a URP helper macro, for the same
                // can't-verify-the-exact-API-here reason as everything
                // else unusual about this pass.
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                half specAngle = saturate(dot(normalWS, halfDirWS));
                half specPower = lerp(4.0h, 64.0h, _Smoothness);
                half specStrength = pow(specAngle, specPower) * _Smoothness;
                half3 specular = specStrength * mainLight.color * lightAtten;

                // Emission reuses the SAME per-vertex color as albedo (a
                // glowing eye's emission is the same hue as its lit
                // color) -- only the STRENGTH is the one shared uniform
                // this material can't vary per-chunk any more, per the
                // header comment above.
                half3 emission = IN.color.rgb * _EmissionStrength;

                // docs/40 §3 item 3: standard Schlick-style fresnel rim
                // term (1 - N.V, powered) -- reuses viewDirWS already
                // computed above for the specular half-vector, no extra
                // per-pixel work beyond one more dot/pow. Scaled by
                // _MadDrNightAmount (0 all through Day, per
                // LumenCycleController's own existing curve) so the rim
                // is invisible in daylight and strengthens exactly when
                // the background gets darker and silhouette readability
                // needs it most -- never an always-on outline glow.
                half rimFresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 rim = _RimColor.rgb * (rimFresnel * _RimIntensity * _MadDrNightAmount);

                half3 color = diffuse + specular + emission + rim;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Reuse Unity's own stock URP Lit passes verbatim for shadows/
        // depth rather than hand-authoring them -- same reasoning as
        // WindowGrid.shader's identical UsePass block: a shadow caster
        // only needs position, and hand-rolling one blind (no Editor to
        // verify against in this environment) would be pure added risk
        // for zero benefit over the tested stock passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
