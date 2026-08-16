# 曲线工具 — 开发者文档（README_CurveTool_DEV）

> 适用对象：需要维护、扩展或理解曲线工具内部实现的开发者。
> 面向普通用户的文档见 [README_CurveTool.md](./README_CurveTool.md)。

---

## 1. 项目概览

曲线工具（Curve Tool）是一个 **Unity 编辑器工具**，在 SceneView 中以类似 Blender 的贝塞尔曲线编辑方式创建、编辑贝塞尔曲线，并**沿曲线生成物体**（依赖项目内的 `PrimitiveComponent` 与 Primitives Prefab 体系）。

| 项目 | 说明 |
|---|---|
| 语言 | C#（Unity 2021.3+，C# 9 语法，含 switch 表达式/模式匹配） |
| 程序集 | 项目默认程序集 `Assembly-CSharp`（无 asmdef；编辑器 API 可用，项目现状允许） |
| 数据存储 | **场景内**（`CurveManager` 组件挂在隐藏对象 `__CurvesToolData__` 上，随场景序列化） |
| 设置存储 | JSON 文件 `CurveToolSettings.json`（工具目录内，路径动态推导） |
| 多语言 | 内置 EN / 简体中文（`L10n` 静态字典） |
| 外部依赖 | `PrimitiveComponent`（`Assets/DONT TOUCH/Scripts/BlockComponents/PrimitiveComponent.cs`）、Primitives Prefab（`Assets/Resources/Blocks/Primitives/*.prefab`） |

---

## 2. 目录结构与文件职责

`Assets/Tools/CurvesTool/`：

