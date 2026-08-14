using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2026-08 (creator brief: "Character System Overhaul -- Replace Capsule
/// Characters"): a shared, modular, low-poly humanoid rig builder --
/// replaces the ad hoc single-capsule-plus-cylinder bodies <see
/// cref="Worker"/> and <see cref="Citizen"/> built for themselves. One
/// rig serves every human-shaped unit in the game (Worker's three
/// faction skins -- Human/Mad-Doctor/Alien -- and the new <see
/// cref="HumanSoldier"/>) via <see cref="HumanCharacterProfile"/>
/// parameters, not four separate builders -- exactly the brief's own
/// "interchangeable body parts... shared meshes whenever possible" ask.
///
/// Built entirely from `PrimitiveType.Cube` (12 triangles each) rather
/// than the stock Capsule/Cylinder/Sphere primitives Worker/Citizen used
/// to reach for -- Unity's built-in Capsule alone is several hundred
/// triangles, which alone blows the brief's ~120-300-triangle-per-
/// character budget. A full biped here (torso, head, 2x upper+lower arm,
/// 2x hand, 2x upper+lower leg, 2x foot = 13 parts) is 156 triangles;
/// the hover-only Alien variant (no legs/feet) is 84. Blocky cube limbs
/// are also a deliberate style fit, not just a budget compromise --
/// "simple and exaggerated for readability" (the brief's own animation-
/// style line) reads naturally as chunky stop-motion-model geometry,
/// consistent with this project's 1950s-monster-movie register
/// (maddr-aesthetic-preferences skill §1).
///
/// ONE shared, instanced `Material` for every character (<see
/// cref="SharedMaterial"/>) -- per-part/per-character color comes ENTIRELY
/// from a <see cref="MaterialPropertyBlock"/> on each part's Renderer
/// (both `_BaseColor` for this project's confirmed URP pipeline and
/// `_Color` for the Built-in/Standard fallback <see cref="ShaderUtil"/>
/// itself already falls back to), never a per-instance Material --
/// exactly the brief's "shared material across all characters... color
/// variations through Material Property Blocks" requirement, and the
/// same SRP-batcher-friendly idiom this codebase already uses everywhere
/// else (EmissiveAnimator, WindowGrid.shader's per-building override
/// texture).
///
/// This is PURE GEOMETRY -- no animation lives here (see <see
/// cref="HumanCharacterAnimator"/>). Every part below is either a
/// permanent visual leaf (Renderer) or a PIVOT (an empty Transform with
/// no Renderer of its own, existing purely so a caller can rotate it) --
/// procedural transform animation, matching this codebase's established
/// no-DCC-pipeline pattern (MonsterBody's foot-planted limb IK, Tank's
/// independent turret slerp) rather than a skinned mesh + bone rig,
/// which nothing in this environment (no Unity Editor, no animation
/// clip authoring tool) could actually produce.
///
/// UNVERIFIED IN A REAL RENDER (standing policy, this environment has no
/// Editor -- CLAUDE.md, docs/28 §5's own precedent for exactly this
/// situation): proportions and pivot placement below are reasoned from
/// the existing Citizen/Worker capsule scale (~1.8 world units tall) and
/// ordinary human body proportions, not measured against an actual
/// on-screen result.
/// </summary>
public static class HumanCharacterKit
{
    private static Material _sharedMaterial;

    /// <summary>The one Material every character part shares -- built
    /// once, lazily, same pattern <see cref="BuildingWindowGrid"/>'s own
    /// `SharedMaterial()` already established for exactly this reason
    /// (SRP-batcher-friendly, avoids a Material allocation per part per
    /// character at hundreds-of-units scale).</summary>
    public static Material SharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        var shader = ShaderUtil.FindRenderableShader();
        if (shader == null) return null;
        _sharedMaterial = new Material(shader) { enableInstancing = true };
        return _sharedMaterial;
    }

    /// <summary>Build a full rig under `parent` (typically the owning
    /// unit's own root Transform, positioned at ground height exactly
    /// like Worker/Citizen already do) and return every part/pivot the
    /// animator needs. `parent` gets exactly ONE BoxCollider sized to the
    /// character's rough bounds, sized/centered from `profile` -- NOT one
    /// collider per cube part (13 colliders per character would be real
    /// physics overhead at hundreds-of-units scale, for zero gameplay
    /// benefit over one bounding box; every existing selection/raycast
    /// path already climbs to the owning component via
    /// `GetComponentInParent`, so a single collider on the root is
    /// exactly as clickable as one per limb).</summary>
    public static HumanCharacterRig Build(Transform parent, HumanCharacterProfile profile)
    {
        var mat = SharedMaterial();
        var rig = new HumanCharacterRig { HasLegs = profile.HasLegs };

        var s = profile.HeightScale;
        var limbT = profile.LimbThicknessScale;
        var armLen = profile.ArmLengthScale;

        // Torso doubles as the "spine" pivot -- rotating it (idle
        // twitch, hunch, build/harvest lean, death collapse, hover bank)
        // carries the head and both arms with it for free, since they're
        // parented under it. Hip pivots are parented under `parent`
        // directly instead, so torso lean never drags the legs along --
        // real people don't tip their hips over when they crane their
        // neck to work.
        var hipY = (profile.HasLegs ? 0.92f : 1.02f) * s;   // hover variant sits a little higher, no legs to stand the torso on
        var torsoH = 0.6f * s;
        var torsoRestPos = new Vector3(0f, hipY + torsoH * 0.5f, 0f);
        var torso = Pivot(parent, torsoRestPos);
        torso.localRotation = Quaternion.Euler(profile.HunchDegrees, 0f, 0f);
        rig.Torso = torso;
        rig.TorsoRestLocalPos = torsoRestPos;
        rig.TorsoRestPitchDeg = profile.HunchDegrees;
        CubePart(torso, Vector3.zero, new Vector3(0.5f * s * profile.ShoulderWidthScale, torsoH, 0.3f * s),
            profile.BodyColor, mat, rig);

        var headSize = 0.32f * s;
        var head = Pivot(torso, new Vector3(0f, torsoH * 0.5f + headSize * 0.5f + 0.02f * s, 0f));
        rig.Head = head;
        CubePart(head, Vector3.zero, new Vector3(headSize, headSize, headSize), profile.BodyColor, mat, rig);
        BuildHeadgear(head, headSize, profile, mat, rig);

        // Asymmetry (Mad Doctor Worker: "slight asymmetry") nudges only
        // ONE shoulder's pivot height -- a small, deliberately uneven
        // offset reads as "stitched together wrong," not a modeling bug,
        // per this class's own header on why blocky/exaggerated geometry
        // is the right register here.
        BuildArm(torso, isLeft: true, s, limbT, armLen,
            asymmetryY: profile.Asymmetric ? -0.06f * s : 0f, profile, mat, rig);
        BuildArm(torso, isLeft: false, s, limbT, armLen, asymmetryY: 0f, profile, mat, rig);

        if (profile.HasLegs)
        {
            BuildLeg(parent, isLeft: true, s, limbT, hipY, profile, mat, rig);
            BuildLeg(parent, isLeft: false, s, limbT, hipY, profile, mat, rig);
        }

        if (profile.HasBackpack)
        {
            var pack = CubePart(torso, new Vector3(0f, 0.05f * s, -0.24f * s),
                new Vector3(0.34f * s, 0.4f * s, 0.16f * s), profile.AccentColor, mat, rig);
            rig.Backpack = pack;
        }

        // Single collider for the whole character -- see this method's
        // own doc comment for why. Sized to roughly bound the standing
        // silhouette regardless of which variant this is (hover units
        // are shorter -- no legs -- so the box is centered lower).
        // NOTE: hipY/torsoH/headSize below already have `s` baked in from
        // their own definitions above -- do not multiply this sum by `s`
        // again (a real bug caught during self-review: it would silently
        // quadratic-scale the collider for any profile whose HeightScale
        // isn't ~1, undersizing/oversizing selection hitboxes).
        var existing = parent.GetComponent<BoxCollider>();
        if (existing == null) existing = parent.gameObject.AddComponent<BoxCollider>();
        var margin = 0.1f * s;
        var totalH = (profile.HasLegs ? hipY + torsoH + headSize : hipY + torsoH * 0.5f + headSize) + margin;
        existing.center = new Vector3(0f, totalH * 0.5f, 0f);
        existing.size = new Vector3(0.7f * s * profile.ShoulderWidthScale, totalH, 0.5f * s);

        return rig;
    }

    private static void BuildHeadgear(Transform head, float headSize, HumanCharacterProfile profile,
        Material mat, HumanCharacterRig rig)
    {
        switch (profile.Headgear)
        {
            case HeadgearKind.HardHat:
                CubePart(head, new Vector3(0f, headSize * 0.55f, 0f),
                    new Vector3(headSize * 1.05f, headSize * 0.22f, headSize * 1.05f), profile.AccentColor, mat, rig);
                break;
            case HeadgearKind.Helmet:
                CubePart(head, new Vector3(0f, headSize * 0.5f, 0f),
                    new Vector3(headSize * 1.1f, headSize * 0.5f, headSize * 1.1f), profile.AccentColor, mat, rig);
                break;
        }
    }

    private static void BuildArm(Transform torso, bool isLeft, float s, float limbT, float armLen,
        float asymmetryY, HumanCharacterProfile profile, Material mat, HumanCharacterRig rig)
    {
        var sign = isLeft ? -1f : 1f;
        var shoulderX = 0.28f * s * profile.ShoulderWidthScale * sign;
        var shoulder = Pivot(torso, new Vector3(shoulderX, 0.24f * s + asymmetryY, 0f));

        var upperLen = 0.38f * s * armLen;
        CubePart(shoulder, new Vector3(0f, -upperLen * 0.5f, 0f),
            new Vector3(0.16f * s * limbT, upperLen, 0.16f * s * limbT), profile.BodyColor, mat, rig);

        var elbow = Pivot(shoulder, new Vector3(0f, -upperLen, 0f));
        var lowerLen = 0.34f * s * armLen;
        CubePart(elbow, new Vector3(0f, -lowerLen * 0.5f, 0f),
            new Vector3(0.14f * s * limbT, lowerLen, 0.14f * s * limbT), profile.BodyColor, mat, rig);

        if (profile.HasHands)
        {
            var handSize = 0.17f * s * (profile.OversizedHands ? 1.4f : 1f);
            CubePart(elbow, new Vector3(0f, -lowerLen - handSize * 0.4f, 0f),
                new Vector3(handSize, handSize, handSize), profile.AccentColor, mat, rig);
        }

        if (isLeft) { rig.LeftShoulder = shoulder; rig.LeftElbow = elbow; }
        else { rig.RightShoulder = shoulder; rig.RightElbow = elbow; }
    }

    private static void BuildLeg(Transform parent, bool isLeft, float s, float limbT, float hipY,
        HumanCharacterProfile profile, Material mat, HumanCharacterRig rig)
    {
        var sign = isLeft ? -1f : 1f;
        var hipX = 0.18f * s * sign;
        var hip = Pivot(parent, new Vector3(hipX, hipY, 0f));

        var upperLen = 0.42f * s;
        CubePart(hip, new Vector3(0f, -upperLen * 0.5f, 0f),
            new Vector3(0.18f * s * limbT, upperLen, 0.18f * s * limbT), profile.BodyColor, mat, rig);

        var knee = Pivot(hip, new Vector3(0f, -upperLen, 0f));
        var lowerLen = 0.4f * s;
        CubePart(knee, new Vector3(0f, -lowerLen * 0.5f, 0f),
            new Vector3(0.16f * s * limbT, lowerLen, 0.16f * s * limbT), profile.BodyColor, mat, rig);

        // foot: offset slightly +Z (forward) so it reads as a foot
        // silhouette from above rather than the leg just terminating --
        // this project's own "clear silhouette from high above" ask
        // (an RTS camera looks mostly straight down).
        CubePart(knee, new Vector3(0f, -lowerLen - 0.05f * s, 0.06f * s),
            new Vector3(0.2f * s * limbT, 0.1f * s, 0.3f * s * limbT), profile.AccentColor, mat, rig);

        if (isLeft) { rig.LeftHip = hip; rig.LeftKnee = knee; }
        else { rig.RightHip = hip; rig.RightKnee = knee; }
    }

    /// <summary>Selection highlight, shared by every rig-based unit
    /// (<see cref="Worker"/>, <see cref="HumanSoldier"/>) -- brightens
    /// each part toward white relative to ITS OWN base color via a
    /// per-renderer MaterialPropertyBlock, same "shape carries kind,
    /// color carries state" split every other selectable unit in this
    /// project follows (aesthetic-preferences skill §5). Never touches
    /// the shared Material itself.</summary>
    public static void SetSelected(HumanCharacterRig rig, bool selected)
    {
        if (rig == null) return;
        for (var i = 0; i < rig.Renderers.Count; i++)
        {
            var renderer = rig.Renderers[i];
            if (renderer == null) continue;
            var baseColor = rig.RendererBaseColors[i];
            var color = selected ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor;
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private static Transform Pivot(Transform parent, Vector3 localPos)
    {
        var go = new GameObject("Pivot");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private static Transform CubePart(Transform parent, Vector3 localCenter, Vector3 size,
        Color color, Material sharedMat, HumanCharacterRig rig)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);   // see Build()'s own comment: one collider on the root instead

        var renderer = go.GetComponent<Renderer>();
        if (sharedMat != null) renderer.sharedMaterial = sharedMat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        renderer.SetPropertyBlock(mpb);
        if (rig != null) { rig.Renderers.Add(renderer); rig.RendererBaseColors.Add(color); }
        return go.transform;
    }
}

/// <summary>Every Transform an animator needs to pose a rig built by
/// <see cref="HumanCharacterKit.Build"/> -- pivots are null exactly where
/// the profile omitted that part (e.g. every leg field on a legless
/// hover variant), callers must null-check before use.</summary>
public class HumanCharacterRig
{
    public bool HasLegs;
    public Transform Torso;
    public Transform Head;
    public Transform LeftShoulder, LeftElbow;
    public Transform RightShoulder, RightElbow;
    public Transform LeftHip, LeftKnee;
    public Transform RightHip, RightKnee;
    public Transform Backpack;
    public readonly List<Renderer> Renderers = new List<Renderer>();
    // Parallel to Renderers -- each part's own tinted color, so a caller
    // (e.g. a selection highlight) can brighten every part relative to
    // ITS OWN base color via a fresh MaterialPropertyBlock rather than
    // mutating the shared Material every character's Renderer points at
    // (which would recolor the whole city's worth of characters at
    // once -- see HumanCharacterKit's own header on why color lives in
    // per-renderer property blocks, never the Material itself).
    public readonly List<Color> RendererBaseColors = new List<Color>();

    // Rest pose the animator adds its own offsets on top of every tick --
    // Build() sets these once; every Tick* method in
    // HumanCharacterAnimator computes an ABSOLUTE pose from rest + the
    // current phase/state each call rather than accumulating deltas, so
    // switching between animation modes frame-to-frame (walk -> idle ->
    // build) never drifts or needs an explicit reset.
    public Vector3 TorsoRestLocalPos;
    public float TorsoRestPitchDeg;
}

public enum HeadgearKind { None, HardHat, Helmet }

/// <summary>Every visual/proportion knob <see cref="HumanCharacterKit.Build"/>
/// reads -- one struct, one builder, four (so far) distinct silhouettes
/// via the static presets below, matching the brief's own "modular...
/// interchangeable" framing instead of four separate character
/// classes.</summary>
public struct HumanCharacterProfile
{
    public Color BodyColor;
    public Color AccentColor;
    public float HeightScale;
    public float LimbThicknessScale;
    public float ArmLengthScale;
    public float ShoulderWidthScale;
    public float HunchDegrees;
    public bool Asymmetric;
    public bool HasLegs;
    public bool HasHands;
    public bool OversizedHands;
    public bool HasBackpack;
    public HeadgearKind Headgear;
    // 2026-08: "twitching idle... occasional head tilt... shoulder
    // twitches" (Mad Doctor Worker) vs. every other profile's plain
    // stand-still idle -- a behavior flag, not something derivable from
    // the geometry knobs above, so HumanCharacterAnimator.TickIdle reads
    // it directly rather than re-deriving "is this the hunched one" from
    // HunchDegrees (which is a real angle a future profile could also
    // want without wanting the twitch).
    public bool Twitchy;

    /// <summary>"Bright work clothing... slightly oversized hands...
    /// friendly proportions" -- the brief's own Human Worker section.</summary>
    public static HumanCharacterProfile HumanWorker()
    {
        return new HumanCharacterProfile
        {
            BodyColor = new Color(0.95f, 0.62f, 0.15f),   // hi-vis-ish safety orange, distinct from the old khaki
            AccentColor = new Color(0.85f, 0.62f, 0.15f),
            HeightScale = 1f,
            LimbThicknessScale = 1f,
            ArmLengthScale = 1f,
            ShoulderWidthScale = 1f,
            HunchDegrees = 0f,
            Asymmetric = false,
            HasLegs = true,
            HasHands = true,
            OversizedHands = true,
            HasBackpack = false,
            Headgear = HeadgearKind.HardHat,
        };
    }

    /// <summary>"Thin limbs, hunched posture, long arms, uneven
    /// proportions, lab coats, stitched clothing, slight asymmetry...
    /// immediately communicate 'experimental creations' rather than
    /// traditional zombies" -- the brief's own Mad Doctor Worker
    /// section. Pale, sickly lab-coat tone -- distinct hue family from
    /// the Human Worker's warm orange (aesthetic-preferences skill §5:
    /// shape AND color both carry identity here, not just shape, since
    /// this is the same underlying Worker class wearing a different
    /// faction's skin).</summary>
    public static HumanCharacterProfile MadDoctorWorker()
    {
        return new HumanCharacterProfile
        {
            BodyColor = new Color(0.78f, 0.8f, 0.72f),
            AccentColor = new Color(0.35f, 0.3f, 0.28f),
            HeightScale = 1.02f,
            LimbThicknessScale = 0.68f,
            ArmLengthScale = 1.3f,
            ShoulderWidthScale = 0.85f,
            HunchDegrees = 22f,
            Asymmetric = true,
            HasLegs = true,
            HasHands = true,
            OversizedHands = false,
            HasBackpack = false,
            Headgear = HeadgearKind.None,
            Twitchy = true,
        };
    }

    /// <summary>"Aliens should never walk... constant hovering... no
    /// visible footsteps... graceful, intelligent and effortless" -- the
    /// brief's own Alien Worker section. No legs at all (<see
    /// cref="HasLegs"/> false) rather than legs that just never animate
    /// -- "no visible footsteps" is a geometry fact here, not an
    /// animation-state promise that could drift out of sync with it.
    /// Bioluminescent-leaning cool color, no headgear/hands accessories
    /// (nothing humanoid-tailored reads right on something that isn't
    /// built like a human).</summary>
    public static HumanCharacterProfile AlienWorker()
    {
        return new HumanCharacterProfile
        {
            BodyColor = new Color(0.42f, 0.85f, 0.72f),
            AccentColor = new Color(0.65f, 0.95f, 0.85f),
            HeightScale = 0.92f,
            LimbThicknessScale = 0.75f,
            ArmLengthScale = 1.1f,
            ShoulderWidthScale = 0.8f,
            HunchDegrees = 0f,
            Asymmetric = false,
            HasLegs = false,
            HasHands = false,
            OversizedHands = false,
            HasBackpack = false,
            Headgear = HeadgearKind.None,
        };
    }

    /// <summary>"Helmet, backpack, simple rifle silhouette, boots,
    /// broader shoulders... disciplined... confident... military
    /// posture" -- the brief's own Human Soldier section. Rifle itself
    /// is built by the caller (<see cref="HumanSoldier"/>), not this
    /// generic kit -- a weapon prop isn't a body part every profile
    /// needs.</summary>
    public static HumanCharacterProfile HumanSoldier()
    {
        return new HumanCharacterProfile
        {
            BodyColor = new Color(0.35f, 0.4f, 0.3f),
            AccentColor = new Color(0.22f, 0.25f, 0.2f),
            HeightScale = 1.05f,
            LimbThicknessScale = 1.1f,
            ArmLengthScale = 1f,
            ShoulderWidthScale = 1.2f,
            HunchDegrees = 0f,
            Asymmetric = false,
            HasLegs = true,
            HasHands = true,
            OversizedHands = false,
            HasBackpack = true,
            Headgear = HeadgearKind.Helmet,
        };
    }
}
