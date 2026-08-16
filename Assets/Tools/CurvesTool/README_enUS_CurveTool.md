# Curve Tool

> Draw Bezier curves in your Unity scene Blender-style, then **spawn objects along the curve with one click**.
> For end users. Developers: see [README_enUS_CurveTool_DEV.md](./README_enUS_CurveTool_DEV.md).

---

## 1. What Is This?

Curve Tool is a Unity **editor tool** that lets you, inside the Scene view:

- 🎯 **Draw Bezier curves** — place vertices freely, drag handles; supports 2D planar curves and 3D spatial curves
- ✏️ **Fine-tune editing** — 5 Blender-style handle types, per-axis locks, grid snapping, multi-select batch dragging
- 🧱 **Spawn objects along curves** — every micro-segment has its own object type (Sphere / Capsule / Cylinder / Cube / Plane / Quad), scale, offset and rotation; generate the whole curve in one click
- 💾 **Data lives in the scene** — curves are saved with the scene (Ctrl+S); every scene owns an independent curve set

---

## 2. Requirements

| Item | Requirement |
|---|---|
| Unity | 2021.3 or newer |
| Project dependency | The `PrimitiveComponent` component and the 6 base prefabs under `Assets/Resources/Blocks/Primitives/` (Cube / Capsule / Cylinder / Sphere / Plane / Quad) |

---

## 3. Opening the Tool

Menu: **Tools → Curve Tool** (window title: Curve Tool).

---

## 4. Quick Start (5 Steps)

1. Open **Tools → Curve Tool**
2. In the **Edit** tab, press the **Curve Edit** button (turns green = editing mode enabled)
3. In the **Curve List**, enter a name and press **2D** or **3D** to create a curve
4. Select and drag vertices / handles in the Scene view to shape the curve (see the operation table below)
5. Press **Generate** to spawn objects along the curve (toggle **Preview** first to see wireframes)

---

## 5. Window Reference

### Edit Tab (top to bottom)

| Section | Purpose |
|---|---|
| **Curve Edit** toggle | Enables Scene-view editing of curves (button turns green when on) |
| **Curve Tools** | Flip X/Y/Z, Mirror X/Y/Z (about origin or cursor), Cursor Reference Frame toggle, cursor position (with per-axis locks) / lock / reset |
| **Curve List** | Create row (name + segment count + 2D/3D buttons); each row: select ○, display D, enable E, lock L, name, axis, segment count, loop R, copy C, delete ✕; "Delete All Curves" at the bottom |
| **Vertex & Handle Properties** | Position of the selected vertex (per-axis locks), handle type, left/right handle positions (per-axis locks) |
| **Segment Properties** | Base primitive, base scale, center offset, fit segment length, relative scale, rotation offset, position offset of selected micro-segments (applies to all selected segments) |
| **Generate Objects** | Object color, **Generate** button, **Preview** toggle |

### Settings Tab

| Group | Options |
|---|---|
| Size | Vertex size, handle size, arrow size (3D height arrows) |
| Snapping | Grid size (XYZ), increment step (XYZ) |
| Color | Vertex color, handle-end color |
| Language | English / 简体中文 (switches immediately) |
| Other | Reset to Default |

---

## 6. Scene Interaction Guide

| Action | Effect |
|---|---|
| **Left-click** vertex / handle | Select and drag |
| **Left-click** micro-segment midpoint | Select that resolution segment (edit its segment properties) |
| **Alt + Left-click** on a curve span | Select the whole Bezier span (all micro-segments of it) |
| **Shift + Left-click** | Add to selection (multi-select; dragging then moves all selected vertices together) |
| **Alt + Right-click** on a curve span | Insert a new vertex on the curve |
| **Ctrl + Right-click** on an endpoint | Extend a new vertex from the endpoint |
| **Delete** | Remove the selected vertex (a curve keeps at least 2 vertices) |
| **Ctrl (or Cmd) + drag** | Uses the *increment snap* step; plain drag uses the *grid size* (or Unity's Snap Settings grid snap when enabled) |

> Tip: a curve locked with **L** turns orange and cannot be edited; a curve hidden with **D** is not drawn in the scene.

### Handle Types (Blender-style)

| Type | Meaning |
|---|---|
| **Auto** | Both ends align automatically; angle and length follow the neighboring vertices |
| **Aligned** | Directions are symmetric (opposite), lengths are independent |
| **Aligned Length** | Direction and length fully mirrored (right handle = -left handle) |
| **Vector** | Handle points at the neighboring vertex; the span becomes a straight line |
| **Free** | Completely unconstrained |

> Dragging an *Auto / Vector* handle automatically switches it to *Free* so automatic recalculation never overrides your manual tweaks.

### Per-Axis Lock (L button)

Vertex positions, handle positions and the cursor position each have **L** buttons: while locked, that axis is protected from dragging (button turns orange) and its numeric field is disabled — great for precise single-axis edits.

---

## 7. Generating Objects

1. **Select a curve** (click ○ in the list, or select a vertex in the scene)
2. Click micro-segments (or Alt+Left-click to pick a whole span) and set, in **Segment Properties**:
   - **Base Primitive**: Sphere / Capsule / Cylinder / Cube / Plane / Quad
   - **Base Scale**: absolute size of the block
   - **Fit Segment Length**: when checked, multiplies the segment length into the chosen scale axis (X/Y/Z) so the block fills each segment
   - **Relative Scale**: an extra multiplier on top of base scale
   - **Center Offset / Position Offset / Rotation Offset**: fine adjustments along and around the segment direction
3. Press **Generate** — blocks are parented under `Curve Generated_{curve name}`

> 💡 **Preview**: toggle **Preview** to see block wireframes in the scene — what you see is exactly what you get (identical math as generation).

---

## 8. Data & Saving (Important)

- **Curve data is stored in the current scene**: press **Ctrl+S** after editing and the curves are saved with the scene file; reopening the scene restores them automatically.
- Every scene has its own independent curve set.
- The data lives on a hidden scene object (`__CurvesToolData__`) — nothing to manage manually.
- Tool settings (colors / sizes / language / snapping) are stored in `CurveToolSettings.json` inside the tool folder; they are global and scene-independent.

---

## 9. FAQ

**Q: I can't click anything in the Scene view?**
A: Make sure the tool window is open and the **Curve Edit** toggle is green.

**Q: Generated objects don't match the preview?**
A: Preview and generation share the exact same placement math, so they cannot diverge — double-check that the curve isn't hidden and the preview toggle is on while comparing.

**Q: Why are some segments missing objects?**
A: Segments shorter than 0.001 units are skipped automatically to avoid zero-size blocks.

**Q: I deleted the hidden `__CurvesToolData__` object?**
A: The tool recreates it (empty) on next open; if you saved the scene before, just reload the scene to restore the data.

**Q: My settings are messed up?**
A: Settings tab → **Reset to Default**.

**Q: How do I remove a curve?**
A: Press **✕** on its row for a single curve, or **Delete All Curves** at the bottom (confirmation dialog).

---

## 10. Version Info

- Tool name: Curve Tool
- Location: `Assets/Tools/CurvesTool/`
- Design: HuanSen233 · Code: DeepSeekAI
