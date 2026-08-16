using MadDr.RosterClient;
using Xunit;

namespace MadDr.RosterClient.Tests;

public class WeaponTests
{
    private static double[] Mid() => new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };

    private static GenomeDto Genome(string plan = "tetrapod", string hand = "laser_array",
        double[]? bodyParams = null, string heartTier = "steady", double[]? handParams = null)
    {
        return new GenomeDto(2, "w", new string[0],
            new BodyGenesDto(plan, bodyParams ?? new[] { 0.5, 0.5, 0.5, 0.5 }),
            new BrainGenesDto("average", new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto(heartTier, new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new SlotsDto(
                new PartAlleleDto(hand, handParams ?? Mid(), null),
                new PartAlleleDto("antenna", Mid(), null),
                new PartAlleleDto("bug_eyes", Mid(), null),
                new PartAlleleDto("hoofed_leg", Mid(), null)));
    }

    [Theory]
    [InlineData("laser_array", WeaponKind.Beam)]
    [InlineData("photon_blaster", WeaponKind.Bolt)]
    [InlineData("plasma_lance", WeaponKind.Bolt)]
    [InlineData("electric_arc", WeaponKind.Arc)]
    [InlineData("rifle_arm", WeaponKind.Bullet)]
    [InlineData("spore_launcher", WeaponKind.Spore)]
    [InlineData("chain_blade", WeaponKind.Melee)]
    [InlineData("claw_hand", WeaponKind.Melee)]
    [InlineData("pincer", WeaponKind.Melee)]
    [InlineData("tentacle", WeaponKind.Melee)]
    [InlineData("hand_stump", WeaponKind.None)]
    public void HandFamilyMapsToWeaponKind(string family, WeaponKind kind)
    {
        var w = Combat.WeaponFor(family, Mid());
        Assert.Equal(kind, w.Kind);
    }

    [Fact]
    public void ArmedWeaponsHavePositiveRangeDamageCadence()
    {
        foreach (var fam in new[] { "laser_array", "photon_blaster", "plasma_lance", "electric_arc", "rifle_arm",
            "spore_launcher", "chain_blade", "claw_hand", "pincer", "tentacle" })
        {
            var w = Combat.WeaponFor(fam, Mid());
            Assert.True(w.CanAttack);
            Assert.True(w.Range > 0, fam);
            Assert.True(w.Damage > 0, fam);
            Assert.True(w.Cadence > 0, fam);
        }
    }

    [Fact]
    public void UnarmedStumpCannotAttack()
    {
        var w = Combat.WeaponFor("hand_stump", Mid());
        Assert.False(w.CanAttack);
        Assert.Equal(0, w.Damage);
        Assert.Equal(0, w.Range);
    }

    [Fact]
    public void BeamAndMeleeAreInstantProjectilesAreNot()
    {
        Assert.Equal(0, Combat.WeaponFor("laser_array", Mid()).ProjectileSpeed);   // hitscan
        Assert.Equal(0, Combat.WeaponFor("claw_hand", Mid()).ProjectileSpeed);     // instant reach
        Assert.True(Combat.WeaponFor("photon_blaster", Mid()).ProjectileSpeed > 0);
        Assert.True(Combat.WeaponFor("rifle_arm", Mid()).ProjectileSpeed > 0);
    }

    [Fact]
    public void LaserFiresFasterButLighterThanPhoton()
    {
        var laser = Combat.WeaponFor("laser_array", Mid());
        var photon = Combat.WeaponFor("photon_blaster", Mid());
        Assert.True(laser.Cadence < photon.Cadence);   // laser is rapid
        Assert.True(laser.Damage < photon.Damage);     // photon is heavy
        Assert.True(photon.Range > laser.Range);       // photon reaches further
    }

    [Fact]
    public void WeaponGenesScaleOutput()
    {
        var lo = Mid(); lo[4] = 0.0;   // count
        var hi = Mid(); hi[4] = 1.0;
        Assert.True(Combat.WeaponFor("laser_array", hi).Damage
            > Combat.WeaponFor("laser_array", lo).Damage);

        var thin = Mid(); thin[1] = 0.0;   // girth
        var fat = Mid(); fat[1] = 1.0;
        Assert.True(Combat.WeaponFor("photon_blaster", fat).Damage
            > Combat.WeaponFor("photon_blaster", thin).Damage);
    }

    /// <summary>2026-08 (creator direction: "a direct Electric arc
    /// attack on opponents and buildings"): Arc is an instant hitscan
    /// kind, same delivery shape as Beam/Melee (ProjectileSpeed == 0 --
    /// see WeaponFx.Fire's own switch, which applies damage the same
    /// frame for all three), not a travelling Projectile like
    /// Bolt/Bullet/Spore. Building-targeting itself needs no dedicated
    /// test here -- MonsterAgent.TickSpecialAttack's AttackBuilding path
    /// already reads whatever WeaponProfile.Damage this returns
    /// generically, with no per-WeaponKind branch (confirmed by
    /// inspection).</summary>
    [Fact]
    public void ElectricArcIsInstantAndScalesWithGirth()
    {
        var arc = Combat.WeaponFor("electric_arc", Mid());
        Assert.Equal(WeaponKind.Arc, arc.Kind);
        Assert.Equal(0, arc.ProjectileSpeed);
        Assert.True(arc.CanAttack);

        var thin = Mid(); thin[1] = 0.0;   // girth
        var fat = Mid(); fat[1] = 1.0;
        Assert.True(Combat.WeaponFor("electric_arc", fat).Damage
            > Combat.WeaponFor("electric_arc", thin).Damage);
    }

    [Fact]
    public void TankWeaponsAreDistinctArchetypes()
    {
        var cannon = WeaponProfile.TankCannon();
        var flame = WeaponProfile.TankFlamethrower();
        Assert.Equal(WeaponKind.Bullet, cannon.Kind);
        Assert.Equal(WeaponKind.Flame, flame.Kind);
        Assert.True(flame.SpreadDeg > 0);           // flame is a cone
        Assert.Equal(0, cannon.SpreadDeg);
        Assert.True(cannon.Range > flame.Range);    // flame is short-range
        Assert.True(flame.Cadence < cannon.Cadence); // flame is continuous
    }

    [Fact]
    public void HumanoidCombatantWeaponsAreDistinctArchetypes()
    {
        var shotgun = WeaponProfile.Shotgun();
        var revolver = WeaponProfile.Revolver();
        var rifle = WeaponProfile.ServiceRifle();

        Assert.Equal(WeaponKind.Bullet, shotgun.Kind);
        Assert.Equal(WeaponKind.Bullet, revolver.Kind);
        Assert.Equal(WeaponKind.Bullet, rifle.Kind);

        // shotgun: short range, hardest single hit, slowest cadence --
        // "a powerful shotgun blast," not sustained fire
        Assert.True(shotgun.Range < revolver.Range);
        Assert.True(shotgun.Range < rifle.Range);
        Assert.True(shotgun.Damage > revolver.Damage);
        Assert.True(shotgun.Damage > rifle.Damage);
        Assert.True(shotgun.Cadence > revolver.Cadence);
        Assert.True(shotgun.Cadence > rifle.Cadence);

        // rifle: longest range, fastest cadence -- a trained combatant's
        // aimed, sustained fire beats a civilian's sidearm at range
        Assert.True(rifle.Range > revolver.Range);
        Assert.True(rifle.Cadence < revolver.Cadence);

        // heavier per-shot than the weakest concrete weapon in the
        // codebase (ZombieClaws) -- these are real firearms, not cannon
        // fodder claws
        var claws = WeaponProfile.ZombieClaws();
        Assert.True(shotgun.Damage > claws.Damage);
        Assert.True(revolver.Damage > claws.Damage);
        Assert.True(rifle.Damage > claws.Damage);
    }

    [Fact]
    public void PoliceSwatHunterMilitiaWeaponsAreDistinctArchetypes()
    {
        // 2026-08 (Police/SWAT/Hunter/Militia variant pass): the two NEW
        // weapons this pass adds -- TacticalCarbine (SWAT) and
        // HuntingRifle (Hunter) -- against the whole existing Human
        // firearm family, same "not a recolor" discipline
        // HumanoidCombatantWeaponsAreDistinctArchetypes already checks.
        var carbine = WeaponProfile.TacticalCarbine();
        var hunting = WeaponProfile.HuntingRifle();
        var rifle = WeaponProfile.ServiceRifle();
        var shotgun = WeaponProfile.Shotgun();

        Assert.Equal(WeaponKind.Bullet, carbine.Kind);
        Assert.Equal(WeaponKind.Bullet, hunting.Kind);

        // carbine: fastest cadence of ANY firearm in the codebase --
        // sustained suppressive fire is the whole point.
        Assert.True(carbine.Cadence <= rifle.Cadence);
        Assert.True(carbine.Cadence <= shotgun.Cadence);
        Assert.True(carbine.Range < rifle.Range);   // CQB-focused, not a long-range weapon

        // hunting rifle: longest range of any firearm in the codebase,
        // slowest cadence of any Bullet-kind weapon -- one well-aimed
        // shot, not sustained fire.
        Assert.True(hunting.Range > rifle.Range);
        Assert.True(hunting.Range > shotgun.Range);
        Assert.True(hunting.Cadence > rifle.Cadence);
        Assert.True(hunting.Damage > rifle.Damage);   // reach AND punch over the standard rifle
    }

    [Fact]
    public void MobWeaponsAreWeakAndReuseExistingKinds()
    {
        // 2026-08 (Angry Civilian Mob): "weak citizen... low damage" --
        // verify the actual numbers back that up, not just the flavor
        // text. Both reuse an EXISTING WeaponKind (Melee/Spore) rather
        // than inventing new rendering paths -- see each factory's own
        // doc comment for why.
        var rock = WeaponProfile.ThrownRock();
        var molotov = WeaponProfile.MolotovCocktail();
        var claws = WeaponProfile.ZombieClaws();

        Assert.Equal(WeaponKind.Melee, rock.Kind);
        Assert.Equal(WeaponKind.Spore, molotov.Kind);

        // "even weaker than ZombieClaws" -- the previous weakest concrete
        // weapon in the codebase (a thrown rock's actual reach can
        // reasonably beat a claw swing's -- throwing something IS the
        // point of reach -- so only damage is the claimed comparison
        // here, not range).
        Assert.True(rock.Damage < claws.Damage);

        // molotov is the more dangerous of the two mob weapons, but still
        // well below any trained-combatant firearm's damage.
        Assert.True(molotov.Damage > rock.Damage);
        var rifle = WeaponProfile.ServiceRifle();
        Assert.True(molotov.Damage < rifle.Damage);
    }

    [Fact]
    public void HealthScalesWithBulkAndHeart()
    {
        var lean = Combat.Profile(Genome(bodyParams: new[] { 0.5, 0.1, 0.5, 0.5 }, heartTier: "faint")).MaxHealth;
        var brute = Combat.Profile(Genome(bodyParams: new[] { 0.5, 0.95, 0.5, 0.5 }, heartTier: "titan")).MaxHealth;
        Assert.True(brute > lean * 2, $"titan brute {brute} vs faint lean {lean}");
        Assert.True(lean > 0);
    }

    [Fact]
    public void ProfilePicksTheHandWeapon()
    {
        Assert.Equal(WeaponKind.Beam, Combat.Profile(Genome(hand: "laser_array")).Weapon.Kind);
        Assert.Equal(WeaponKind.None, Combat.Profile(Genome(hand: "hand_stump")).Weapon.Kind);
    }
}
