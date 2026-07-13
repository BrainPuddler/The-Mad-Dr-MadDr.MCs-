using System.Collections.Generic;
using MadDr.CityGen;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// A placeholder body for one fetched creature -- a scaled, colored
/// capsule, not doc08's real genome-to-mesh pipeline (that doesn't exist
/// in code yet; this is the same kind of stand-in CityGizmo already is
/// for buildings). Wanders between passable hexes so the terrain and
/// destruction work built earlier this session becomes visible, not
/// just tested: an amphibious creature (crab/serpentine) will cross the
/// river other creatures walk around.
///
/// Speed is a fixed placeholder, not derived from the genome: docs/04/06's
/// Speed stat lives in a `statGenes` block that never actually shipped in
/// the real v2 schema (found and logged earlier this session porting
/// Cannibalize) -- there is no real speed gene to read yet.
/// </summary>
public class MonsterAvatar : MonoBehaviour
{
    private const string AmphibiousPlanCrab = "crab";
    private const string AmphibiousPlanSerpentine = "serpentine";
    private const float WanderSpeedMetersPerSecond = 3f;
    private const int WanderRadiusHexes = 10;

    private BattlefieldState _battlefield;
    private HexCoord _homeHex;
    private bool _isAmphibious;
    private Vector3 _origin;
    private Vector3 _target;
    private System.Random _rng;

    public void Init(StoredGenomeDto creature, CityModel city, BattlefieldState battlefield, HexCoord homeHex, Vector3 origin, int rngSeed)
    {
        _battlefield = battlefield;
        _homeHex = homeHex;
        _origin = origin;
        _rng = new System.Random(rngSeed);

        var plan = creature.Genome.Body.Plan;
        _isAmphibious = plan == AmphibiousPlanCrab || plan == AmphibiousPlanSerpentine;

        // Scale by body.bulk (BODY_AXES[1], docs/15) -- the one genome
        // value that maps onto "how big is this thing" without needing
        // the stat block that never shipped.
        var bulk = creature.Genome.Body.Params.Length > 1 ? creature.Genome.Body.Params[1] : 0.5;
        var scale = Mathf.Lerp(0.6f, 2.2f, (float)bulk);
        transform.localScale = new Vector3(scale, scale * 1.4f, scale);

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = ColorForPlan(plan, creature.Id);
            renderer.material = mat;
        }

        transform.position = origin + new Vector3(0f, scale * 0.7f, 0f);
        _target = transform.position;
        PickNewTarget();
    }

    private void Update()
    {
        if (_battlefield == null) return;

        transform.position = Vector3.MoveTowards(transform.position, _target, WanderSpeedMetersPerSecond * Time.deltaTime);
        if (Vector3.Distance(transform.position, _target) < 0.5f) PickNewTarget();
    }

    private void PickNewTarget()
    {
        // Built once per retarget, not once per candidate hex -- each
        // BlockedTo*() call walks every building/bridge in the city.
        var blocked = _isAmphibious ? _battlefield.BlockedToAmphibious() : _battlefield.BlockedToGround();

        var candidates = new List<HexCoord>();
        foreach (var hex in _homeHex.Range(WanderRadiusHexes))
            if (!blocked.Contains(hex)) candidates.Add(hex);
        if (candidates.Count == 0) return; // stay put rather than pick an illegal target

        var choice = candidates[_rng.Next(candidates.Count)];
        var (x, z) = choice.ToWorld();
        var (hx, hz) = _homeHex.ToWorld();
        var offset = new Vector3((float)(x - hx), 0f, (float)(z - hz));
        _target = _origin + offset + new Vector3(0f, transform.position.y - _origin.y, 0f);
    }

    private static Color ColorForPlan(string plan, string creatureId)
    {
        switch (plan)
        {
            case "crab": return new Color(0.75f, 0.25f, 0.2f);
            case "serpentine": return new Color(0.2f, 0.55f, 0.25f);
            case "winged": return new Color(0.55f, 0.35f, 0.75f);
            case "avian": return new Color(0.8f, 0.65f, 0.2f);
            case "arachnid": return new Color(0.3f, 0.2f, 0.35f);
            case "treant": return new Color(0.35f, 0.5f, 0.25f);
            case "floater": return new Color(0.3f, 0.7f, 0.8f);
            case "blob": return new Color(0.6f, 0.3f, 0.55f);
            default:
                // tetrapod and anything unrecognized: hash the creature's
                // own id so individuals still read as visually distinct.
                var hash = 0;
                foreach (var c in creatureId) hash = hash * 31 + c;
                var hue = ((hash % 360) + 360) % 360 / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }
    }
}
