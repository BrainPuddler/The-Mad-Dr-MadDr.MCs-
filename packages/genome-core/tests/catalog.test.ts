import { test } from "node:test";
import assert from "node:assert/strict";

import { BODY_PLANS, isAmphibious } from "../src/index.js";

test("crab and serpentine are amphibious; every other plan is ground-bound", () => {
  const amphibious = Object.keys(BODY_PLANS).filter((p) => isAmphibious(p)).sort();
  assert.deepEqual(amphibious, ["crab", "serpentine"]);
});

test("isAmphibious treats unknown plans as ground-bound, not an error", () => {
  assert.equal(isAmphibious("kraken_that_does_not_exist"), false);
});
