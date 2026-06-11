# 13 — Design Review: Book of Lenses Evaluation

Status: Reviewed v1.0 · An assessment of this design suite (docs 01–12) through Jesse Schell's *The Art of Game Design: A Book of Lenses*, rating three axes: uniqueness, fun/play, and marketability.

## Summary

| Axis | Rating | Verdict |
| --- | --- | --- |
| Uniqueness | **8.5 / 10** | A genuinely rare combination, though not unprecedented |
| Fun / Play | **7 / 10 (provisional)** | Well-built fun *hypotheses*; one big unproven risk (touch input) |
| Marketability | **5.5 / 10** | The weakest axis — strong organic hook, hard genre, constrained monetization |

## Uniqueness — 8.5/10

- **Lens of Surprise / Lens of Curiosity**: excellent. The Mutator ([06](06-mutator-design.md)) is a surprise engine by construction — biased randomness means every operation poses a question ("what will feeding it this arm do?") and answers it unpredictably. Surprise is built into the core loop, not sprinkled on top.
- **Lens of Endogenous Value**: even stronger. A monster is valuable three ways at once — your creation (emotional), your army (strategic), your breeding stock (generative). That triple-loading is what made El-Fish memorable, attached to stakes El-Fish never had.
- **Lens of Unification**: the Notebook fiction frame ([01](01-vision.md)) ties the lab, map, collection, and even failed experiments into one metaphor — rare discipline at the design-doc stage.
- **The caveat that keeps this under 9**: prior art exists — **Impossible Creatures** (Relic, 2003), an RTS of spliced animal units, was a commercial disappointment. Its post-mortem lesson: creature-combining collapsed to a few dominant builds at the strategy layer. Our power-budget/brain-budget system ([06](06-mutator-design.md)) is the structural answer to that failure; recorded as studied prior art in [12-open-questions.md](12-open-questions.md). The cross-device commute lab ([07](07-mutator-server-architecture.md)) appears genuinely novel — no shipped game makes that exact promise.

## Fun / Play — 7/10, provisional by design

- **Lens of the Toy**: the Mutator passes Schell's hardest test — it would be fun with no goal at all. Breeding monsters and browsing pedigrees is play in itself; El-Fish proved the market. A game built on a fun toy starts ahead.
- **Lens of Skill vs. Chance**: textbook-good. Bounded luck (±15%, no misses) seasons outcomes without deciding them; the worked anchors in [04-combat-model.md](04-combat-model.md) (~70% / ~85% / near-even) show the balance was designed, not hoped for.
- **Lens of Meaningful Choices / Lens of Triangularity**: well served by the Lumen Cycle ([03](03-mana-system.md)) — "attack now at strength, or hold and survive the enemy's hour" is a real risk/reward choice on a visible public clock; Twilight emitters are deliberate high-risk/high-reward.
- **Lens of Time**: the 10–15 minute match bounded by three Lumen cycles is well judged for mobile sessions.
- **The unresolved risk — Lens of Accessibility / Lens of the Interface**: *facing-and-flank micro on a touchscreen under real-time pressure*. The combat skill axis (arcs, turning costs, repositioning, [04](04-combat-model.md)) assumes positional intent can be expressed quickly with a thumb. Mobile RTS history is a graveyard of designs that were fun with a mouse and miserable on a phone. The docs specify *readability* (facing wedges, aura rings) but input design is open. **Action taken**: touchscreen-input fun is now an explicit Phase-1 exit criterion in [11-roadmap.md](11-roadmap.md) — the sandbox must be played on a phone, not a dev PC.

The 7 is provisional in exactly the way the roadmap intends: Phase 1's purpose is to convert this number into evidence.

## Marketability — 5.5/10

- **Lens of the Player**: the audience is an *intersection* — players who like real-time competitive strategy AND creature breeding AND synchronous 1v1 on mobile. Each circle is large; the intersection is smaller, and intersection audiences are expensive to reach with paid acquisition.
- **Genre reality**: synchronous competitive mobile games that succeeded (Clash Royale) did so by radically simplifying the RTS. This design ships a fuller RTS than anything that has worked at scale on phones — not a reason to stop, but the reason this is 5.5 and not 7.
- **Two structural tensions**:
  1. *No-pay-to-win vs. f2p economics* ([01](01-vision.md), [05](05-component-economy.md)): cosmetic-only monetization demands a large player base or strong cosmetics — and the pigment system is curated and subtle by art-direction design, which is good for tone and bad for selling skins. The pillar is right; it prices the game into needing organic growth.
  2. *The name*: charming and unsearchable; naming work is folded into Q6 in [12-open-questions.md](12-open-questions.md).
- **The genuine asset — Lens of Surprise again**: this game produces *shareable artifacts*. "Look at the horror I bred" is a screenshot, a stream moment, a short-form clip. Games whose core loop emits show-off-able uniqueness (Spore's creature creator went viral before launch) earn organic reach money can't buy. The Mutator is not just the design's heart — it is the marketing department.

## Recommendations (status)

1. ✅ Touchscreen-input fun added to Phase-1 exit criteria ([11-roadmap.md](11-roadmap.md)).
2. ✅ Impossible Creatures recorded as studied prior art ([12-open-questions.md](12-open-questions.md) decision log).
3. Open: marketing plan should be built around shareable creature artifacts (Phase 4–5 planning input).
