# Grid Standard Size Properties Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add DEV Grid controls that create, update, toggle, and physically remove SISBolt-compatible `KEEP_DESIGN_SIZE` properties on selected Shapes.

**Architecture:** Extend `ICorelShape` with the smallest name/ID/value property API needed by the feature. `StandardSizeService` owns all selection and SISBolt rules; `CorelShapeAdapter` translates those calls to COM `Shape.Properties`. The WPF panel only invokes the service through the existing command-group/status pipeline.

**Tech Stack:** C#, .NET Standard 2.0 shared services, .NET Framework 4.8 WPF/Corel.Interop.VGCore, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-29-grid-standard-size-design.md`

## Global Constraints

- Use exactly `KEEP_DESIGN_SIZE`; ID 0 is Boolean `True`; ID 1 is Double `shape.Width`.
- Manipulate only properties whose name is exactly `KEEP_DESIGN_SIZE`; preserve all other properties, including the empty-name entry.
- Alternar removes the property physically only when every selected Shape has it; otherwise it sets/updates all.
- Atualizar never removes; it creates when absent and updates when present.
- Apply individually to selected Shapes; do not recurse into groups.
- Preserve geometry and all Shape metadata other than `Shape.Properties`.
- Use existing `BeginCommandGroup`/`EndCommandGroup`; leave the read-only inspector unchanged.
- No commit, push, release, or installer publication.

## Review Focus

- A property with an empty name must survive every operation — test in Task 1.
- A group selected as one Shape must not cause child mutation — test in Task 1.
- Mixed selection must update existing Shapes and add missing ones — test in Task 1.
- Repeated update after width change must write the new width without resizing — test in Task 1.
- COM deletion must remove only exact `KEEP_DESIGN_SIZE` entries and not adjacent properties — test in Task 2.

---

### Task 1: Standard-size contract and service

**Files:**
- Modify: `src/PXToolBox.Outputs/Abstractions/ICorelShape.cs`
- Modify: `tests/PXToolBox.Tests/Fakes/FakeShape.cs`
- Create: `src/PXToolBox.Outputs/Tools/StandardSizeService.cs`
- Create: `tests/PXToolBox.Tests/StandardSizeServiceTests.cs`

**Interfaces:**
- Consumes: `ICorelShape.Width`, `ICorelShape.Shapes`, and property methods `HasProperty`, `SetProperty`, `DeleteProperties`.
- Produces: `StandardSizeService.HasStandardSize(ICorelShape)`, `SetStandardSize(ICorelShape)`, `UpdateStandardSize(ICorelShape)`, `RemoveStandardSize(ICorelShape)`, `Toggle(ICorelSelection)`, and `UpdateSelection(ICorelSelection)`, with the query returning `bool` and mutations returning `OperationResult`.

- [ ] **Step 1: Write failing service tests** covering HasStandardSize, creation, width update, physical removal, unrelated-property preservation, mixed selection, all-marked selection, multiple objects, empty selection, and non-recursive groups.
- [ ] **Step 2: Run the focused tests and verify they fail because the service/API is absent.**
- [ ] **Step 3: Add the minimal property methods to `ICorelShape`, implement them in `FakeShape`, and implement `StandardSizeService` with exact SISBolt constants and selection rules.**
- [ ] **Step 4: Run focused tests and verify they pass.**
- [ ] **Step 5: Run the complete test project and verify no regressions.**

### Task 2: Corel COM property adapter

**Files:**
- Modify: `host/PXToolBox.CorelHost.Dev/Corel/CorelShapeAdapter.cs`
- Modify: `tests/PXToolBox.Tests/StandardSizeServiceTests.cs` only if adapter-compatible contract tests need a shared fake

**Interfaces:**
- Consumes: the property methods added to `ICorelShape`.
- Produces: COM-backed implementations using `Shape.Properties.Exists`, indexed get/set, and physical delete for exact name/ID pairs.

- [ ] **Step 1: Add adapter contract coverage or a focused compile-level test seam for read/set/delete behavior.**
- [ ] **Step 2: Run the focused test/build and verify the new adapter members fail to compile before implementation.**
- [ ] **Step 3: Implement Corel translation, preserving the inspector and using no `ObjectData`.**
- [ ] **Step 4: Build the host and run the complete automated test project.**

### Task 3: DEV Grid UI and command integration

**Files:**
- Modify: `host/PXToolBox.CorelHost.Dev/ControlUI.xaml`
- Modify: `host/PXToolBox.CorelHost.Dev/ControlUI.xaml.cs`

**Interfaces:**
- Consumes: `StandardSizeService.Toggle` and `StandardSizeService.UpdateSelection`, plus existing `RunOnSelection`.
- Produces: two Grid buttons with the requested labels/tooltips, status messages, and one Corel Undo group per click.

- [ ] **Step 1: Add UI/dispatcher tests where the current host test setup permits; otherwise verify handlers statically against the existing command pipeline.**
- [ ] **Step 2: Run the relevant test/build command and verify the new controls compile.**
- [ ] **Step 3: Add both buttons together in the Grid expander and route handlers through `RunOnSelection` with command names `Alternar Tamanho Padrão` and `Atualizar Tamanho Padrão`.**
- [ ] **Step 4: Build the host and rerun all automated tests.**

### Task 4: Validation record and final verification

**Files:**
- Modify: `docs/mvp-smoke-test.md` with the real Corel validation checklist/result only if appropriate

- [ ] **Step 1: Run the full automated test suite and host build.**
- [ ] **Step 2: Inspect the diff for scope violations, unchanged inspector behavior, and exact strings.**
- [ ] **Step 3: Perform the Corel real-host checks that are available: empty selection, ON, update after width change, OFF, mixed/all-selected, and copy to another document; report any unavailable checks explicitly.**