| 文件 | 职责 |
|---|---|
| `CurveTool.cs` | EditorWindow 主骨架：生命周期（OnEnable/OnDisable）、模式栏、业务逻辑（建曲线/复制/镜像）、设置持久化（JSON 读写） |
| `CurveTool.EditTab.cs` | 编辑页签 UI：曲线工具折叠区（翻转/镜像/游标）、曲线列表、顶点与控制柄属性、段属性、生成按钮 |
| `CurveTool.SettingsTab.cs` | 设置页签 UI |
| `CurveManager.cs` | **数据层核心**：场景数据载体（MonoBehaviour）、单例绑定、数据操作（增删曲线/顶点/选中）、脏标记、旧资产迁移、JSON 导出 |
| `BezierCurve.cs` | 单条曲线：顶点/段列表、采样（2D/3D）、控制柄自动重算（RecalculateHandles）、坐标映射（MapToWorld/MapFromWorld） |
| `BezierCurveData.cs` | 数据模型：`UpAxis`/\`HandleType\` 枚举、`CurveVertex`（顶点 + 左右控制柄 + 分轴锁 + 类型约束 ApplyHandleType） |
| `SegmentInfo.cs` | 小线段（分辨率段）独立参数：生成物体类型、缩放/偏移/旋转 |
| `CurveSceneEditor.cs` | SceneView 交互主入口：注册钩子、事件分发（MouseDown/Drag/Up/KeyDown） |
| `CurveSceneEditor.HitTest.cs` | 命中检测：顶点/控制柄/箭头（7 轮优先级扫描）、曲线段、小线段、游标 |
| `CurveSceneEditor.MouseEvents.cs` | 鼠标/键盘事件处理：选择、拖拽（含多选批量）、插入顶点、延伸顶点、删除 |
| `CurveSceneEditor.Utility.cs` | 辅助：鼠标→世界坐标（平面投影）、吸附、几何工具（点线距/贝塞尔插值） |
| `CurveSceneRenderer.cs` | SceneView 渲染：曲线折线、顶点/控制柄/箭头、预览线框（各 Primitive 原生尺寸）、游标 |
| `CurveObjectBuilder.cs` | 生成器：沿曲线采样、逐段 Spawn `PrimitiveComponent`、Undo 注册、父对象组织 |
| `CurvePlacementHelper.cs` | **放置参数计算器**：段位置/旋转/缩放统一算法，生成与预览共用（预览所见 = 生成所得） |
| `L10n.cs` | 静态多语言字典 + 切换 API |

> 类结构：`CurveTool` 与 `CurveSceneEditor` 为 partial class，按职责拆文件。

---

## 3. 架构总览

```
┌─────────────────────────────── Unity 编辑器 ───────────────────────────────┐
│                                                                              │
│  ┌──────────────┐   ┌──────────────────┐   ┌─────────────────────────────┐  │
│  │  CurveTool   │   │ CurveSceneEditor │   │   CurveSceneRenderer        │  │
│  │  (EditorWindow)│ │ (SceneView 交互) │   │   (SceneView 渲染)           │  │
│  │  编辑/设置     │   │ 命中→拖拽→插入→延伸 │   │ 曲线/顶点/柄/预览/游标      │  │
│  └──────┬───────┘   └────────┬─────────┘   └────────────┬────────────────┘  │
│         │   UI 读写           │ 事件读写                  │ 只读              │
│         ▼                    ▼                          ▼                   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                     CurveManager（数据层）                            │   │
│  │  MonoBehaviour 场景载体（__CurvesToolData__）· 静态单例 · 脏标记       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│         │ 序列化（场景 .unity 文件，Ctrl+S 落盘）                             │
│         ▼                                                                   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  数据模型：BezierCurve → CurveVertex / SegmentInfo                    │   │
│  │  纯数据类（[Serializable]），无 GameObject，可序列化、可 JSON 导出      │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  ┌────────────────────────────── 生成管线 ──────────────────────────────┐   │
│  │  CurveObjectBuilder.Build(curve)                                      │   │
│  │    → SamplePoints/SamplePoints3D 采样                                  │   │
│  │    → CurvePlacementHelper.ComputePlacement（每段：pos/rot/scale）      │   │
│  │    → Spawn：Assets/Resources/Blocks/Primitives/{type}.prefab           │   │
│  │    → PrimitiveComponent（挂到父对象"曲线生成_{Name}"下）                │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────┘
```

**设计原则**：
1. **数据与视图分离**：数据模型（BezierCurve 等）是纯 [Serializable] 类；渲染/交互/UI 只读写数据。
2. **场景即数据库**：曲线数据保存在场景中，每个场景拥有独立曲线集，随场景保存/加载。
3. **预览与生成同源**：生成与预览共用 `CurvePlacementHelper.ComputePlacement`，保证线框预览与实际生成结果一致。
4. **会话态与持久态分离**：选中/多选/段选中均为 `[NonSerialized]`，不写入场景。

---

## 4. 数据模型详解

### 4.1 CurveManager（场景数据载体）

- **类型**：`MonoBehaviour`，挂在场景隐藏对象 `__CurvesToolData__`（`hideFlags = HideFlags.HideInHierarchy`，层级面板不可见但随场景序列化）。
- **单例**：`CurveManager.Instance` 静态属性。绑定逻辑 `BindToActiveScene()`：
  1. 在活动场景根对象中查找已有 `CurveManager` 组件（场景重新打开时命中，数据随场景恢复）；
  2. 未找到则创建隐藏载体对象并挂组件；
  3. 每次绑定后调用 `MigrateFromLegacyAsset()`（旧资产一次性迁移，见 §9）。
- **场景切换**：静态构造函数注册 `EditorSceneManager.sceneOpened`，清空 `_instance` 缓存；`Instance` getter 同时校验 `gameObject.scene != SceneManager.GetActiveScene()` 时重绑。
- **脏标记约定（重要）**：所有数据修改路径必须最终调用 `MarkDirty()`，该方法同时执行：
  ```csharp
  EditorUtility.SetDirty(this);                          // 组件脏
  EditorSceneManager.MarkSceneDirty(gameObject.scene);   // 场景脏（Ctrl+S 才落盘）
  ```
  未标脏的修改不会写入场景文件。工具内所有入口均已覆盖（含 3D 建曲线、删除全部等补丁点）。
- **序列化字段**：`Curves`（List<BezierCurve>）、游标（CursorPosition/CursorLocked/CursorLockX/Y/Z/CursorReferenceMode）。
- **会话字段**（NonSerialized）：SelectedCurveIndex、SelectedVertexIndex、SelectedVertexIndices。

### 4.2 BezierCurve

| 字段 | 说明 |
|---|---|
| `Name` | 曲线名（工具内自动去重：名+数字后缀） |
| `Is3D` | 3D 曲线（顶点使用 Height，UpAxis 无效） |
| `UpAxis` | 2D 曲线编辑平面：Y=上（XZ 平面）、Z=上（XY 平面）、X=上（YZ 平面） |
| `Vertices` | 顶点列表（List<CurveVertex>） |
| `Segments` | 小线段列表（List<SegmentInfo>），数量 = 跨段数 × SegmentCount |
| `SegmentCount` | 每跨段的分辨率（1..256，UI 强制限制） |
| `IsLoop` | 闭环 |
| `IsEnabled/IsVisible/IsLocked` | 启用（可生成）/ 可见 / 锁定（禁止编辑，橙色显示） |

**核心方法**：
- `RebuildSegments()`：按 TotalSegmentCount 增删 Segments 列表（保持同步）。
- `SamplePoints()` / `SamplePoints3D()`：沿曲线采样小线段端点；3D 曲线用 `SamplePointZ()` 单独采样高度分量再组合。
- `RecalculateHandles()`：按 HandleType 重算控制柄（Auto/Vector 自动计算，端点镜像连接侧，Aligned 族从对侧同步），2 顶点闭环近似圆形（切线 + 1/3 弦长）。
- `MapToWorld/MapFromWorld/PlaneNormal`：2D 平面坐标 ↔ 世界坐标映射（依 UpAxis）。

### 4.3 CurveVertex（坐标系约定）

**关键约定（务必理解）**：
- 2D 曲线：`Position = (世界X, 世界Z)`，`Height = 0`。世界 Y 由 UpAxis 映射。
- 3D 曲线：`Position = (世界X, 世界Z)`（XZ 投影），`Height = 世界Y`。
- 控制柄：`LeftHandle/RightHandle = (X偏移, Z偏移)`（相对顶点的偏移量），`LeftHandleHeight/RightHandleHeight` = 世界 Y 偏移。
- 辅助属性：`PositionV3`、`LeftHandleOffsetV3`、`RightHandleOffsetV3` 提供完整 3D 世界坐标/偏移向量。

**控制柄类型**（`HandleTypeA/B`，A=左柄约束右柄，B=右柄约束左柄）：
| 类型 | 语义 |
|---|---|
| Auto | 自动：两端对齐，按相邻顶点自动计算角度与长度 |
| Aligned | 对齐：方向锁定（对称），长度独立 |
| AlignedLength | 镜像：方向+长度完全镜像（`右 = -左`） |
| Vector | 矢量：指向相邻顶点（段间为直线） |
| Free | 自由：无约束 |

**约束应用**：`ApplyHandleType(movedHandle, is3d)` 在移动一侧控制柄后调用，按被移动侧类型约束对侧（Auto/AlignedLength → 镜像同步；Aligned → 方向同步、长度独立）。**拖拽与 UI 编辑 Auto/Vector 族柄时自动降级为 Free**（避免自动计算覆盖用户手动调整）。

### 4.4 SegmentInfo（段参数）

| 字段 | 说明 | 生成作用 |
|---|---|---|
| `PrimitiveType` | 物体类型（Sphere/Capsule/Cylinder/Cube/Plane/Quad） | 选择 Prefab |
| `BaseScale` | 基础缩放 | 缩放链起点 |
| `FitSegmentLength` + `FitAxis` | 是否将段长覆盖到指定轴 | `scale[FitAxis] *= segLen` |
| `RelativeScale` | 相对缩放乘数 | 最终缩放 = BaseScale → FitLength → ×RelativeScale |
| `PositionOffset` | 中心点偏移（相对段长，0=中心，0.5=半段长） | 沿切线方向偏移 |
| `PositionOffset3D` | 段本地空间 XYZ 加算偏移 | `pos += rot * offset3D` |
| `RotationOffset` | 欧拉旋转偏移 | `rot = LookRotation * Euler(offset)` |

---

## 5. 交互系统（CurveSceneEditor）

### 5.1 事件分发（OnSceneGUI）

- 仅当工具窗口存在且 `IsEditMode`（曲线编辑开关）时消耗事件（`HandleUtility.AddDefaultControl` 阻止默认场景选择/轨道）。
- 分发：MouseDown/MouseDrag/MouseUp/KeyDown → 对应 Handler。

### 5.2 命中检测优先级（HitTest）

7 轮扫描，优先级从高到低：
1. 已选控制柄 → 2. 未选控制柄 → 3. 已选顶点 → 4. 未选顶点 → 5. 已选箭头 → 6. 未选箭头 → 7. 锁定曲线。

顶点子元素（`SelectedSubElement`）：
| 值 | 含义 |
|---|---|
| 1 | 左控制柄 |
| 2 | 右控制柄 |
| 3 | 顶点自身 |
| 4 | 顶点高度箭头（3D） |
| 5 | 左柄高度箭头（3D） |
| 6 | 右柄高度箭头（3D） |

命中半径随视图缩放（`HandleUtility.GetHandleSize`）；多曲线不同轴向时按各曲线 UpAxis 投影鼠标，避免串位。

### 5.3 拖拽逻辑（HandleMouseDrag）

- **游标拖拽**：相机前向平面投影 + 吸附 + 分轴锁。
- **顶点/控制柄拖拽**：记录起始值（`_dragStartValue`）→ 计算增量 → 应用分轴锁 → `ApplyHandleType` 约束对侧。
- **3D 曲线**：Y 拖拽用屏幕 Y 增量 × 世界每像素比例；高度箭头（case 4/5/6）单独处理。
- **批量移动**：Shift 多选后，拖拽同步移动 `_dragStartValues` 中所有顶点。
- **Undo 防爆**：`_undoRecorded` 标志，仅拖拽首帧 `Undo.RecordObject` 一次。
- 吸附：`EditorSnapSettings.gridSnapEnabled` 或 Ctrl/Cmd → 网格/增量吸附。

### 5.4 快捷键表（编辑器交互）

| 操作 | 说明 |
|---|---|
| 左键 顶点/控制柄 | 选中并拖拽 |
| 左键 小线段中点 | 选中分辨率段 |
| Alt + 左键 曲线段 | 选中整条贝塞尔跨度（所有小线段） |
| Shift + 左键 | 追加多选 |
| Alt + 右键 曲线段 | 插入新顶点（3D 曲线取曲线上最近点） |
| Ctrl + 右键 端点 | 从端点延伸新顶点 |
| Delete | 删除选中顶点（曲线至少保留 2 个顶点） |

---

## 6. 渲染系统（CurveSceneRenderer）

- 注册/注销：`CurveSceneRenderer.Register()/Unregister()`（SceneView.duringSceneGui），由 CurveTool.OnEnable/OnDisable 管理。
- **编辑模式**：完整渲染（曲线折线 + 顶点/控制柄 + 预览线框开关）。
- **预览模式**（不进入编辑）：仅渲染预览线框。
- 颜色体系：曲线=白、顶点=黑、曲柄线按类型（Auto=绿、Aligned=蓝、AlignedLength=青、Vector=橙、Free=灰）、曲柄端=红、选中=黄、选中段=蓝、锁定=橙；均可在窗口/设置中调整。
- **预览线框按 Primitive 原生尺寸**绘制（Cube/Sphere 1×1×1、Capsule/Cylinder 1×2×1、Plane 10×1×10、Quad 1×1），经 `Matrix4x4.TRS` 统一变形，与 Prefab 实际尺寸对齐。
- 游标：线框球体（三向圆环）+ 轴色箭头，大小随视图缩放。

---

## 7. 生成系统

### 7.1 流程（CurveObjectBuilder.Build）

1. 校验：曲线非空、顶点 ≥ 2、采样点 ≥ 2。
2. `RebuildSegments()` 同步段数据。
3. 创建父对象 `曲线生成_{Name}`（`Undo.RegisterCreatedObjectUndo`）。
4. 逐段调用 `CurvePlacementHelper.ComputePlacement` 计算 pos/rot/scale，跳过过短段（< 0.001）。
5. `Spawn`：加载 `Assets/Resources/Blocks/Primitives/{type}.prefab` → `PrimitiveComponent.Create` → 设置位置/旋转/缩放/颜色/可碰撞/可见 → 挂到父对象（Undo 注册）。
6. 选中父对象 + Frame + 绿色日志（`gen_success`，含数量与曲线名）。

### 7.2 放置算法（CurvePlacementHelper.ComputePlacement）

- **2D**：段中点 + 切线方向偏移（PositionOffset×段长）→ `MapToWorld`；方向 `MapToWorld(a+dir2)-MapToWorld(a)`；与平面法线平行时 `LookRotation` 退化 → `FromToRotation` 兜底。
- **3D**：直接用 3D 采样点距离/方向。
- 旋转 = `LookRotation(dir, PlaneNormal) * Euler(RotationOffset)`。
- 缩放链：`BaseScale → (FitSegmentLength ? scale[FitAxis]*=segLen : 不变) → ×RelativeScale`。

---

## 8. 持久化设计

| 数据类型 | 存储位置 | 机制 |
|---|---|---|
| 曲线数据 | 场景 .unity 文件 | CurveManager 组件随场景序列化；修改须 `MarkDirty()` 标记场景脏后 Ctrl+S 落盘 |
| 工具设置 | `{ToolDirectory}/CurveToolSettings.json` | OnDisable/变更时 `SaveSettings()`（JsonUtility 写入），OnEnable 时 `LoadSettings()` |
| 语言 | 设置 JSON 内 `Language` 字段 | `L10n.SetLanguage` |

- **ToolDirectory 推导**：`AssetDatabase.FindAssets("CurveManager t:MonoScript")` → 脚本路径 → 目录（目录移动自动跟随；找不到时兜底 `Assets/Tools/CurvesTool`）。
- **场景切换**：每个场景独立数据；`sceneOpened` 清缓存重绑。
- **迁移**：`MigrateFromLegacyAsset()` —— 若旧 `CurveManager.asset`（ScriptableObject 时代遗留）存在且新组件无数据，反射读取 Curves/游标字段导入场景组件后删除旧资产；**导入失败或组件已有数据时保留旧资产**（防误删）。
- **导出 API**：`SerializeToJson()` / `SaveToFile(path)`（当前无 UI 入口，供扩展使用）。

---

## 9. UI 结构（CurveTool）

- 编辑 Tab：`曲线工具`（翻转 X/Y/Z、镜像 X/Y/Z、游标参考系、游标属性/锁定/重置）、`曲线列表`（新建 2D/3D + 每行 ○选择/D显示/E启用/L锁定/名称/轴向/段数/R闭环/C复制/✕删除 + 删除全部）、`顶点与控制柄属性`（位置+分轴锁、控制柄类型、左右柄位置）、`选中段属性`（基础物体/基础缩放/中心点偏移/适应段长/相对缩放/旋转偏移/位置偏移，批量作用于所有选中段）、`物体生成`（颜色 + 生成 + 预览开关）。
- 设置 Tab：尺寸（顶点/控制柄/箭头）、吸附（网格步长/增量步长）、颜色（顶点/柄端）、语言、重置默认。

---

## 10. 本地化（L10n）

- 结构：`Dictionary<string, Dictionary<Lang, string>>`，`Lang { EN, ZH }`。
- 用法：`L10n.T("key")`、`L10n.T("key", args)`（string.Format 风格，如 `del_confirm` 的 {0}）。
- **添加词条**：在 `L10n.Data` 中新增键 + EN/ZH 两值；新增 Tab 文案时同步。

---

## 11. 外部依赖

| 依赖 | 位置 | 说明 |
|---|---|---|
| `PrimitiveComponent` | `Assets/DONT TOUCH/Scripts/BlockComponents/PrimitiveComponent.cs` | 生成物体的挂载组件（继承 `SchematicBlock`，含 Color/Collidable/Visible） |
| Primitives Prefab | `Assets/Resources/Blocks/Primitives/{Capsule,Cube,Cylinder,Plane,Quad,Sphere}.prefab` | 按 PrimitiveType 加载，缺失时 LogError 并跳过该段 |

> 新增 Primitive 类型需同时：扩展 `SegmentInfo.PrimitiveType` 枚举引用、`L10n` 的 `primitive_*` 词条、`CurveTool.EditTab` 的 `primNames` 数组、`CurveSceneRenderer` 的线框绘制分支、`Resources/Blocks/Primitives` 下对应 Prefab。

---

## 12. 扩展指南

### 12.1 新增曲线操作（如"旋转 90°"）
1. 在 `CurveTool.EditTab.cs` 的 `DrawCurveToolsFoldout` 添加按钮；
2. 修改数据时遵循模式：`Undo.RecordObject(_manager, "动作名") → 改数据 → _manager.MarkDirty() → SceneView.RepaintAll()`。

### 12.2 新增设置项
1. `CurveTool` 添加 public 字段（默认值）；
2. `ToolSettings` 类加字段 + SaveSettings/LoadSettings 同步；
3. `SettingsTab` 添加 UI 行；
4. `Reset to Default` 按钮中补充默认值。

### 12.3 新增控制柄类型
1. `HandleType` 枚举加值；
2. `CurveVertex.ApplyHandleType` 与 `BezierCurve.RecalculateHandles` 补充约束逻辑；
3. `L10n` 加 `ht_*` 词条；`CurveTool.EditTab` 的 `_htNames` 数组同步；
4. `CurveSceneRenderer.GetHandleLineColor` 添加对应线色。

### 12.4 新增生成物类型
见 §11（Primitive 依赖清单），重点：`CurveSceneRenderer` 线框按 Prefab 原生尺寸绘制（参考注释中的尺寸表）。

---

## 13. 代码约定与注意事项

- **编辑器脚本**：本工具全部为 Editor 功能，但位于普通目录（非 Editor 文件夹）；项目 Assembly-CSharp 引用了 UnityEditor.CoreModule（项目现状），**新增代码勿引入运行时不可用 API 到运行时路径**。
- **Undo 约定**：所有数据修改前 `Undo.RecordObject(_manager, "中文动作名")`（场景组件原生支持 Undo）；拖拽场景只记录一次。
- **脏标记约定**：任何数据修改最终必须 `MarkDirty()`（场景数据模式，否则 Ctrl+S 不落盘）。新增修改路径时务必补上。
- **编码**：UTF-8 无 BOM；函数级注释（中文）；public 字段加 /// 注释。
- **序列化**：新增持久字段须 [Serializable]；会话态字段标 [NonSerialized]。
- **命名**：CurveTool（窗口）、CurveManager（数据）、CurveSceneEditor/Renderer（场景层）、CurveObjectBuilder/CurvePlacementHelper（生成层）——新类遵循前缀 `Curve`。
- **避免**：不要在数据模型中引用 UnityEngine.Object（保持纯数据可序列化）；不要在渲染/交互中直接修改数据（走 CurveManager API + MarkDirty）。

---

## 14. 已知限制

1. **2 顶点闭环**：近似为圆形（切线 + 1/3 弦长），非精确圆。
2. **Play 模式**：`Instance` 返回编辑器场景对象（非 Play 副本），工具编辑操作建议在 Edit 模式进行。
3. **隐藏载体**：`__CurvesToolData__` 不出现在层级面板（HideInHierarchy），误删会重建为空数据（旧数据丢失前有场景文件可恢复）。
4. **段数上限**：SegmentCount 限制 1..256（UI 与生成共用，防性能爆炸）。
5. **过短段**：段长 < 0.001 时生成跳过（ComputePlacement 返回 false）。
6. **旧资产迁移**：仅处理"旧 asset 存在且组件无数据"的场景；历史 ScriptableObject 数据在场景化迁移完成后已删除旧资产文件。
7. **设置文件损坏**：LoadSettings 静默忽略（catch 空），使用默认值。

---

## 15. 测试建议

- **场景落盘**：建曲线 → 修改 → Ctrl+S → 重开场景 → 数据恢复（检查 .unity 文件中 `__CurvesToolData__` 与 `Curves` 字段）。
- **Undo**：拖拽/插入/删除后 Ctrl+Z 逐步回退，检查 `MarkSceneDirty` 状态。
- **多场景**：切换场景后各自曲线独立。
- **生成一致性**：开预览 → 生成 → 对比线框与实物体块的位置/旋转/缩放。
- **3D 曲线**：高度箭头拖拽、Z 值采样、LookRotation 退化兜底（竖直段）。
- **多选**：Shift 批量拖拽与分轴锁组合。
