import { test } from "node:test";
import assert from "node:assert/strict";

import { BODY_PLANS, FAMILIES, isAmphibious, isKnownFamily, homologOf, originOf, isVestigial } from "../src/index.js";

test("crab and serpentine are amphibious; every other plan is ground-bound", () => {
  const amphibious = Object.keys(BODY_PLANS).filter((p) => isAmphibious(p)).sort();
  assert.deepEqual(amphibious, ["crab", "serpentine"]);
});

test("isAmphibious treats unknown plans as ground-bound, not an error", () => {
  assert.equal(isAmphibious("kraken_that_does_not_exist"), false);
});

test("laser_array and photon_blaster are biotech hand weapons, not vestigial", () => {
  for (const family of ["laser_array", "photon_blaster"]) {
    assert.equal(isKnownFamily(family), true);
    assert.equal(homologOf(family), "hand");
    assert.equal(originOf(family), "biotech");
    assert.equal(isVestigial(family), false);
  }
});

test("the three alien hand weapons (plasma_lance, laser_array, photon_blaster) have distinct canalized bounds", () => {
  const lance = FAMILIES.plasma_lance!.bounds;
  const array = FAMILIES.laser_array!.bounds;
  const blaster = FAMILIES.photon_blaster!.bounds;

  // Each narrows a different axis away from the full [0,1] range, so
  // breeding one never silently drifts into looking like another.
  assert.notDeepEqual(lance, array);
  assert.notDeepEqual(lance, blaster);
  assert.notDeepEqual(array, blaster);
});
