# 0ToolLib —— 运行库

> 存放 `Assets/Tools/` 下各工具**可复用的通用逻辑**（纯数据/数学、通用机制、编辑器交互框架）。
> 工具特定的代码（UI 控件、业务逻辑）留在各自工具目录。
> 迁移计划见 `../MIGRATION_PLAN_0ToolLib_zhCN.md`（内部文档，不提交）。

## 层级约定

| 目录 | 内容 | 约束 |
|---|---|---|
| `Runtime/` | 纯数据/数学/通用机制（如贝塞尔数据模型、`ToolL10n`） | **不引用** `UnityEditor` API、不引用任何工具类型；可被将来 asmdef 化直接作为 Runtime 程序集 |
| `Editor/` | 编辑器功能（SceneView 交互框架、绘制 helper） | 依赖 `UnityEditor`，可引用 `ToolLib.Runtime`；**不得反向引用任何工具** |

- **依赖方向**：工具 → 运行库（单向），运行库绝不引用工具。
- **命名空间**：`ToolLib`（Runtime）/ `ToolLib.Editor`（Editor）。文件夹名为 `0ToolLib`（C# 标识符不能以数字开头，故命名空间用 `ToolLib`）。
  > 历史迁移代码（如从 CurvesTool 移入的 `BezierCurve` 等）当前**保持全局命名空间**以兼容 Unity 场景序列化，命名空间化是独立任务（见迁移计划 §6）。
- **程序集**：当前随项目编译进 `Assembly-CSharp`（项目无 asmdef）；本库暂不建 asmdef。

## 当前内容

### Runtime/
- `ToolL10n.cs` —— 注册表式多语言管理器（各工具以 scope 注册词条表，`T(scope, key)` 查询）
- `BezierCurve.cs`、`BezierCurveData.cs`、`SegmentInfo.cs`、`CurvePlacementHelper.cs` —— 贝塞尔曲线数据模型与段放置计算（自 CurvesTool 迁入，类名/命名空间不变）

### Editor/
- （占位，待 SceneView 交互框架迁入）

## 使用示例

```csharp
// 工具侧注册自己的词条表（scope 唯一）
ToolL10n.Register("curve", dataTable);
// 查询
ToolL10n.T("curve", "window_title");
ToolL10n.T("curve", "gen_success", 10, "Curve1");   // 带参数格式化
ToolL10n.SetLanguage(ToolL10n.Lang.ZH);
```
