# SOLIDWORKS 当前模型

2026-11-03 按用户指定，当前最新版采用桌面提供的 **2026-10-13 三维模型 ZIP**。原始交付包保存于 [cad/releases](../releases/V型管道检测机器人_3D模型_2026-10-13.zip)；版本选择、50 个文件的映射及 SHA-256 记录在 [CURRENT_MODEL.json](CURRENT_MODEL.json)。文件日期较旧不影响此次版本选择。

## 当前交付物

- [model/V_Pipe_Robot_Concept_ID150.SLDASM](model/V_Pipe_Robot_Concept_ID150.SLDASM)：SOLIDWORKS 2026 原生主装配。
- `model/parts/`：原包中的 38 个独立零件，随装配整体保留。
- [model/V_Pipe_Robot_Concept_ID150.STEP](model/V_Pipe_Robot_Concept_ID150.STEP)：通用中性格式。
- [model/V_Pipe_Robot_Concept_ID150_GA.SLDDRW](model/V_Pipe_Robot_Concept_ID150_GA.SLDDRW)：A3 总布置图。
- `model/overview_robot_only.png`、`model/overview_with_pipe.png`、`model/general_arrangement.png`：该包原有预览。
- `model/MODEL_VALIDATION.txt`、`model/SOLIDWORKS_ASSEMBLY_AUDIT.txt`、`model/HARNESS_PART_MAPPING.txt`：该包原有参数和组件审计记录。
- `model/00_发送老师前请阅读.txt`：原交付说明。
- `../../design/basic_mechanical_scheme.md`、`../../design/assembly_and_procurement.md`：该包配套的机械方案和采购文档。

原包中的 `Native_SOLIDWORKS_2026/`、`Neutral_STEP/`、`Preview/` 和审计文本统一映射到 `model/`；两份设计文档映射到 `design/`。导入过程没有重建或另存 CAD，文件内容与原包完全一致。

## 版本参数与核验范围

原包审计记录为 118 个顶层组件、38 个独立零件，配置 `ID150_NOMINAL`，显示状态 `ROBOT_ONLY` / `WITH_PIPE`。主要包络为 φ60×28 mm 全向轮、150 mm 连杆轴距、25T/40T 齿轮副（32.5 mm 中心距）、92×64×30 mm 设备舱、50×48×32 mm 相机预留舱。

本次核验原 ZIP 与仓库副本一致，并逐项核对 50 个导入文件。上述组件、显示状态和参数沿用包内记录；没有重新执行 SOLIDWORKS 打开、重建、实体干涉或加工验证。模型仍为概念包络，实物尺寸和制造细节应按原包说明后续完善。

## 历史修订与生成工具

包含惰轮、六件打印外壳、127 个组件及 50 个唯一引用零件的版本，保留在 [历史提交 6ad9346](https://github.com/A123536/final-report/tree/6ad93463daccba81145dd6c9fa55dd463d4a2adb/cad/solidworks/model)。其零干涉记录及打印文件只适用于该历史版本。

本目录的 C# 生成器、导出器和验证工具保留原有实现，没有按本次 ZIP 回退。使用它们重新生成或保存模型可能改变当前基线；如需试验，请指定独立输出目录。工具的本地 `.exe` 和 SOLIDWORKS Interop DLL 仍属于忽略的构建产物。
