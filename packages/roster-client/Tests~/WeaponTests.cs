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
        foreach (var fam in new[] { "laser_array", "photon_blaster", "plasma_lance", "rifle_arm",
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
