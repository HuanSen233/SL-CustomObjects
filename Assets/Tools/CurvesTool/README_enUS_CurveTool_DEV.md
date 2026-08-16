# Curve Tool — Developer Documentation (README_enUS_CurveTool_DEV)

> For developers who maintain, extend, or want to understand the internal implementation.
> End-user documentation: [README_enUS_CurveTool.md](./README_enUS_CurveTool.md).

---

## 1. Project Overview

Curve Tool is a **Unity editor tool** that provides Blender-style Bezier curve editing in the Scene view and **spawns objects along curves** (depending on the project's `PrimitiveComponent` and the Primitives prefab system).

| Item | Details |
|---|---|
| Language | C# (Unity 2021.3+, C# 9 features incl. switch expressions / pattern matching) |
| Assembly | Default project assembly `Assembly-CSharp` (no asmdef; editor APIs are available in this project's current setup) |
| Data storage | **In-scene** — the `CurveManager` component lives on the hidden object `__CurvesToolData__` and is serialized with the scene |
| Settings storage | JSON file `CurveToolSettings.json` (inside the tool folder; path is derived dynamically) |
| Localization | Built-in EN / 简体中文 via the static `L10n` dictionary |
| External deps | `PrimitiveComponent` (`Assets/DONT TOUCH/Scripts/BlockComponents/PrimitiveComponent.cs`), Primitives prefabs (`Assets/Resources/Blocks/Primitives/*.prefab`) |

---

## 2. Directory & File Responsibilities

`Assets/Tools/CurvesTool/`:

| File | Responsibility |
|---|---|
| `CurveTool.cs` | EditorWindow skeleton: lifecycle (OnEnable/OnDisable), mode bar, business logic (create/copy/mirror curves), settings persistence (JSON) |
| `CurveTool.EditTab.cs` | Edit-tab UI: curve tools foldout (flip/mirror/cursor), curve list, vertex & handle properties, segment properties, generate section |
| `CurveTool.SettingsTab.cs` | Settings-tab UI |
| `CurveManager.cs` | **Data-layer core**: scene data carrier (MonoBehaviour), singleton binding, data operations (add/remove curves & vertices, selection), dirty marking, legacy-asset migration, JSON export |
| `BezierCurve.cs` | One curve: vertex/segment lists, sampling (2D/3D), handle recalculation (RecalculateHandles), coordinate mapping (MapToWorld/MapFromWorld) |
| `BezierCurveData.cs` | Data models: `UpAxis`/\`HandleType\` enums, `CurveVertex` (vertex + left/right handles + per-axis locks + ApplyHandleType constraints) |
| `SegmentInfo.cs` | Per-micro-segment parameters: spawn primitive type, scale/offset/rotation |
| `CurveSceneEditor.cs` | SceneView interaction entry: hook registration, event dispatch (MouseDown/Drag/Up/KeyDown) |
| `CurveSceneEditor.HitTest.cs` | Hit testing: vertices/handles/arrows (7-pass priority scan), curve spans, micro-segments, cursor |
| `CurveSceneEditor.MouseEvents.cs` | Mouse/keyboard handling: selection, dragging (incl. batch multi-select), vertex insertion, extension, deletion |
| `CurveSceneEditor.Utility.cs` | Helpers: mouse→world (plane projection), snapping, geometry utilities (point-line distance, Bezier interpolation) |
| `CurveSceneRenderer.cs` | SceneView rendering: curve polylines, vertices/handles/arrows, preview wireframes (native primitive sizes), cursor |
| `CurveObjectBuilder.cs` | Generator: samples the curve, spawns `PrimitiveComponent` per segment, registers Undo, organizes the parent object |
| `CurvePlacementHelper.cs` | **Placement calculator**: unified per-segment position/rotation/scale math shared by generation and preview (preview = generated) |
| `L10n.cs` | Static localization dictionary + switching API |

> `CurveTool` and `CurveSceneEditor` are partial classes split across files by responsibility.

---

## 3. Architecture Overview

```
┌─────────────────────────────── Unity Editor ───────────────────────────────┐
│                                                                              │
│  ┌──────────────┐   ┌──────────────────┐   ┌─────────────────────────────┐  │
│  │  CurveTool   │   │ CurveSceneEditor │   │   CurveSceneRenderer        │  │
│  │ (EditorWindow)│  │ (SceneView input)│   │   (SceneView rendering)      │  │
│  │ Edit/Settings │  │ hit→drag→insert  │   │ curve/verts/handles/preview  │  │
│  │               │  │ →extend          │   │ /cursor                     │  │
│  └──────┬───────┘   └────────┬─────────┘   └────────────┬────────────────┘  │
│         │  UI read/write     │ event read/write         │ read-only          │
│         ▼                    ▼                          ▼                   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    CurveManager (data layer)                          │   │
│  │  MonoBehaviour scene carrier (__CurvesToolData__) · static singleton  │   │
│  │  · dirty marking                                                       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│         │ serialized (scene .unity file, saved via Ctrl+S)                 │
│         ▼                                                                   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  Data models: BezierCurve → CurveVertex / SegmentInfo                 │   │
│  │  Plain [Serializable] classes — no GameObjects, serializable, JSON    │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌─────────────────────────────── Generation pipeline ──────────────────┐   │
│  │  CurveObjectBuilder.Build(curve)                                      │   │
│  │    → SamplePoints / SamplePoints3D                                    │   │
│  │    → CurvePlacementHelper.ComputePlacement (pos/rot/scale per seg)    │   │
│  │    → Spawn: Assets/Resources/Blocks/Primitives/{type}.prefab          │   │
│  │    → PrimitiveComponent (parented under "Curve Generated_{name}")     │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Design principles**:
1. **Data/view separation**: data models are plain [Serializable] classes; rendering, interaction and UI only read/write data.
2. **Scene as database**: curve data lives in the scene — each scene owns an independent curve set, saved/loaded with the scene.
3. **Preview == generation**: both share `CurvePlacementHelper.ComputePlacement` so the wireframe preview always matches the generated result.
4. **Session vs. persistent state**: selection/multi-select/segment selection are `[NonSerialized]` and never written into the scene.

---

## 4. Data Models

### 4.1 CurveManager (scene data carrier)

- **Type**: `MonoBehaviour` on the hidden scene object `__CurvesToolData__` (`hideFlags = HideFlags.HideInHierarchy` — invisible in the hierarchy but serialized with the scene).
- **Singleton**: the static `CurveManager.Instance` property. Binding (`BindToActiveScene()`):
  1. Search scene roots for an existing `CurveManager` component (hit on scene reopen — data restored from the scene);
  2. If absent, create the hidden carrier object and add the component;
  3. After every binding, run `MigrateFromLegacyAsset()` (one-time migration, see §9).
- **Scene switching**: the static constructor subscribes to `EditorSceneManager.sceneOpened` and clears the `_instance` cache; the `Instance` getter also re-binds when `gameObject.scene != SceneManager.GetActiveScene()`.
- **Dirty-marking contract (important)**: every data mutation path must end with `MarkDirty()`, which does both:
  ```csharp
  EditorUtility.SetDirty(this);                          // component dirty
  EditorSceneManager.MarkSceneDirty(gameObject.scene);   // scene dirty (saved via Ctrl+S)
  ```
  Unmarked changes are never written to the scene file. All tool entry points are covered (including the patched 3D-create and delete-all paths).
- **Serialized fields**: `Curves` (List<BezierCurve>), cursor (CursorPosition/CursorLocked/CursorLockX/Y/Z/CursorReferenceMode).
- **Session fields** (NonSerialized): SelectedCurveIndex, SelectedVertexIndex, SelectedVertexIndices.

### 4.2 BezierCurve

| Field | Description |
|---|---|
| `Name` | Curve name (auto-deduped by the tool: name + numeric suffix) |
| `Is3D` | 3D curve (vertices use Height; UpAxis is ignored) |
| `UpAxis` | 2D editing plane: Y=up (XZ plane), Z=up (XY plane), X=up (YZ plane) |
| `Vertices` | Vertex list (List<CurveVertex>) |
| `Segments` | Micro-segment list (List<SegmentInfo>); count = spans × SegmentCount |
| `SegmentCount` | Resolution per span (1..256, clamped in the UI) |
| `IsLoop` | Closed loop |
| `IsEnabled/IsVisible/IsLocked` | Enabled (can generate) / visible / locked (no editing, orange display) |

**Core methods**:
- `RebuildSegments()`: grows/trims `Segments` to match `TotalSegmentCount`.
- `SamplePoints()` / `SamplePoints3D()`: sample micro-segment endpoints along the curve; 3D curves sample the height component separately via `SamplePointZ()` and combine.
- `RecalculateHandles()`: recomputes handles from HandleType (Auto/Vector recomputed; endpoint mirroring; Aligned family synced from the opposite side); a 2-vertex loop approximates a circle (tangent + 1/3 chord).
- `MapToWorld/MapFromWorld/PlaneNormal`: 2D-plane ↔ world mapping (by UpAxis).

### 4.3 CurveVertex (coordinate convention)

**Key conventions (read carefully)**:
- 2D curves: `Position = (worldX, worldZ)`, `Height = 0`. World Y comes from the UpAxis mapping.
- 3D curves: `Position = (worldX, worldZ)` (XZ projection), `Height = worldY`.
- Handles: `LeftHandle/RightHandle = (X offset, Z offset)` relative to the vertex; `LeftHandleHeight/RightHandleHeight` = world-Y offsets.
- Convenience properties: `PositionV3`, `LeftHandleOffsetV3`, `RightHandleOffsetV3` expose full 3D world coordinates/offset vectors.

**Handle types** (`HandleTypeA/B`; A constrains the right handle from the left, B the left from the right):
| Type | Semantics |
|---|---|
| Auto | Automatic: both ends aligned; angle/length follow neighbors |
| Aligned | Direction-locked (symmetric), independent length |
| AlignedLength | Fully mirrored (`right = -left`) |
| Vector | Points at the neighbor (straight spans) |
| Free | Unconstrained |

**Constraint application**: `ApplyHandleType(movedHandle, is3d)` runs after moving one handle and constrains the opposite side by the moved side's type (Auto/AlignedLength → mirror; Aligned → direction sync, length free). **Dragging or UI-editing an Auto/Vector-family handle downgrades it to Free** so automatic math never overrides manual tweaks.

### 4.4 SegmentInfo (segment parameters)

| Field | Description | Effect at generation |
|---|---|---|
| `PrimitiveType` | Sphere/Capsule/Cylinder/Cube/Plane/Quad | Chooses the prefab |
| `BaseScale` | Base scale | Start of the scale chain |
| `FitSegmentLength` + `FitAxis` | Fit the segment length into an axis | `scale[FitAxis] *= segLen` |
| `RelativeScale` | Relative multiplier | Final = Base → FitLength → ×Relative |
| `PositionOffset` | Center offset (relative to segment length; 0=center, 0.5=half) | Offset along the tangent |
| `PositionOffset3D` | Local XYZ additive offset | `pos += rot * offset3D` |
| `RotationOffset` | Euler rotation offset | `rot = LookRotation * Euler(offset)` |

---

## 5. Interaction System (CurveSceneEditor)

### 5.1 Event dispatch (OnSceneGUI)

- Only when the tool window exists and `IsEditMode` (Curve Edit toggle) is on, events are consumed (`HandleUtility.AddDefaultControl` blocks default scene selection/orbit).
- Dispatch: MouseDown/MouseDrag/MouseUp/KeyDown → matching handlers.

### 5.2 Hit-test priority (HitTest)

7 passes, highest priority first:
1. Selected handles → 2. Unselected handles → 3. Selected vertices → 4. Unselected vertices → 5. Selected arrows → 6. Unselected arrows → 7. Locked curves.

Vertex sub-elements (`SelectedSubElement`):
| Value | Meaning |
|---|---|
| 1 | Left handle |
| 2 | Right handle |
| 3 | The vertex itself |
| 4 | Vertex height arrow (3D) |
| 5 | Left-handle height arrow (3D) |
| 6 | Right-handle height arrow (3D) |

Hit radii scale with the view (`HandleUtility.GetHandleSize`); with multiple curves on different axes, the mouse is projected per-curve by its UpAxis to avoid cross-hit.

### 5.3 Drag logic (HandleMouseDrag)

- **Cursor drag**: projection onto the camera-facing plane + snapping + per-axis locks.
- **Vertex/handle drag**: start value (`_dragStartValue`) → delta → per-axis locks → `ApplyHandleType` constraint.
- **3D curves**: Y dragging uses screen-Y delta × world-per-pixel ratio; height arrows (case 4/5/6) handled separately.
- **Batch move**: after Shift multi-select, dragging moves all vertices in `_dragStartValues` together.
- **Undo spam guard**: `_undoRecorded` flag — `Undo.RecordObject` fires once per drag.
- Snapping: `EditorSnapSettings.gridSnapEnabled` or Ctrl/Cmd → grid/increment snap.

### 5.4 Shortcut table (editor interaction)

| Action | Description |
|---|---|
| Left-click vertex/handle | Select and drag |
| Left-click micro-segment midpoint | Select the resolution segment |
| Alt + Left-click on a span | Select the whole Bezier span (all its micro-segments) |
| Shift + Left-click | Add to selection |
| Alt + Right-click on a span | Insert a new vertex (3D: nearest point on the curve) |
| Ctrl + Right-click on an endpoint | Extend a new vertex from the endpoint |
| Delete | Remove the selected vertex (curves keep ≥ 2 vertices) |

---

## 6. Rendering System (CurveSceneRenderer)

- Registered/unregistered via `CurveSceneRenderer.Register()/Unregister()` (SceneView.duringSceneGui), managed by CurveTool.OnEnable/OnDisable.
- **Edit mode**: full rendering (curve polyline + vertices/handles + preview wireframes toggle).
- **Preview mode** (without edit mode): preview wireframes only.
- Color scheme: curve=white, vertex=black, handle line by type (Auto=green, Aligned=blue, AlignedLength=cyan, Vector=orange, Free=gray), handle end=red, selected=yellow, selected segment=blue, locked=orange; all adjustable in the window/settings.
- **Preview wireframes use native primitive sizes** (Cube/Sphere 1×1×1, Capsule/Cylinder 1×2×1, Plane 10×1×10, Quad 1×1) transformed by `Matrix4x4.TRS`, matching the real prefab dimensions.
- Cursor: wireframe sphere (three rings) + axis-colored arrows, view-scaled.

---

## 7. Generation System

### 7.1 Flow (CurveObjectBuilder.Build)

1. Validate: curve non-null, ≥ 2 vertices, ≥ 2 sample points.
2. `RebuildSegments()` to sync segment data.
3. Create the parent object `Curve Generated_{name}` (`Undo.RegisterCreatedObjectUndo`).
4. Per segment, call `CurvePlacementHelper.ComputePlacement` for pos/rot/scale; skip segments shorter than 0.001.
5. `Spawn`: load `Assets/Resources/Blocks/Primitives/{type}.prefab` → `PrimitiveComponent.Create` → set position/rotation/scale/color/collidable/visible → parent it (Undo registered).
6. Select the parent, frame it, and log a green `gen_success` message (count + curve name).

### 7.2 Placement math (CurvePlacementHelper.ComputePlacement)

- **2D**: segment midpoint + tangent offset (PositionOffset × segLen) → `MapToWorld`; direction = `MapToWorld(a+dir2) - MapToWorld(a)`; when parallel to the plane normal, `LookRotation` degenerates → `FromToRotation` fallback.
- **3D**: uses the 3D sample points directly for distance/direction.
- Rotation = `LookRotation(dir, PlaneNormal) * Euler(RotationOffset)`.
- Scale chain: `BaseScale → (FitSegmentLength ? scale[FitAxis]*=segLen : unchanged) → ×RelativeScale`.

---

## 8. Persistence Design

| Data | Storage | Mechanism |
|---|---|---|
| Curve data | Scene .unity file | CurveManager component serialized with the scene; mutations must `MarkDirty()` (scene dirty) before Ctrl+S |
| Tool settings | `{ToolDirectory}/CurveToolSettings.json` | `SaveSettings()` on OnDisable/change (JsonUtility); `LoadSettings()` on OnEnable |
| Language | `Language` field inside the settings JSON | via `L10n.SetLanguage` |

- **ToolDirectory derivation**: `AssetDatabase.FindAssets("CurveManager t:MonoScript")` → script path → directory (follows folder moves; falls back to `Assets/Tools/CurvesTool`).
- **Scene switching**: each scene owns its data; `sceneOpened` clears the cache and re-binds.
- **Migration**: `MigrateFromLegacyAsset()` — if the legacy `CurveManager.asset` (from the ScriptableObject era) exists and the new component has no data, import Curves/cursor fields into the scene component via reflection, then delete the old asset; **on failure or non-empty component the old asset is kept** (no destructive removal).
- **Export API**: `SerializeToJson()` / `SaveToFile(path)` (no UI entry yet; available for extensions).

---

## 9. UI Structure (CurveTool)

- Edit tab: `Curve Tools` (flip X/Y/Z, mirror X/Y/Z, cursor reference frame, cursor properties/lock/reset), `Curve List` (create 2D/3D + per-row ○select/D display/E enable/L lock/name/axis/segments/R loop/C copy/✕ delete + delete-all), `Vertex & Handle Properties` (position + per-axis locks, handle type, left/right handle positions), `Segment Properties` (base primitive/base scale/center offset/fit segment length/relative scale/rotation offset/position offset; batch-applied to all selected segments), `Generate Objects` (color + Generate + Preview toggle).
- Settings tab: size (vertex/handle/arrow), snapping (grid/increment), colors (vertex/handle end), language, reset-to-default.

---

## 10. Localization (L10n)

- Structure: `Dictionary<string, Dictionary<Lang, string>>`, `Lang { EN, ZH }`.
- Usage: `L10n.T("key")`, `L10n.T("key", args)` (string.Format style, e.g. `del_confirm` with {0}).
- **Adding entries**: add a key with EN + ZH values in `L10n.Data`; keep new tab strings in sync.

---

## 11. External Dependencies

| Dependency | Location | Notes |
|---|---|---|
| `PrimitiveComponent` | `Assets/DONT TOUCH/Scripts/BlockComponents/PrimitiveComponent.cs` | Component attached to spawned blocks (extends `SchematicBlock`; exposes Color/Collidable/Visible) |
| Primitives prefabs | `Assets/Resources/Blocks/Primitives/{Capsule,Cube,Cylinder,Plane,Quad,Sphere}.prefab` | Loaded by PrimitiveType; LogError + skip the segment when missing |

> Adding a new primitive type requires touching: the `SegmentInfo.PrimitiveType` enum reference, the `L10n` `primitive_*` entries, the `primNames` array in `CurveTool.EditTab`, the wireframe branch in `CurveSceneRenderer`, and a matching prefab under `Resources/Blocks/Primitives`.

---

## 12. Extension Guide

### 12.1 New curve operation (e.g. "rotate 90°")
1. Add a button in `DrawCurveToolsFoldout` (`CurveTool.EditTab.cs`);
2. Mutate data following the pattern: `Undo.RecordObject(_manager, "action") → mutate → _manager.MarkDirty() → SceneView.RepaintAll()`.

### 12.2 New setting
1. Add a public field (with default) on `CurveTool`;
2. Add the field to `ToolSettings` + sync SaveSettings/LoadSettings;
3. Add a UI row in `SettingsTab`;
4. Add the default to the *Reset to Default* button.

### 12.3 New handle type
1. Add to the `HandleType` enum;
2. Add constraint logic in `CurveVertex.ApplyHandleType` and `BezierCurve.RecalculateHandles`;
3. Add `ht_*` entries in `L10n` and sync the `_htNames` array in `CurveTool.EditTab`;
4. Add the matching line color in `CurveSceneRenderer.GetHandleLineColor`.

### 12.4 New spawnable type
See §11 (dependency checklist); in particular, the `CurveSceneRenderer` wireframe must match the prefab's native size (see the size table in the comments).

---

## 13. Code Conventions & Caveats

- **Editor scripts**: this tool is editor-only functionality but lives in a normal folder (not an Editor folder); the project's Assembly-CSharp references UnityEditor.CoreModule (current project state) — **do not introduce runtime-unavailable APIs into runtime paths**.
- **Undo contract**: record `Undo.RecordObject(_manager, "action")` before every data mutation (scene components support Undo natively); drag scenarios record exactly once.
- **Dirty contract**: every data mutation must end with `MarkDirty()` (scene-data mode — otherwise Ctrl+S won't persist). Always add it on new mutation paths.
- **Encoding**: UTF-8 without BOM; function-level comments (Chinese is the project convention); /// comments on public fields.
- **Serialization**: new persistent fields must be [Serializable]; session state must be [NonSerialized].
- **Naming**: CurveTool (window), CurveManager (data), CurveSceneEditor/Renderer (scene layer), CurveObjectBuilder/CurvePlacementHelper (generation layer) — new classes follow the `Curve` prefix.
- **Avoid**: referencing UnityEngine.Object inside data models (keep them plain and serializable); mutating data directly from render/interaction code (go through CurveManager APIs + MarkDirty).

---

## 14. Known Limitations

1. **2-vertex loops**: approximated as a circle (tangent + 1/3 chord), not a perfect circle.
2. **Play mode**: `Instance` returns the editor scene object (not the Play copy); edit operations are intended for Edit mode.
3. **Hidden carrier**: `__CurvesToolData__` is invisible in the hierarchy (HideInHierarchy); deleting it recreates an empty carrier (a saved scene file can restore previous data).
4. **Segment cap**: SegmentCount is clamped to 1..256 (shared by UI and generation to prevent performance explosions).
5. **Tiny segments**: generation skips segments shorter than 0.001 (ComputePlacement returns false).
6. **Legacy migration**: only handles "old asset exists AND component empty"; the historical ScriptableObject asset has been removed after the scene-data migration.
7. **Corrupt settings file**: LoadSettings silently ignores it (empty catch) and uses defaults.

---

## 15. Testing Recommendations

- **Scene persistence**: create a curve → edit → Ctrl+S → reopen the scene → verify data (check `__CurvesToolData__` and the `Curves` field in the .unity file).
- **Undo**: after drag/insert/delete, step back with Ctrl+Z and check the `MarkSceneDirty` state.
- **Multi-scene**: switching scenes keeps each curve set independent.
- **Generation consistency**: enable Preview → Generate → compare wireframes with the actual blocks (position/rotation/scale).
- **3D curves**: height-arrow drags, Z sampling, LookRotation fallback (vertical segments).
- **Multi-select**: Shift batch dragging combined with per-axis locks.
