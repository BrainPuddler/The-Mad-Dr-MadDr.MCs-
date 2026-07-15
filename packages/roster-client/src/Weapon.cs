using System;

namespace MadDr.RosterClient
{
    /// <summary>How a weapon delivers its hit -- drives the battlefield
    /// FX (WeaponFx) and the targeting rules.</summary>
    public enum WeaponKind
    {
        None,    // healed stump: unarmed
        Melee,   // claws/blades: instant, very short reach
        Beam,    // laser_array: instant hitscan line
        Bolt,    // photon/plasma "phaser": a slow glowing projectile
        Bullet,  // rifle / tank cannon: a fast small projectile
        Spore,   // spore_launcher: a lobbed biotech round
        Flame,   // flamethrower: a short continuous cone
    }

    /// <summary>A weapon's battlefield stats -- engine-agnostic so the
    /// numbers are tested once and shared, the same discipline as
    /// LocomotionProfile. Colors are 0-255 RGB, matching the renderer
    /// palette, so a laser_array's cyan emitter fires a cyan beam.</summary>
    public sealed class WeaponProfile
    {
        public WeaponKind Kind { get; }
        public string Name { get; }
        public double Range { get; }            // meters (docs/18: 20 m hex)
        public double Damage { get; }           // hit points per shot/tick
        public double Cadence { get; }          // seconds between shots
        public double ProjectileSpeed { get; }  // m/s; 0 for instant (beam/melee/flame)
        public double SpreadDeg { get; }         // cone half-angle for Flame; 0 otherwise
        public int R { get; }
        public int G { get; }
        public int B { get; }

        public WeaponProfile(WeaponKind kind, string name, double range, double damage, double cadence,
            double projectileSpeed, double spreadDeg, int r, int g, int b)
        {
            Kind = kind;
            Name = name;
            Range = range;
            Damage = damage;
            Cadence = cadence;
            ProjectileSpeed = projectileSpeed;
            SpreadDeg = spreadDeg;
            R = r;
            G = g;
            B = b;
        }

        public bool CanAttack { get { return Kind != WeaponKind.None; } }

        // ---- tank weapons (not genome-driven -- the human faction's 1950s
        // hardware; the flamethrower exists purely because it is cool) ----

        public static WeaponProfile TankCannon()
        {
            return new WeaponProfile(WeaponKind.Bullet, "75mm cannon", 68, 34, 2.3, 78, 0, 255, 214, 120);
        }

        public static WeaponProfile TankFlamethrower()
        {
            return new WeaponProfile(WeaponKind.Flame, "flamethrower", 22, 11, 0.22, 0, 24, 255, 120, 30);
        }
    }

    /// <summary>A combatant's fight stats derived from its genome: the
    /// weapon its hand family projects, plus a health pool from mass and
    /// heart tier (a titan-hearted brute soaks far more than a faint
    /// pinhead). Tested in Tests~, same as Locomotion.</summary>
    public sealed class CombatProfile
    {
        public double MaxHealth { get; }
        public WeaponProfile Weapon { get; }

        public CombatProfile(double maxHealth, WeaponProfile weapon)
        {
            MaxHealth = maxHealth;
            Weapon = weapon;
        }
    }

    public static class Combat
    {
        public static CombatProfile Profile(GenomeDto g)
        {
            var bulk = At(g.Body.Params, 1, 0.5);
            var vigor = At(g.Heart.Params, 0, 0.5);
            var planMass = PlanMass(g.Body.Plan);

            // health: a floor everyone gets, plus mass (plan x bulk) and the
            // heart's reserve (vigor scales it), so breeding a bigger,
            // stronger-hearted creature makes a visibly tougher one
            var maxHealth = Round1(48 + planMass * 26 + bulk * 90 + HeartHp(g.Heart.Tier) * (0.6 + 0.8 * vigor));

            return new CombatProfile(maxHealth, WeaponFor(g.Slots.Hand.Family, g.Slots.Hand.Params));
        }

        /// <summary>Hand family -> weapon. Genes scale it: `count`
        /// (params[4]) adds emitters/rounds, `girth` (params[1]) adds
        /// mass per hit -- the same axes that shape the rendered part.</summary>
        public static WeaponProfile WeaponFor(string family, double[] handParams)
        {
            var girth = At(handParams, 1, 0.5);
            var count = At(handParams, 4, 0.5);

            switch (family)
            {
                case "laser_array":
                    // cyan hitscan: rapid, low per-hit, more emitters = more DPS
                    return new WeaponProfile(WeaponKind.Beam, "Laser array", 62, Round1(5 + count * 6), 0.35, 0, 0,
                        130, 220, 255);
                case "photon_blaster":
                    // warm phaser bolt: slow, heavy, long reach
                    return new WeaponProfile(WeaponKind.Bolt, "Photon blaster", 74, Round1(26 + girth * 22), 1.5, 54, 0,
                        255, 235, 175);
                case "plasma_lance":
                    return new WeaponProfile(WeaponKind.Bolt, "Plasma lance", 34, Round1(22 + girth * 16), 1.1, 40, 0,
                        150, 85, 230);
                case "rifle_arm":
                    return new WeaponProfile(WeaponKind.Bullet, "Rifle", 56, Round1(6 + girth * 5), 0.25, 92, 0,
                        250, 230, 160);
                case "spore_launcher":
                    return new WeaponProfile(WeaponKind.Spore, "Spore launcher", 46, Round1(13 + count * 8), 1.0, 32, 0,
                        150, 235, 190);
                case "chain_blade":
                    return new WeaponProfile(WeaponKind.Melee, "Chain blade", 7, Round1(18 + girth * 12), 0.5, 0, 0,
                        116, 130, 144);
                case "pincer":
                    return new WeaponProfile(WeaponKind.Melee, "Pincer", 6, Round1(13 + girth * 10), 0.7, 0, 0,
                        196, 184, 152);
                case "tentacle":
                    return new WeaponProfile(WeaponKind.Melee, "Tentacle", 9, Round1(9 + count * 6), 0.8, 0, 0,
                        150, 85, 230);
                case "claw_hand":
                    return new WeaponProfile(WeaponKind.Melee, "Claws", 6, Round1(11 + count * 7), 0.6, 0, 0,
                        196, 184, 152);
                case "hand_stump":
                default:
                    return new WeaponProfile(WeaponKind.None, "Unarmed", 0, 0, 1, 0, 0, 160, 150, 140);
            }
        }

        private static double At(double[] arr, int i, double fallback)
        {
            return arr != null && i < arr.Length ? arr[i] : fallback;
        }

        private static double HeartHp(string tier)
        {
            switch (tier)
            {
                case "faint": return 20;
                case "steady": return 40;
                case "strong": return 70;
                case "titan": return 120;
                default: return 40;
            }
        }

        private static double PlanMass(string plan)
        {
            switch (plan)
            {
                case "tetrapod": return 1.0;
                case "winged": return 0.8;
                case "serpentine": return 1.15;
                case "blob": return 1.55;
                case "crab": return 1.3;
                case "arachnid": return 1.1;
                case "avian": return 0.85;
                case "treant": return 1.7;
                case "floater": return 0.6;
                default: return 1.0;
            }
        }

        private static double Round1(double x) { return Math.Round(x, 1, MidpointRounding.AwayFromZero); }
    }
}
