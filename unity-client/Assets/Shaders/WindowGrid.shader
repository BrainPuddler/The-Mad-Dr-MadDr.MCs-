// docs/33 (window glow GPU system): one shared, SRP-batcher-friendly
// material for EVERY window in the city. A building's entire window set
// is one merged mesh (BuildingWindowGrid.cs) with this material -- ONE
// draw call per building, zero per-window GameObjects/Renderers/Lights.
//
// Per-window identity/state is split two ways:
//  - STATIC per-window data (seed, brightness variance, the window's own
//    randomized "someone's home"/"lights out" cycle fractions, whether it
//    can ever glow at all) is baked into vertex data at mesh-build time
//    (TEXCOORD1/2) -- it never changes after the building is built, so it
//    costs nothing at runtime beyond the interpolator bandwidth every
//    other vertex attribute already costs.
//  - DYNAMIC per-window override state (the mandatory gameplay
//    SetWindowOn/SetWindowOff API) lives in a small per-building
//    "override texture" sampled via TEXCOORD3, set per-MaterialPropertyBlock
//    by BuildingWindowGrid. Default texel value (0.5) means "no
//    override -- run the ambient day/night occupancy schedule below";
//    0 forces off, 1 forces on.
//
// The ambient schedule itself (arrival/bedtime occupancy gate, a hard
// on/off flip, never a fade or a wobble -- 2026-08 creator direction:
// "Always like a light switch NOT a dimmer") is computed ENTIRELY in the
// fragment shader from a handful of globals (_MadDrWin*, pushed once per
// frame by BuildingWindowGridDriver, not per building/window) plus the
// static per-vertex data above -- this is the same math EmissiveAnimator.cs's
// LightBehaviorKind.Window used to run per-instance in C# every frame (see
// that file's history for the original derivation), ported to the GPU so
// it costs nothing on the CPU regardless of how many windows exist.
//
// Built on top of Unity's own stock "Universal Render Pipeline/Lit"
// shadow/depth passes via UsePass (reused verbatim, not reimplemented) --
// only the ForwardLit pass below is hand-authored, to keep the amount of
// from-scratch HLSL as small as possible. This project's pipeline is
// confirmed URP (Unity 6000.3.13f1, URP 17.3.0) -- see
// .claude/skills/maddr-lighting-system.
Shader "MadDr/WindowGrid"
{
    Properties
    {
        // 2026-08 (creator direction: "Loose the glazing effect. Just
        // solid colours."): no texture is sampled by the ForwardLit pass
        // below any more -- solid _WarmColor/_DarkGlassColor/_FrameColor
        // only (the pane-vs-frame split below is a per-window UV distance
        // check, not a texture). Declared (with a harmless "white"
        // default) purely because the ShadowCaster/DepthOnly passes
        // reused via UsePass below expect a _BaseMap to exist on the
        // shader they came from; same reasoning as _Cutoff just below.
        _BaseMap("Unused (kept for UsePass compatibility)", 2D) = "white" {}

        // 2026-08 (creator direction: "the blue lights windows are too
        // blue just stay to the original warm tones"): every lit window
        // uses this ONE color now -- no per-window warm/cool split. Value
        // matches BaseDresser.cs's own pre-existing faction-building
        // window color (`new Color(1f, 0.85f, 0.55f)`), i.e. this
        // restores the original tone rather than inventing a new one.
        _WarmColor("Lit Window Color", Color) = (1, 0.85, 0.55, 1)
        _DarkGlassColor("Dark/Off Glass Color", Color) = (0.16, 0.2, 0.28, 1)

        // 2026-08 (creator report: "look too flat and pasted on"): a flat
        // single-normal quad has zero shading variation of its own, so
        // with no texture (per the prior "just solid colours" direction)
        // there was nothing left to distinguish "a window" from "a
        // rectangle of color stuck on the wall." Real windows aren't
        // just glass, they're glass INSET into a frame/sash -- a plain
        // solid color, ordinarily lit (never emissive, never subject to
        // the on/off schedule below), around the pane's edge. That's the
        // depth cue being restored here, without reintroducing a texture.
        _FrameColor("Window Frame/Sash Color", Color) = (0.08, 0.07, 0.06, 1)
        _FrameWidth("Frame Width (fraction of pane, per edge)", Range(0, 0.4)) = 0.12

        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _BulbEmissiveBase("Bulb Emissive Base", Float) = 0.25

        // Not read by the ForwardLit pass below (windows are always
        // opaque, never alpha-cutout) -- declared only because the
        // ShadowCaster/DepthOnly passes reused via UsePass below expect it
        // to exist on the shader they came from; harmless if unread.
        _Cutoff("Alpha Cutoff (unused, opaque)", Range(0,1)) = 0.5

        // [MainColor] so RuntimeCityBuilder's damage-darken pass (which
        // sets BOTH _BaseColor -- URP/Lit's MainColor property -- and
        // _Color -- Built-in/Standard's -- via MaterialPropertyBlock,
        // unconditionally, on every renderer under a damaged building)
        // actually darkens windows too, the same as every other prop on
        // that building. Multiplied into both albedo and emission below.
        [MainColor] _BaseColor("Damage Tint (set by RuntimeCityBuilder)", Color) = (1,1,1,1)

        // Per-building override state texture (single channel, R). Set
        // per-instance via MaterialPropertyBlock by BuildingWindowGrid --
        // NOT a shared global, every building has its own. Declared here
        // (with a harmless default) purely so SRP-batcher-compatible
        // per-object override plumbing exists; the real texture is always
        // supplied per-renderer.
        [NoScaleOffset] _OverrideStateTex("Override State (per building)", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // 2026-08 (creator report: "dots. Not rectangles" -- the GPU
            // window-grid conversion, docs/33): no Cull state here means
            // the default Cull Back applies. AddWindow's quad winding
            // (BuildingWindowGrid.Build) comes from `right x up` alone --
            // it does NOT look at `normal` to correct itself, and its
            // CALLERS hand it inconsistent tangent handedness for
            // opposite-facing walls -- e.g.
            // BuildingDresser.SpawnWindowStrip passes the SAME
            // Vector3.right tangent for both its Vector3.forward AND
            // Vector3.back calls, and FacadeKit.Tangent(face) does the
            // same per-axis (PlusX and MinusX both return Vector3.forward).
            // That flips the resulting triangle winding on roughly half of
            // every building's walls, back-face-culling those windows
            // entirely -- leaving only the sparse DynamicLightBudget real
            // lights (which are genuinely round point lights) visible, no
            // rectangles at all on the culled side. Cull Off sidesteps
            // needing to hand-derive and get right which specific callers
            // have the wrong handedness (no Editor here to render-check
            // against) -- a window pane is never seen from its "back" side
            // anyway (there's no interior geometry to view it from), so
            // double-siding it is a safe, no-downside fix rather than a
            // guess at Unity's CW/CCW convention that could just as
            // easily cull the WRONG side instead.
            Cull Off

            HLSLPROGRAM
            #pragma vertex WindowGridVertex
            #pragma fragment WindowGridFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WarmColor;
                half4 _DarkGlassColor;
                half4 _FrameColor;
                half4 _BaseColor;
                half _FrameWidth;
                half _Smoothness;
                half _BulbEmissiveBase;
            CBUFFER_END

            TEXTURE2D(_OverrideStateTex); SAMPLER(sampler_OverrideStateTex);

            // Pushed once per frame by BuildingWindowGridDriver -- global,
            // NOT per-material, so every building's shared material reads
            // the same live day/night state without a per-object update.
            float _MadDrWinCycleProgress;      // DayNightState.CycleProgress
            float _MadDrWinLightActivity;      // DayNightState.LightActivity
            float _MadDrWinNeonBoost;          // DayNightState.NeonBoost
            float _MadDrWinScheduleEnabled;    // EmissiveAnimator.WindowScheduleEnabled ? 1 : 0
            // NOTE: onFrac/offFrac/activityThreshold arrive already baked
            // per-window in TEXCOORD2 (BuildingWindowGrid.Build()) -- the
            // On/OffRangeStart/End constants that PRODUCE those values are
            // consumed entirely on the C# side and have no shader-side
            // counterpart. 2026-08 (creator direction: "Always like a
            // light switch NOT a dimmer" + "NEVER flash on and off in
            // short intervals"): the on/off gates below used to be
            // smoothstep-banded (a ~2.4s fade) and lit windows used to
            // wobble with a sine flicker on top -- both removed. There is
            // no shader-side transition-width/flicker-speed knob left at
            // all; the switch is instant and binary by construction now,
            // not a tunable that happens to be set narrow.

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Per-window 0..1 corner UV (BuildingWindowGrid.Build()) --
                // no longer used to sample a texture (2026-08, "just solid
                // colours"), but used below to find each pixel's distance
                // to the pane's own edge for the frame/sash inset.
                float2 uv0        : TEXCOORD0;
                // (seed, brightnessVar, flagsPacked, unused)
                float4 packed1    : TEXCOORD1;
                // (onFrac, offFrac, activityThreshold, unused)
                float4 packed2    : TEXCOORD2;
                // (overrideU, overrideV, unused, unused)
                float4 overrideUV : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float4 packed1    : TEXCOORD1;
                float4 packed2    : TEXCOORD2;
                float2 overrideUV : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS   : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                float  fogFactor  : TEXCOORD7;
            };

            Varyings WindowGridVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.uv0 = IN.uv0;
                OUT.packed1 = IN.packed1;
                OUT.packed2 = IN.packed2;
                OUT.overrideUV = IN.overrideUV.xy;
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            // Mirrors EmissiveAnimator.cs's LightBehaviorKind.Window case +
            // OccupancyGate, moved to the GPU: same per-window
            // seed/cycle-fraction data that used to be computed once at
            // EmissiveAnimator.Register time and is now baked into the
            // vertex stream once at mesh-build time instead. 2026-08:
            // both sides dropped the sine wobble and switched every gate
            // from a smoothstep band to a hard step -- see this file's
            // header and EmissiveAnimator.OccupancyGate's own comment for
            // the "light switch, not a dimmer" direction this implements.
            half MadDrWindowMultiplier(float onFrac, float offFrac, float activityThreshold,
                half canGlow, half alwaysOn, half overrideVal)
            {
                // Gameplay override always wins, even over a window that
                // ambiently never glows -- SetWindowOn/Off is meant to work
                // on ANY window by id (docs/33), not just ones the ambient
                // schedule already made eligible. 0 = forced off, 1 =
                // forced on, ~0.5 (default, untouched texel) = fall through
                // to the ambient schedule below.
                if (overrideVal < 0.25h) return 0.0h;
                if (overrideVal > 0.75h) return 1.0h;

                if (canGlow < 0.5h) return 0.0h;
                if (_MadDrWinScheduleEnabled < 0.5 || alwaysOn > 0.5h) return 1.0h;

                float t = _MadDrWinCycleProgress;
                half onGate = (half)step(onFrac, t);
                half offGate = (half)(1.0 - step(offFrac, t));
                half activityGate = (half)step(activityThreshold, _MadDrWinLightActivity);
                return min(onGate, min(offGate, activityGate));
            }

            half4 WindowGridFragment(Varyings IN) : SV_Target
            {
                float brightnessVar = IN.packed1.y;
                float flags = IN.packed1.z;

                // flagsPacked = canGlow*1 + alwaysOn*2
                half canGlow = (half)step(0.5, fmod(flags, 2.0));
                float rest = floor(flags / 2.0);
                half alwaysOn = (half)step(0.5, fmod(rest, 2.0));

                half overrideVal = SAMPLE_TEXTURE2D(_OverrideStateTex, sampler_OverrideStateTex, IN.overrideUV).r;

                half mult = MadDrWindowMultiplier(IN.packed2.x, IN.packed2.y, IN.packed2.z,
                    canGlow, alwaysOn, overrideVal);

                // Frame/sash inset: crisp-edged (not a soft vignette, to
                // match this project's flat-color aesthetic), a fixed
                // fraction of the pane's own UV size on every edge, so it
                // scales with each window's own dimensions automatically.
                float edgeDist = min(min(IN.uv0.x, 1.0 - IN.uv0.x), min(IN.uv0.y, 1.0 - IN.uv0.y));
                half isFrame = (half)step(edgeDist, (float)_FrameWidth);

                half3 litColor = _WarmColor.rgb * _BaseColor.rgb;
                half3 paneAlbedo = lerp(_DarkGlassColor.rgb, litColor, mult) * _BaseColor.rgb;
                half3 albedo = lerp(paneAlbedo, _FrameColor.rgb * _BaseColor.rgb, isFrame);

                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                half3 ambient = SampleSH(normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = albedo * (ambient + mainLight.color * (ndotl * mainLight.shadowAttenuation * mainLight.distanceAttenuation));

                // The frame is wood/metal, not glass -- ordinarily lit
                // (the `diffuse` term above already covers it) but never
                // emissive and never gated by the occupancy schedule,
                // same as the wall around it.
                half3 emission = lerp(litColor * _BulbEmissiveBase * brightnessVar * mult * _MadDrWinNeonBoost,
                    half3(0, 0, 0), isFrame);

                half3 color = diffuse + emission;
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Reuse Unity's own stock URP Lit passes verbatim for shadows/depth
        // rather than hand-authoring them -- these don't need any of the
        // window-state logic above (a shadow caster only needs position),
        // and hand-rolling them blind (no Editor to verify against) would
        // be pure added risk for zero benefit over the tested stock passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
