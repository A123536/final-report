# 六件式 PETG 外壳

先查看 `Printable_Enclosure_Split_A3.pdf`，它是7页A3图纸：第1页总图，第2–7页依次为E-01至E-06的分件视图、尺寸与孔位说明。

## 打开和打印

- SOLIDWORKS原生拆分图：`Printable_Enclosure_Split_A3.SLDDRW`。
- 爆炸摆放装配：`Printable_Enclosure_Exploded_ID150.SLDASM`。
- 实际合盖装配：`Printable_Enclosure_Assembled_ID150.SLDASM`。
- `STL_mm/`：六个已摆放打印方向的毫米STL。E-01/E-03高22，E-02/E-04/E-06高4.4，E-05高50 mm。
- `STEP_parts/`：六个保留设计坐标的STEP，用于后续尺寸调整。
- 原生零件在上一级 `parts/`，打开装配时请保持整个model目录结构。

电源舱、控制舱各用4只M3×10低圆头螺钉和M3热熔螺母；摄像头前盖用4只M2.5×8螺钉和M2.5热熔螺母。M3螺钉头必须满足直径≤5.7、高度≤1.7 mm，普通高圆柱头会碰编码器。

PETG基线：0.4 mm喷嘴、0.20 mm层高、4–5道外墙、6层顶底、30–40%填充。盖体顶面朝下，底板平放，摄像头主体后壁朝下。外挑螺柱肩部及摄像头过线桥需检查局部支撑；取件后清理首层象脚和孔内残留。

设备舱定位挡舌单边间隙0.30 mm，摄像头0.50 mm。暂定M3热熔底孔φ4.2、M2.5底孔φ4.0 mm，必须先按所购螺母和实际打印机试打校准。完整中文设计与装配顺序见仓库 `design/printable_enclosure_joint_plan.md`；交付压缩包根目录另附该说明。

## 已验证与待验证

六件原生模型均为单个有效实体，STL均为单个封闭、法向一致的网格，单位和打印朝向正确。合盖装配无实体干涉；140/150/160 mm整机姿态均无新增外壳干涉，12只螺钉的头、杆包络也均无碰撞。

本轮是外壳拼接样机方案。板卡实测安装孔、舱体到机架的夹持、温升与密封仍需结合实物完成；尚未进行打印实物的拔出强度、振动、耐热及管内行走试验。

## 验证记录

- `PRINT_PART_GEOMETRY_AUDIT.txt`：六件实体及M3孔道检查。
- `STL_VALIDATION.json`：六个STL封闭性、连通性、尺度、平台方向。
- `INTERFERENCE_ID140/150/160.txt`：三种姿态各23项已分类的概念连接，非预期干涉为0。
- `INTERFERENCE_CLOSED_ENCLOSURE.txt`：实际对合的外壳装配，0项干涉。
- `FASTENER_CLEARANCE_ID140/150/160.txt`：12只螺钉包络，0项碰撞。
- `CLEAN_CLOSE_*.txt`：主装配、两个外壳装配、两份图纸的引用和保存标志检查。
