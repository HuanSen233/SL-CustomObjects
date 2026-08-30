# TriangleTool — V1 (Exact) 三角面 Unity 工具

> 把 **TriangleScpSl**（[Foibos](https://github.com/Foiboss/TriangleScpSl)，CC-BY-SA 3.0）的
> **V1 (Exact) 基础算法**移植为 Unity 工具：用三个平行四边形（由 Unity Quad 构建）精确渲染一个三角面。
> V2（Approximate 拉伸聚类）与 V3（Hierarchical 层级父子）不在本工具范围内。

## 算法原理（V1 Exact）

### 1. 三角形 → 3 个平行四边形（半对角线参数化）

对三角形顶点 `a, b, c`，先算三边中点 `halfAb=(a+b)/2, halfAc=(a+c)/2, halfBc=(b+c)/2`，
再对每个顶点构造一个平行四边形，用 **半对角线**（`VLeft, VUp`）与中心 `Center` 参数化：

| 顶点 | Center | VLeft | VUp |
|------|--------|-------|-----|
| a | `(halfBc + a)/2` | `a - Center` | `halfAc - Center` |
| b | `(halfAc + b)/2` | `b - Center` | `halfAb - Center` |
| c | `(halfAb + c)/2` | `c - Center` | `halfBc - Center` |

角点 = `Center ± VLeft`、`Center ± VUp`。三个平行四边形精确铺满三角形；
中心的 medial 三角形被覆盖 3 次，但同平面、同色、不可见 → **像素级精确**。

### 2. 平行四边形 → 2 个 Quad（SetParent 变形技巧）

任意平行四边形可用 Unity Quad 渲染，关键是 **非均匀缩放继承**：

1. 取较长半对角线作 `up` 轴（保证仿射分解数值稳定）；
2. 仿射分解出内接矩形 `a × b` 与剪切因子 `x`（由 `upLen`、`leftY=dot(VLeft,upN)`、`leftX=⊥分量` 推导）；
3. **不可见父 Quad**：`scale=(x,1,1)`、`position=Center`、`rotation=LookRotation(vNormal, VUp)`，关闭渲染；
4. **可见子 Quad**：`SetParent(父)` 后 `localPosition=0`、`localRotation=Euler(0,0,-atan2(b,a))`、`localScale=(b,a,1)`。

子 Quad 继承父级的剪切缩放 `x` 与朝向，得到精确的平行四边形。**2 个 Quad / 平行四边形。**

### 3. 矩形优化

若 `|VLeft| ≈ |VUp|`（半对角线等长 ⇒ 边互相垂直），直接用 **1 个 Quad**：
`edgeA=VLeft+VUp, edgeB=VLeft-VUp`，`scale=(|edgeB|, |edgeA|, 1)`，`rotation=LookRotation(cross(edgeB,edgeA), edgeA.normalized)`。
每遇到一个矩形省 1 个 primitive。

## 三种模式（设置 Tab → 构建模式）

| 模式 | 原理 | Primitive 成本 |
|------|------|----------------|
| **V1 (Exact)** | 每个平行四边形 = Empty 父（剪切）+ 可见 Quad 子 | 像素级精确，Primitive 最多 |
| **V2 (Approximate)** | `VectorPhiSolver` 把平行四边形分解为 `(theta, phi)` 角度，角度相近者共享一个不可见 **stretch**（Empty 块，旋转 R(theta) + 缩放 `(cos(phi)·F, sin(phi)·F, 1)`，F=2），子 Quad 继承变形 | `stretch 数 + Quad 数`，误差由 `Accuracy`（世界单位）控制 |
| **V3 (Hierarchical)** | 在 V2 基础上：① 构建时优先把平行四边形挂到已有可见 Quad 下（TryCreateUnderParent，零 stretch）；② 构建后 `OptimizationPasses` 轮优化扫描，把 stretch 子重挂到可见 Quad；③ 合并低使用率 stretch | 最低，`ReparentedCount + 合并` 省掉 stretch |

关键机制（移植自 TriangleScpSl）：
- **角度聚类**：`StretchSpatialIndex` 是 `(theta, phi)` 空间的 2D 空间哈希；先查近邻，未命中再全量扫描（平行四边形有一整条有效分解曲线，远处 stretch 也可能在容差内）；
- **误差度量**：`StretchMath.MaxVertexError` 计算用候选 stretch 渲染时的最坏顶点位移，须 ≤ `Accuracy`；
- **V3 内联父级**：`TryFitUnderQuad` 把目标平行四边形角点变换到候选父 Quad 局部空间，在容差内则直接挂载；
- **合并**：`ConsolidateStretches` 按子数量从小到大排空 stretch，其子 Quad 在容差内迁到其他 stretch，排空即销毁。

### Primitive 计数

父对象为项目的 **Empty 块**（无渲染器），不计入渲染 primitive：

```
V1: 可见 Quad 块 = 平行四边形数；总块 = Quad + (平行四边形数 − 矩形数) 个 Empty + 1 根
V2: 总块 = 1 根 + stretch 数 + Quad 数 + 回退对×2
V3: 总块 = 1 根 + 在用 stretch 数 + Quad 数 + 回退对×2（被重挂的 Quad 不再占用 stretch）
```

## 文件结构

```
Assets/Tools/TriangleTool/
  README.md
  LICENSE                          # CC-BY-SA 3.0（继承自 TriangleScpSl）
  TestModels/Suzanne/              # 测试模型（Suzanne，OBJ + MTL + Blender 源文件）
  Runtime/
    TriangleParallelogramDecomposer.cs   # 三角形 → 3 平行四边形（纯数学）
    ParallelogramQuadBuilder.cs          # V1：平行四边形 → 2 块剪切构建 + 矩形单 Quad
    TriangleModelBuilder.cs              # 统一 V1/V2/V3 构建器（模式枚举 + 分帧 + 后处理）
    VectorPhiSolver.cs                   # V2/V3：(theta, phi) 角度分解求解器
    StretchSpatialIndex.cs               # V2/V3：角度空间 2D 哈希索引（stretch 复用查找）
    StretchMath.cs                       # V2/V3：stretch 变换数学 + 误差度量 + 合并
    ProjectBlockFactory.cs               # 项目块工厂（Empty.prefab / Primitives/Quad.prefab）
    TriangleData.cs                      # 三角形数据（顶点 + 颜色）
    TriangleFace.cs                      # 三角面数据（名称 + 3 点 + 颜色 + 可见/启用/锁定 + 选中态）
    TriangleFaceManager.cs               # 场景数据载体（隐藏对象 __TriangleToolData__，增删选/改脏/持久化）
    ObjTriangleParser.cs                 # OBJ 解析器（v/顶点色/mtllib/usemtl/Kd/f）
    ObjModelLoader.cs                    # OBJ 路径解析 + 加载入口
  Editor/
    TriangleTool.cs                # 窗口骨架（字段/生命周期/持久化/业务逻辑），UI 结构参考 CurvesTool
    TriangleTool.EditTab.cs        # 编辑 Tab（模式栏 + 三角面列表 + 三角面属性 + 生成 + 统计）
    TriangleTool.ImportTab.cs      # 模型导入 Tab（OBJ：路径/浏览/强制回退色/加载并构建/进度/取消）
    TriangleTool.SettingsTab.cs    # 设置 Tab（构建模式/输入/颜色/语言/重置）
    TriangleTool.L10n.cs           # 双语本地化（English/简体中文）
    TriangleSceneRenderer.cs       # SceneView 渲染（编辑：3 点+线框；预览：高亮轮廓）
    TriangleSceneEditor.cs         # SceneView 编辑（命中/拖拽 3 点，复用 0ToolLib 共享拖拽）
    TriangleToolSettings.json      # 持久化设置（窗口关闭自动保存）
    ObjBuildSession.cs             # 分帧构建会话（大模型不卡编辑器）
```

## 使用方法

### 编辑器窗口（UI 结构参考 CurvesTool）

- 菜单 **Tools → Triangle Tool → Open Window** 打开窗口；
- **编辑 Tab**：
  - **模式栏**：`编辑模式`（4/5，默认开启，场景点拖拽与线框生效）+ `预览`（1/5，高亮三角面轮廓）；
  - **三角面列表** 折叠区：名称 + `新建` 按钮创建面；行内 `○选中 · D 可见 · E 启用 · L 锁定 · C 复制 · ✕ 删除`，底部 `删除全部三角面`；
  - **三角面属性** 折叠区：选中面的 3 个世界坐标点 + 颜色（选中非锁定面即可编辑）；
  - **生成模型** 折叠区：`生成` 按钮，从所有启用 + 可见的面用 `TriangleModelBuilder` 构建平行四边形模型；每次点击都新建一个顶层父名（`TriangleModel (Tool)`，场景内去重）的**新**生成物体，不碰之前生成的物体；
  - **统计** 折叠区：平行四边形 / 可见 Quad / 矩形 / Stretch / 重挂数 / 节省数 / 总块数。
- **模型导入 Tab**：OBJ 路径 + 浏览、强制回退色、回退色、**加载并构建**（分帧，带进度条）/ **取消**；
- **设置 Tab**：构建模式（精度/优化轮数）、输入（移动工具 W / 编辑器吸附 / 吸附网格 / 增量）、颜色（面颜色/回退色/可碰撞）、语言（English/简体中文）、重置为默认值（窗口关闭自动保存为 `TriangleToolSettings.json`）。

在编辑模式下，选中一个面后场景中会显示 3 个可拖拽点与三角形线框（含一条蓝色**面朝向线**，方向随当前绕序）：拖点即可编辑（支持网格/增量吸附与 Undo）；启用**移动工具（W）**时用 PositionHandle 拖拽。**三角面属性**里的 `反转面` 控件交换 P2/P3 翻转面朝向。预览开启后按面颜色高亮轮廓（非编辑展示）。编辑模式与曲线工具互斥（同一时刻仅一个工具处于编辑模式）。默认三角面顶点为 `P1(0,0,0) P2(2,0,0) P3(0,0,2)`（位于 XZ 平面，法线 = ±Y）。

### 示例与测试模型

- 菜单 **Tools → Triangle Tool → Create Example Triangle**：场景原点生成示例三角面。
- 菜单 **Tools → Triangle Tool → Load Suzanne Test Model**：加载 `TestModels/Suzanne/Suzanne.obj`
  （507 顶点 / 500 面，MTL 材质色，约 1000 三角形 → ~3000 平行四边形 → ~6000 Quad）。

### 运行时 API

```csharp
using TriangleTool;

// 统一构建器（V1/V2/V3 可选）
var builder = new TriangleModelBuilder
{
    Mode = TriangleBuildMode.Hierarchical,  // Exact / StretchClustered / Hierarchical
    Accuracy = 0.001f,                      // V2/V3 拉伸复用容差（世界单位）
    OptimizationPasses = 3,                 // V3 优化扫描轮数
};
builder.EnsureRoot("MyTriangle");
foreach (TriangleData tri in triangles)
    builder.BuildOneTriangle(tri);
builder.Finish();                           // V2: 合并 stretch；V3: 扫描+合并+清理
Debug.Log($"quads={builder.QuadCount}, stretches={builder.StretchCount}, " +
          $"reparented={builder.ReparentedCount}, saved={builder.StretchesSaved}");
builder.Destroy();                          // 清理

// 从 OBJ 加载（自动解析材质/顶点色）
if (ObjModelLoader.TryLoadTriangles("Suzanne", Color.white, false,
        out List<TriangleData> tris, out string name, out string error))
{
    builder.BuildOneTriangle 逐个喂入 ...  // 或配合 ObjBuildSession 分帧构建
}
```

## 项目自身对象

工具生成的所有物体均使用**项目自己的 Block 预制体**（与 `Assets/Tools/CurvesTool` 相同的模式，
经 `PrimitiveComponent.Create` / `EmptyComponent.Create` 实例化，颜色由 `PrimitiveComponent.Update()`
应用项目 Regular/Transparent 材质）：

| 用途 | 预制体 |
|------|--------|
| 根容器 / 不可见父对象（剪切变换载体） | `Assets/Resources/Blocks/Empty.prefab` |
| 可见三角面（Quad 块） | `Assets/Resources/Blocks/Primitives/Quad.prefab` |

> 编辑模式下的 3 个可拖拽点是 **SceneView 手柄**（`Handles.SphereHandleCap`）绘制的，不生成 GameObject；由 `0ToolLib/SceneDragUtility` 提供命中与吸附。

生成的可见 Quad 块 `Collidable = false`（纯渲染，不阻挡）。

## OBJ 解析细节（移植自 TriangleScpSl / ObjParser）

- 支持 `v`（含可选顶点色 r g b）、`mtllib` / `usemtl` / `Kd`（材质漫反射色）、`f`（n-gon 扇形三角化，兼容 `v/vt/vn` 引用）；
- X 轴镜像 `(-x, y, z)` 将 OBJ 右手系转为 Unity 左手系，随后反转面绕序保持法线朝外；
- 三角形颜色优先级：当前材质色 → 三顶点色平均 → 回退色。

## 许可

本工具是 TriangleScpSl（作者 Foibos）V1 算法的移植实现，沿用 **CC-BY-SA 3.0** 许可（见 `LICENSE`）。
源码中的数学部分与注释已按移植来源标注。
