// Minimal stand-ins so `MonsterSteeringController.cs` -- the REAL file,
// compiled straight out of `Assets/Scripts/` by this harness's csproj, not
// a copy -- builds and runs outside Unity. Only the members that file
// actually touches exist here; this is deliberately not a UnityEngine
// emulator.
//
// Why a committed harness instead of a throwaway one: every previous pass
// at the circling bug was verified against an ad-hoc harness that was
// itself wrong (see MonsterSteeringController's own 2026-08 CORRECTION --
// it never published `LastVelocity` between frames, so the whole
// Alignment term was silently dead and the measurements were
// meaningless). A harness that lives in the repo can be re-run and
// re-reviewed instead of re-invented wrong each time.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 up { get { return new Vector3(0f, 1f, 0f); } }

        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }

        public Vector3 normalized
        {
            get
            {
                var m = magnitude;
                return m < 1e-9f ? zero : new Vector3(x / m, y / m, z / m);
            }
        }

        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float s) { return new Vector3(a.x * s, a.y * s, a.z * s); }
        public static Vector3 operator *(float s, Vector3 a) { return a * s; }
        public static Vector3 operator /(Vector3 a, float s) { return new Vector3(a.x / s, a.y / s, a.z / s); }

        public override string ToString() { return "(" + x.ToString("0.00") + ", " + z.ToString("0.00") + ")"; }
    }

    public static class Mathf
    {
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Abs(float a) { return a < 0f ? -a : a; }
        public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
    }
}

/// <summary>The slice of the real `UnitCombat` MonoBehaviour that
/// `MonsterSteeringController` reads. Field names/types match the real one
/// exactly, so the compiled steering code is bit-identical to what ships.</summary>
public sealed class UnitCombat
{
    private static int _nextId = 1000;
    private readonly int _id;

    public UnitCombat() { _id = _nextId++; }

    public string Faction = "monster";
    public float Radius = 1.5f;
    public UnityEngine.Vector3 LastVelocity;
    public bool Alive = true;
    public readonly FakeTransform transform = new FakeTransform();

    public int GetInstanceID() { return _id; }
}

public sealed class FakeTransform
{
    public UnityEngine.Vector3 position;
}
