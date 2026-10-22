# V 型直管检测机器人

本仓库保存一套面向 `140–160 mm` 内径水平直管的有线 V 型检测机器人方案。当前基线覆盖机械架构、二维准静态仿真、概念级三维布置和初步采购规划；尚未完成制造级 CAD、控制固件和物理样机验证。

## 当前方案

- 三只直径 `60 mm` 的全向轮，前后轮主动驱动，中心轮被动压壁。
- 两根轴间距 `150 mm` 的连杆构成 V 型机构，中心关节使用左右镜像扭簧。
- 机械限位为 `90°–125°`，目标整机质量 `1.6 kg`，上限 `1.8 kg`。
- 两台 JGB37-520 编码减速电机通过模数 1 的 25T 电机齿轮、25T 惰轮和 40T 轮轴齿轮驱动，总减速比为 1.6:1。
- 目标巡航速度 `0.15 m/s`，目标管内行程 `10 m`，拖线总长 `15 m`。
- 计划采用地面端 24 V 供电、机器人端 12 V/5 V 降压、STM32F411 控制和 RS-485 通信。

## 仓库结构

```text
cad/
  v_pipe_robot_concept.py       概念级三维包络和图片生成脚本
  outputs/                      GLB、渲染图和模型元数据
  solidworks/                   SOLIDWORKS 2026 原生装配、零件及生成工具
design/
  basic_mechanical_scheme.md    机械方案与第二轮仿真结论
  assembly_and_procurement.md   装配、接线和采购建议
simulation/
  v_pipe_robot_sim.py           几何、压壁、牵引和不确定性计算
  results/                      CSV、JSON 和仿真图表
```

`simulation/results/` 和 `cad/outputs/` 是当前设计基线的一部分，因此纳入版本控制。`.codex_tmp/` 只包含资料提取、中间渲染和临时 Python 依赖，不纳入版本控制。

SOLIDWORKS 主装配体位于 `cad/solidworks/model/V_Pipe_Robot_Concept_ID150.SLDASM`。其 `ID150_NOMINAL` 配置包含 `ROBOT_ONLY` 与 `WITH_PIPE` 两个显示状态；修订模型已完成 140、150、160 mm 三种姿态的实体干涉检查，非预期干涉为零，但仍属于概念包络，不能直接作为加工放行文件。

电子设备外壳已改为六件 FDM/PETG 样机零件：电源舱上盖/底板、控制舱上盖/底板、相机主体/前盖。拼接方案见 `design/printable_enclosure_joint_plan.md`；原生爆炸装配、合盖装配、七页 A3 分件图、PDF、分件 STEP 及已定向的毫米 STL 位于 `cad/solidworks/model/printable_enclosure/`。主装配现有 127 个组件、50 个唯一引用零件。打印前需校准所用机器的定位间隙和所购热熔螺母底孔。

## 环境与运行

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
