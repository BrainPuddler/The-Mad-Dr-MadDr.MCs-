using MadDr.MatchCore;
using UnityEngine;

/// <summary>
/// 2026-08 creator direction: "Update the ui to reflect each theme,
/// faction style established by the maps and period style." <see
/// cref="BuildingDef"/>'s own doc comment already names the intended
/// design directly: "Per-faction display SKINS (Blood Bank / Plasma
/// Reserve / Ichor Cistern, ...) are a Unity/display concern... the sim
/// only ever reasons about the generic kind, since stats are shared
/// across factions and only names are themed." That skin never actually
/// got built on the Unity side before now -- every building showed the
/// SAME generic <see cref="BuildingDef.Name"/> and the SAME flat color
/// regardless of which faction the local player picked, despite the sim
/// layer being explicit that this was always the intended split. This
/// class is that missing skin layer: a display NAME per (kind, faction)
/// -- reusing the exact "Blood Bank / Plasma Reserve / Ichor Cistern"
/// wording <see cref="BuildingDef"/>'s own comment already canonized for
/// <see cref="BuildingKind.BloodStorage"/> -- plus an ACCENT COLOR that
/// reuses <see cref="BaseDresser.OwnerBaseColor"/> directly rather than
/// inventing a second palette (the same faction should read as the same
/// color everywhere).
///
/// Every name is period-flavored (1950s) AND faction-flavored: The Mad
/// Doctor reads gothic-medical (Sanatorium/Ward/Bank), the Human Army
/// reads military-industrial (Depot/Motor Pool/Ordnance), the Alien
/// Hive reads organic-biotech (Cistern/Nest/Midden) -- matching the
/// per-faction visual language `maddr-aesthetic-preferences` already
/// establishes for geometry/materials, extended here to the ONE other
/// place a building's identity shows up: its name in the HUD.
/// <see cref="FactionId.Mixed"/> and any unrecognized faction fall back
/// to the sim's own generic <see cref="BuildingDef.Name"/> -- Mixed has
/// no single origin/energy of its own (<see cref="FactionDef"/>'s own
/// doc comment), so it has no single theme to skin toward either.
/// </summary>
public static class BuildingFactionSkin
{
    /// <summary>Faction-themed display name for one building kind, or the
    /// sim's own generic <see cref="BuildingDef.Name"/> for <see
    /// cref="BuildingKind.Hq"/> is intentionally NOT skinned here even
    /// though <see cref="FactionDef.BaseName"/> already has a themed HQ
    /// name per faction -- Hq display already routes through
    /// FactionDef.BaseName elsewhere (docs/23 §1/§2); duplicating it here
    /// would be a second, possibly-drifting copy of the same three
    /// strings.</summary>
    public static string NameFor(BuildingKind kind, FactionId faction)
    {
        switch (kind)
        {
            case BuildingKind.Hq:
                return FactionDef.Get(faction).BaseName;

            case BuildingKind.BloodStorage:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Blood Bank";
                    case FactionId.HumanArmy: return "Plasma Reserve";
                    case FactionId.AlienHive: return "Ichor Cistern";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.FuelPump:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Transfusion Ward";
                    case FactionId.HumanArmy: return "Fuel Depot";
                    case FactionId.AlienHive: return "Secretion Duct";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.FuelStorage:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Serum Vault";
                    case FactionId.HumanArmy: return "Motor Pool";
                    case FactionId.AlienHive: return "Resin Hollow";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.PartsStorage:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Salvage Ward";
                    case FactionId.HumanArmy: return "Ordnance Depot";
                    case FactionId.AlienHive: return "Chitin Midden";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.HarvestPost:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Collection Cart";
                    case FactionId.HumanArmy: return "Requisition Post";
                    case FactionId.AlienHive: return "Feeding Nest";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.Factory:
                switch (faction)
                {
                    // "Stitchworks" is docs/22 §7's own established term
                    // for the Doctor's production building ("a forward
                    // Stitchworks... is a massive tempo investment" --
                    // quoted directly in BuildingDef.cs's own comment) --
                    // reused verbatim rather than inventing a new name.
                    case FactionId.MadDoctor: return "Stitchworks";
                    case FactionId.HumanArmy: return "Assembly Plant";
                    case FactionId.AlienHive: return "Spawning Vat";
                    default: return BuildingDef.Get(kind).Name;
                }

            case BuildingKind.Defense:
                switch (faction)
                {
                    case FactionId.MadDoctor: return "Ironclad Ward";
                    case FactionId.HumanArmy: return "Pillbox";
                    case FactionId.AlienHive: return "Carapace Nest";
                    default: return BuildingDef.Get(kind).Name;
                }

            default: // BigBrain
                switch (faction)
                {
                    // "Big Brain" is the creator's own literal, canon term
                    // (BuildingDef.cs's own comment: "must make a big
                    // brain control unit... creator's own words") -- kept
                    // unchanged for the Doctor, the faction it's actually
                    // lore-specific to. Army/Hive equivalents are this
                    // pass's own invented flavor for the SAME generic
                    // BuildingKind, not a claim about faction-gating
                    // (nothing in this file changes which factions CAN
                    // build which kind -- that's unchanged, unrelated sim
                    // logic).
                    case FactionId.MadDoctor: return "Big Brain";
                    case FactionId.HumanArmy: return "C2 Bunker";
                    case FactionId.AlienHive: return "Hive Mind Node";
                    default: return BuildingDef.Get(kind).Name;
                }
        }
    }

    /// <summary>Thin passthrough to <see cref="BaseDresser.
    /// OwnerBaseColor"/> -- see that method's own doc comment for the
    /// actual palette (Mad Doctor sage-green / Human Army steel-blue /
    /// Alien Hive violet-purple) and why it's the single source of truth
    /// for "what color is this faction" everywhere in this project, HUD
    /// included.</summary>
    public static Color AccentColorFor(FactionId faction) => BaseDresser.OwnerBaseColor(faction);
}
