# SOLIDWORKS 概念装配

`GenerateSolidWorksConcept.cs` 使用本机 SOLIDWORKS 2026 API 生成原生 `SLDPRT` 和 `SLDASM` 文件。模型严格复用 `cad/v_pipe_robot_concept.py` 的 150 mm 标称管径包络参数。

输出目录 `model/` 包含装配体、零件库和尺寸验证记录。以下对象仍是设计包络而非制造模型：

- φ60×28 mm 全向轮及滚子；
- 扭簧线圈和弹簧腿；
- 模数 1 的 25T 电机齿轮、25T 惰轮和 40T 轮轴齿轮齿形；
- 电机、板卡及安装孔；
- 透明管道采用 2 mm 可视化壁厚。

这些对象必须在实物采购和测量后再替换，不能直接据此加工。

## 当前交付物

- `model/V_Pipe_Robot_Concept_ID150.SLDASM`：主装配体，共124个顶层组件、46个唯一引用零件。
- `model/parts/*.SLDPRT`：独立原生零件和器件包络。
- `model/MODEL_VALIDATION.txt`：关键几何参数检查记录。
- `model/SOLIDWORKS_ASSEMBLY_AUDIT.txt`：组件清单及显示状态审计。
- `model/INTERFERENCE_VALIDATION.md`：140/150/160 mm 实体干涉、管壁包络和关闭提示验证记录。
- `model/REFERENCED_PARTS_ID150.txt`：标称装配实际引用的零件清单。
- `model/overview_with_pipe.png`：透明管道内的装配效果。
- `model/overview_robot_only.png`：隐藏管道后的机器人装配效果。
- `model/V_Pipe_Robot_Concept_ID150_GA.SLDDRW`：A3 总布置工程图，包含 V 型侧向、横向和等轴测视图。
- `model/general_arrangement.png`：总布置工程图预览。

主装配体默认隐藏管道，方便继续编辑内部结构。在特征树中选择 `Pipe_ID150_OD154_L430` 并执行“显示”即可恢复透明管道。

装配体配置名为 `ID150_NOMINAL`，包含两个显示状态：

- `ROBOT_ONLY`：默认状态，隐藏管道并显示完整机器人；
- `WITH_PIPE`：显示透明的 ID150 mm 管道包络。

装配体自定义属性记录了管径范围、轮子包络、150 mm 连杆轴距、108.757667° 标称 V 角、90°–125°机械限位、目标质量、速度和行程。

`GenerateSolidWorksConcept.cs` 是可重复生成 140–160 mm 姿态的参数源；`FinalizeSolidWorksAssembly.cs` 用于设置透明度、导出快照、审计组件并保存最终显示状态；`CreateGeneralArrangementDrawing.cs` 生成 A3 总布置工程图。`AuditSolidWorksInterferences.cs` 以只读方式执行实体干涉检查并主动关闭文档；`CheckSolidWorksCleanClose.cs` 验证普通打开后所有文档的 `SaveFlag` 均保持为假。工具的本地 `.exe` 和 SOLIDWORKS Interop DLL 属于构建产物，不纳入 Git。
