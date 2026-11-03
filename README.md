# V 型直管检测机器人

本仓库保存一套面向 `140–160 mm` 内径水平直管的有线 V 型检测机器人方案。当前基线覆盖机械架构、二维准静态仿真、概念级三维布置和初步采购规划；尚未完成制造级 CAD、控制固件和物理样机验证。

## 当前采用版本

2026-11-03 按用户指定，将 **2026-10-13 的三维模型交付包**设为当前最新版。这里的“最新版”指当前选定基线，不按文件日期排序。

- [原始完整 ZIP](cad/releases/V型管道检测机器人_3D模型_2026-10-13.zip)：与桌面提供的压缩包逐字节一致。
- [SOLIDWORKS 主装配](cad/solidworks/model/V_Pipe_Robot_Concept_ID150.SLDASM)：118 个顶层组件、38 个独立零件；零件位于同级 `parts/`。
- [通用 STEP 模型](cad/solidworks/model/V_Pipe_Robot_Concept_ID150.STEP)及 [A3 总布置图](cad/solidworks/model/V_Pipe_Robot_Concept_ID150_GA.SLDDRW)。
- [版本与文件校验清单](cad/solidworks/CURRENT_MODEL.json)：记录原包 SHA-256、50 个文件的对应路径和校验值。

主模型、零件、预览图、原有验证记录，以及 `design/basic_mechanical_scheme.md`、`design/assembly_and_procurement.md` 均取自该包。118 个组件和显示状态来自包内原有审计记录，本次只核验文件完整性，没有重新执行 SOLIDWORKS 重建或干涉检查。

10 月 28 日的可打印外壳、87 项采购清单及装配图解属于历史修订，不代表本次选定模型。此前完整版本保留在 [提交 6ad9346](https://github.com/A123536/final-report/tree/6ad93463daccba81145dd6c9fa55dd463d4a2adb)，后续如需重新采用可从历史恢复。

## 当前方案

- 三只直径 `60 mm` 的全向轮，前后轮主动驱动，中心轮被动压壁。
- 两根轴间距 `150 mm` 的连杆构成 V 型机构，中心关节使用左右镜像扭簧。
- 机械限位为 `90°–125°`，目标整机质量 `1.6 kg`，上限 `1.8 kg`。
- 两台 JGB37-520 编码减速电机通过模数 1 的 25T/40T 齿轮副驱动，理论中心距 `32.5 mm`，减速比为 1.6:1。
- 目标巡航速度 `0.15 m/s`，目标管内行程 `10 m`，拖线总长 `15 m`。
- 计划采用地面端 24 V 供电、机器人端 12 V/5 V 降压、STM32F411 控制和 RS-485 通信。

## 仓库结构

```text
cad/
  v_pipe_robot_concept.py       概念级三维包络和图片生成脚本
  outputs/                      GLB、渲染图和模型元数据
  solidworks/                   SOLIDWORKS 2026 原生装配、零件及生成工具
  releases/                     用户选定的原始模型交付 ZIP
design/
  basic_mechanical_scheme.md    机械方案与第二轮仿真结论
  assembly_and_procurement.md   装配、接线和采购建议
simulation/
  v_pipe_robot_sim.py           几何、压壁、牵引和不确定性计算
  results/                      CSV、JSON 和仿真图表
```

`simulation/results/` 和 `cad/outputs/` 保留已有仿真、GLB 和渲染结果；本次没有按归档模型重新生成这些派生文件，当前三维外形以 `cad/solidworks/model/` 中的原生文件、STEP 和预览为准。`.codex_tmp/` 存放临时文件及切换前的本地模型备份，不纳入版本控制。

SOLIDWORKS 主装配体位于 `cad/solidworks/model/V_Pipe_Robot_Concept_ID150.SLDASM`。包内记录其 `ID150_NOMINAL` 配置包含 `ROBOT_ONLY` 与 `WITH_PIPE` 两个显示状态。当前版采用 `92 × 64 × 30 mm` 设备舱包络和 `50 × 48 × 32 mm` 相机预留包络，属于概念模型。

历史六件 FDM/PETG 外壳及其三种管径干涉检查结果属于后续修订，未包含在当前选定 ZIP 中；相关历史方案见 `design/printable_enclosure_joint_plan.md`，原生模型和打印文件可从上述历史提交获取。

历史 2026-10-28 采购清单对应的 87 项器材说明见 `design/assembly_guide_20261028.md`；`cad/assembly_guide/` 保留当时的 8 张模型/连接图和 PDF，它们基于后续修订模型，不能当作当前版的装配依据。

## 环境与运行

以下脚本保留仓库已有实现，并未随归档模型回退；运行生成器不能保证复现当前 ZIP。查看当前版请直接使用上方交付文件。

建议使用 Python 3.12 创建独立虚拟环境：

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

重新生成二维仿真结果：

```powershell
python simulation\v_pipe_robot_sim.py
```

重新生成概念级三维模型和渲染图：

```powershell
python cad\v_pipe_robot_concept.py
```

两个脚本会覆盖各自输出目录中的已有结果。运行前如需保留历史结果，请先提交或复制现有文件。

## 当前验证边界

现有结果来自二维对称准静态模型和概念包络，不等同于制造可行性或样机验证。下一阶段仍需完成：

1. 实测全向轮、轮毂、电机、齿轮和轴承尺寸。
2. 完成中心铰链、扭簧、锁销、限位和轴承座的制造级 CAD。
3. 标定扭簧角度－转矩曲线和真实轮管摩擦系数。
4. 开发双电机编码器闭环、限流和 RS-485 通信程序。
5. 在 140、150、160 mm 管径及不锈钢/PVC 管道中完成 10 m 拖线试验。

详细参数、计算假设和采购顺序以 `design/` 下的两份设计文档为准。
