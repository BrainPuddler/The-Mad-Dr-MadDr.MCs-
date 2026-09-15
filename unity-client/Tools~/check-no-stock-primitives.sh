#!/usr/bin/env bash
# docs/39 §11 item 3's lint rule: "no PrimitiveType.Sphere/Capsule
# outside VFX and the Big Brain jar" (docs/39 §0 rule 5 -- a stock
# Sphere is 760 triangles, a Capsule 832; PropLibrary's low-poly
# stand-ins (~80/~30 tris) exist for exactly this reason).
#
# Checks the thing that actually matters -- a DIRECT
# GameObject.CreatePrimitive(PrimitiveType.Sphere/Capsule) call, i.e. a
# raw stock mesh actually getting spawned -- not every mention of the
# enum value. Passing PrimitiveType.Sphere as an ARGUMENT to
# RuntimeCityBuilder.SpawnPrim/PropLibrary.Spawn/MonsterBody.Part/
# Tank.Prim is fine and expected: those are the shared choke points that
# already redirect Sphere/Cylinder to PropLibrary's low-poly meshes
# (docs/12's matching entry) -- they never reach CreatePrimitive with
# those types themselves. A NEW direct CreatePrimitive(Sphere/Capsule)
# call anywhere outside the allowlist below is what this script exists
# to catch.
#
# Run from anywhere; paths are relative to this script's own location.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
SCRIPTS_DIR="unity-client/Assets/Scripts"

# file:line-substring pairs that are allowed to call CreatePrimitive
# directly with Sphere/Capsule -- keep this list SHORT and SPECIFIC
# (whole files, not blanket exemptions by convention) so a real new
# violation elsewhere can't hide behind a loose pattern.
ALLOWLIST_FILES=(
  # Big Brain jar (docs/39's own explicit exception)
  "BrainJarBubbles.cs"
  # VFX (docs/39's own explicit exception) -- SpecialAttackVfx.cs's
  # MakePrimitiveChild, DamageFx.cs's fire/smoke/debris, WeaponFx.cs's
  # projectile/impact spheres
  "SpecialAttackVfx.cs"
  "DamageFx.cs"
  "WeaponFx.cs"
)
# 2026-09-16: the citizen-Capsule holdout this script used to carve out
# an exception for (docs/34 §0 / docs/36 §12 -- "Citizen.cs is the last
# capsule holdout") is now fixed: Citizen builds a real HumanCharacterKit
# rig instead. No exception needed for RuntimeCityBuilder.cs anymore --
# removed rather than left as a zero-count no-op, so a REAL new
# regression there is caught like anywhere else.

violations=0

while IFS=: read -r file line rest; do
  base="$(basename "$file")"
  allowed=false
  for a in "${ALLOWLIST_FILES[@]}"; do
    if [[ "$base" == "$a" ]]; then allowed=true; break; fi
  done
  if [[ "$allowed" == true ]]; then continue; fi
  echo "VIOLATION: $file:$line: $rest"
  violations=$((violations + 1))
done < <(grep -rn "CreatePrimitive(PrimitiveType.Sphere)\|CreatePrimitive(PrimitiveType.Capsule)" "$SCRIPTS_DIR"/*.cs)

if [[ "$violations" -gt 0 ]]; then
  echo ""
  echo "$violations stock Sphere/Capsule spawn(s) found outside the VFX/Big Brain jar exception (docs/39 §0 rule 5, §11 item 3)."
  exit 1
fi

echo "OK: no stock Sphere/Capsule spawns outside the VFX/Big Brain jar exception."
