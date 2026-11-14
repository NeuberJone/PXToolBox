# PX Ecosystem — Architecture and Product Evolution Proposals

> Planning document — 2026-11-13. **Proposed, not implemented.** Do not interpret unchecked items as regressions or already-delivered features. Existing features and their acceptance status remain tracked in [../ROADMAP.md](../ROADMAP.md), [px-ecosystem-engines-roadmap.md](px-ecosystem-engines-roadmap.md), and [reference-feature-roadmap.md](reference-feature-roadmap.md).

## 1. Guiding decisions

- Keep **PXToolBox, ListForge, Nexor and Records of Corel Ragnarok** usable and distributable independently.
- Reuse shared **engines and contracts**, not necessarily executables, UI, databases, or repositories. Extract a library only when there is a real second consumer.
- Keep **MoldMotor** separate from PXToolBox Moldes after initial integration; no automatic cross-product feature synchronization.
- Avoid direct cross-product database coupling and circular dependencies; use versioned contracts and adapters.
- Preserve offline-first operation, manual overrides, undo, and operator approval for consequential operations.
- Keep the DEV installer and identity independent of commercial editions. No UI terminology using “Job”.

## 2. Consolidate production workflow without removing individual tools

- [ ] Provide a **Production Art** guided workflow: Prepare → Personalize → Assemble → Validate → Optimize → Export/Send to Nexor.
- [ ] Retain individual macro buttons and expert workflows for each stage.
- [ ] Consolidate Grid, sizing, personalization and assembly into a coherent navigation/assistant, while keeping distinct domain responsibilities.
- [ ] Specify shared production data contracts (order reference, pieces, sizes, quantities, fabric, artwork identifiers, maps, versions and provenance).
- [ ] Make ListForge the authoritative interpreter of input lists; do not duplicate parsing in Assembly or Nexor.
- [ ] Evaluate a **PX Production Engine** as orchestration of existing services, not a second implementation of Nesting, Slice, ListForge or molds.

## 3. PX Validator — proposed

- [ ] Validate missing sizes, missing fonts, names/numbers outside permitted zones, empty PowerClips, duplicated or missing pieces, list/production quantity mismatch, unexpected overlaps, elements outside print bounds, required artwork and fabric/map compatibility.
- [ ] Classify findings as **blocking error**, **warning** or **information**; do not block for warnings by default.
- [ ] Support configurable validation profiles and explicit operator review.
- [ ] Provide a preview/report with object references, explanation and suggested correction.
- [ ] Add automated core tests and real CorelDRAW 28 acceptance cases.

## 4. Nesting — per-object persistent rotation and mixed layouts (HIGH PRIORITY)

### User interaction

- [ ] In **Nesting → Object Properties → Allowed Rotation**, allow one or many selected shapes to be assigned:
  - **Inherit from fabric** (no object override);
  - **Fixed**: 0°;
  - **Half-turn**: 0° and 180°;
  - **Quarter-turns**: 0°, 90°, 180°, 270°;
  - **Custom**: explicit allowed angle set (only if geometry engine supports it).
- [ ] Read/show mixed values for multi-selection; provide **Apply to selection** and **Restore fabric default**.
- [ ] Display a non-printing indicator of per-object overrides.
- [ ] Support optional named presets by piece type, applied explicitly to selected objects; **do not infer by object name**.

### Persistence and precedence

- [ ] Reuse the existing object metadata/property service used for **Standard Size** and **Freehand**, with **namespaced PX-owned keys**, without overwriting SISBolt or unrelated metadata.
- [ ] Keep object rotation metadata independent of Standard Size, Freehand and other object flags.
- [ ] Persist after save/reopen and test copy/paste across documents in real CorelDRAW 28; do not assume COM metadata survives copying.
- [ ] Use precedence: **object override → selected fabric default → global Nesting default**.
- [ ] Model **hard fabric/print constraints** separately from preferences. Effective angles = requested angles intersect hard allowed angles. If empty, show a validation error rather than silently changing rules.
- [ ] Clarify whether angle values are **absolute relative to original object orientation** or relative to the fabric grain; document the convention and test it.
- [ ] Never silently mirror objects. Rotation and reflection are different operations.

### Solver and acceptance

