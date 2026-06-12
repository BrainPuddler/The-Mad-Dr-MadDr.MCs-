# The Lab — browser test bench

A static test site for the bits built so far: **the Lab** (the mutator and
the chop shop in one room). Spawn specimens, mutate and splice them, cut
parts off, transplant hearts, and watch the data screen — viability,
typed energy upkeep (blood/fuel/ichor), brain behavior, lineage.

- Runs **entirely in the browser**: `lib/` is the compiled
  [`@maddr/genome-core`](../packages/genome-core/) (the exact library the
  server uses), loaded as native ES modules. No build step, no backend.
- Specimens persist in the browser's localStorage. This is a test
  harness — the real persistent, anti-cheat lab is
  [`packages/mutator-service`](../packages/mutator-service/).

## Run locally

Any static file server works:

```
cd site && python3 -m http.server 8080
# open http://localhost:8080
```

## Deploy to GitHub Pages

`.github/workflows/pages.yml` deploys this folder. One-time setup:
repo **Settings → Pages → Source: GitHub Actions**. After that, every push
that touches `site/` redeploys, or run the workflow manually from the
Actions tab.

## Refreshing `lib/` after genome-core changes

```
cd packages/genome-core && npm run build && cp dist/src/*.js ../../site/lib/
```
