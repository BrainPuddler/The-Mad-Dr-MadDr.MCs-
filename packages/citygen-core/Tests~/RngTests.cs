using MadDr.CityGen;
using Xunit;

namespace MadDr.CityGen.Tests;

/// <summary>
/// Golden-value tests against the REAL genome-core rng.ts, not a
/// re-derivation of the algorithm. Captured 2026-07 by running the
/// compiled reference implementation directly:
///
///   node -e "const {Rng}=require('./dist/src/rng.js');
///             const r=new Rng(SEED); for(...) console.log(r.next());"
///
/// from packages/genome-core/. If this ever needs re-capturing (e.g.
/// after an intentional rng.ts change), regenerate the same way -- do
/// not hand-edit the expected values.
/// </summary>
public class RngTests
{
    [Fact]
    public void Seed_0_matches_the_TypeScript_reference()
    {
        var r = new Rng(0u);
        var expected = new[]
        {
            0.1276340070180595, 0.2554530606139451, 0.5167204516474158,
            0.3874804873485118, 0.810084972763434,
        };
        foreach (var e in expected) Assert.Equal(e, r.Next(), precision: 12);
    }

    [Fact]
    public void Seed_1_matches_the_TypeScript_reference()
    {
        var r = new Rng(1u);
        var expected = new[]
        {
            0.263228221796453, 0.6034020676743239, 0.7019734452478588,
            0.22371549904346466, 0.6365781899075955,
        };
        foreach (var e in expected) Assert.Equal(e, r.Next(), precision: 12);
    }

    [Fact]
    public void Seed_42_matches_the_TypeScript_reference()
    {
        var r = new Rng(42u);
        var expected = new[]
        {
            0.8907801888417453, 0.4310670436825603, 0.3220651443116367,
            0.2072944741230458, 0.6512069271411747,
        };
        foreach (var e in expected) Assert.Equal(e, r.Next(), precision: 12);
    }

    [Fact]
    public void Seed_123456789_matches_the_TypeScript_reference()
    {
        var r = new Rng(123456789u);
        var expected = new[]
        {
            0.8854307532310486, 0.018727635499089956, 0.906807153718546,
            0.9081173758022487, 0.35717548755928874,
        };
        foreach (var e in expected) Assert.Equal(e, r.Next(), precision: 12);
    }

    [Fact]
    public void IntRange_100_seed_42_matches_the_TypeScript_reference()
    {
        var r = new Rng(42u);
        var expected = new[] { 89, 43, 32, 20, 65 };
        foreach (var e in expected) Assert.Equal(e, r.IntRange(100));
    }

    [Fact]
    public void Bool_seed_42_matches_the_TypeScript_reference()
    {
        var r = new Rng(42u);
        var expected = new[] { false, true, true, true, false, false, true, true };
        foreach (var e in expected) Assert.Equal(e, r.Bool());
    }

    [Fact]
    public void SeedFromString_matches_the_TypeScript_FNV1a_reference()
    {
        Assert.Equal(842549123u, Rng.SeedFromString("road-seed"));
        Assert.Equal(2166136261u, Rng.SeedFromString(""));
    }

    [Fact]
    public void Same_seed_always_produces_the_same_sequence()
    {
        var a = new Rng(999u);
        var b = new Rng(999u);
        for (var i = 0; i < 50; i++) Assert.Equal(a.Next(), b.Next());
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        var a = new Rng(1u);
        var b = new Rng(2u);
        var same = true;
        for (var i = 0; i < 10; i++) same &= a.Next() == b.Next();
        Assert.False(same);
    }

    [Fact]
    public void Next_always_stays_in_0_1()
    {
        var r = new Rng(7u);
        for (var i = 0; i < 10000; i++)
        {
            var v = r.Next();
            Assert.True(v >= 0.0 && v < 1.0, $"out of range: {v}");
        }
    }

    [Fact]
    public void Int_constructor_overload_reinterprets_the_same_32_bits_as_uint()
    {
        var fromInt = new Rng(-1);
        var fromUint = new Rng(uint.MaxValue);
        Assert.Equal(fromUint.Next(), fromInt.Next());
    }
}