- [ ] Process mixed items in **one shared optimization run and one map**, including quantities and grouped pieces, while enforcing each item's effective allowed orientations.
- [ ] Preserve original artwork and object properties; apply placements with safe Undo and non-destructive preview.
- [ ] Respect grain direction, print direction, one-way graphics, spacing and fabric width.
- [ ] Test 0°-only, 0°/180°, quarter-turn, conflicting hard constraints, repeated pieces, groups, irregular contours, cancelled run, and no feasible layout.
- [ ] Benchmark utilization and runtime against separate-montage baseline; do not promise optimality without evidence.

**Illustrative scenario:** Dryfit default 0°/180°; fronts/backs inherit it, sleeves and collars override to quarter-turns, a side stripe overrides to fixed 0°. All are packed together, subject to fabric hard constraints.

## 5. Nesting — textile-focused improvements

- [ ] Integrate **fabric widths, registered scraps and their dimensions** from Nexor through a versioned adapter/contract (not direct database writes).
- [ ] Enforce per-piece grain/orientation and configurable rotation restrictions.
- [ ] Compare packing strategies with utilization, estimated waste and runtime.
- [ ] Keep real contours, safe spacing, group/metadata preservation, roll/sheet modes and preview as explicit regression/acceptance criteria.
- [ ] Add historical comparisons and scrap-prioritization only after the production feedback loop is reliable.
- [ ] Keep universal eCut parity (CNC, plotter, broad CAD features) **out of initial textile-production scope**.

## 6. Nexor integration and traceability

- [ ] Define versioned, local-first production exchange: planned pieces and quantities → generated map and fabric allocation → print execution/reprints → actual consumption.
- [ ] Include stable artwork/order identifiers, map revision, source version and idempotency keys to avoid duplicate imports.
- [ ] Keep Nexor responsible for production state transitions and measured consumption.
- [ ] Report planned vs actual, reprints, waste and discrepancies without mutating Nexor's database directly.

## 7. PX Ecosystem Manager — future distribution phase

- [ ] Provide a **modular installer** where users select independent products and optional PXToolBox components.
- [ ] Preserve standalone installers for PXToolBox, ListForge, Nexor and other products.
- [ ] Detect existing installations, prerequisites and compatible CorelDRAW versions **without hardcoding an incorrect host version**.
- [ ] Allow add/remove, independent update, repair, integrity verification and per-product uninstall without damaging others.
- [ ] Preserve user profiles/data during upgrades; support rollback or safe recovery on failed update.
- [ ] Allow DEV and commercial PXToolBox to coexist with separate installation identity and settings.
- [ ] Keep installed applications functional if Manager is removed.
- [ ] Centralize distribution/update infrastructure; **each product still enforces its own license**, including offline scenarios.
- [ ] Consider later: license overview, optional cross-PC profile sync, release notes, diagnostics, update channels. Avoid mandatory accounts, cloud or resident services in v1.

**Architecture note:** Manager is a distribution/orchestration layer, not a runtime dependency. Shared services should be extracted incrementally, not pre-created as empty projects.

## 8. Rationalization / defer

- [ ] Audit duplicate implementations across products and unify only proven overlaps.
- [ ] Keep Corel adapters outside pure algorithmic cores.
- [ ] Defer a universal SISBolt/eCut clone; prioritize documented textile workflows.
- [ ] Defer cloud sync, marketplace, broad CAD/CNC feature sets and speculative microservices.
- [ ] Do not conflate 'implemented', 'documented' and 'real-Corel validated' in status reporting.

## 9. Suggested order and acceptance gates

1. **P0:** CorelDRAW 28 compatibility; real-world Nesting/Slice smoke tests; protect existing user data and Undo.
2. **P1:** Persisted per-object rotation + mixed packing, with fabric hard constraints and tests.
3. **P1:** Versioned production contracts, PX Validator MVP and assembly workflow consolidation.
4. **P1:** Nexor fabric/scrap adapter and measured feedback loop.
5. **P2:** UX/profile improvements, product licensing and stable installers.
6. **P3:** PX Ecosystem Manager, optional sync and additional ecosystem services.

Every feature above remains **unchecked** until code, tests and (where applicable) real-Corel acceptance demonstrate completion.
