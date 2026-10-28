"""Build assembly figures and an 87-item illustrated guide from the saved BOM.

The engineering geometry is read-only tessellation exported from SOLIDWORKS.
New procurement hardware remains explicitly marked as not yet fitted to CAD.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
import subprocess
import textwrap
from xml.sax.saxutils import escape

import numpy as np
from reportlab.graphics.shapes import Drawing, Rect, Circle, Line, Polygon, String
from reportlab.graphics import renderPDF, renderSVG
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, CondPageBreak, Image, KeepTogether, Table, TableStyle

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'cad'/'assembly_guide'
TMP=ROOT/'.codex_tmp'/'assembly_guide_20261028'
RUNTIME=Path('C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies')
POPPLER=RUNTIME/'native/poppler/Library/bin/pdftoppm.exe'
BOM=json.loads((ROOT/'design/procurement_20261028.json').read_text(encoding='utf-8'))
EX=json.loads((ROOT/'design/assembly_item_explanations_20261028.json').read_text(encoding='utf-8'))
SCENE=json.loads((OUT/'native_scene.json').read_text(encoding='utf-8'))
ITEMS={r[0]:r for r in BOM['items']}
assert set(ITEMS)==set(EX) and len(ITEMS)==87
OUT.mkdir(parents=True,exist_ok=True);TMP.mkdir(parents=True,exist_ok=True)
pdfmetrics.registerFont(TTFont('CJK','C:/Windows/Fonts/msyh.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('CJKB','C:/Windows/Fonts/msyhbd.ttc',subfontIndex=0))
pdfmetrics.registerFontFamily('CJK',normal='CJK',bold='CJKB',italic='CJK',boldItalic='CJKB')

INK=colors.HexColor('#18334A');MUTED=colors.HexColor('#536777');BLUE=colors.HexColor('#24709C')
ORANGE=colors.HexColor('#BD6B20');GREEN=colors.HexColor('#3E7E61');PURPLE=colors.HexColor('#805497')
PALE=colors.HexColor('#EDF3F7');BORDER=colors.HexColor('#B8C8D2');RED=colors.HexColor('#A73932')
W,H=1000,620

def wrap(s,max_width,size=17,font='CJK'):
    lines=[];line=''
    for char in s:
        if char in '。，、；：？！）' and line and pdfmetrics.stringWidth(line+char,font,size)>max_width:
            lines.append(line+char);line=''
        elif char=='\n' or pdfmetrics.stringWidth(line+char,font,size)>max_width:
            lines.append(line);line='' if char=='\n' else char
        else:line+=char
    if line:lines.append(line)
    return lines

def label(d,x,y,text,size=17,color=INK,width=None,bold=False,anchor='start'):
    lines=wrap(text,width,size,'CJKB' if bold else 'CJK') if width else text.split('\n')
    for i,line in enumerate(lines):
        d.add(String(x,y-i*(size+5),line,fontName='CJKB' if bold else 'CJK',fontSize=size,fillColor=color,textAnchor=anchor))
    return y-len(lines)*(size+5)

def box(d,x,y,w,h,text,fill=PALE,stroke=BORDER,size=17):
    d.add(Rect(x,y,w,h,rx=6,ry=6,fillColor=fill,strokeColor=stroke,strokeWidth=1.2))
    lines=wrap(text,w-20,size)
    top=y+h/2+(len(lines)-1)*(size+5)/2-size*.33
    for i,line in enumerate(lines):label(d,x+w/2,top-i*(size+5),line,size,anchor='middle')

def arrow(d,points,color=BLUE,dash=False,width=2.2,head=True):
    for a,b in zip(points,points[1:]):
        ln=Line(*a,*b,strokeColor=color,strokeWidth=width)
        if dash:ln.strokeDashArray=[6,4]
        d.add(ln)
    if head:
        a,b=np.array(points[-2],float),np.array(points[-1],float)
        u=(b-a)/np.linalg.norm(b-a);n=np.array([-u[1],u[0]])
        p=b-u*10
        d.add(Polygon([*b,*(p+n*4.5),*(p-n*4.5)],fillColor=color,strokeColor=None))

def header(number,title,sub):
    d=Drawing(W,H)
    d.add(Rect(0,0,W,H,fillColor=colors.white,strokeColor=None))
    label(d,25,H-36,f'{number:02d}  {title}',25,bold=True)
    label(d,25,H-65,sub,15,MUTED)
    return d

def note(d,text):label(d,25,22,text,14,MUTED,width=950)

def role(part):
    if 'Bay_' in part:return 'shell'
    if 'CameraHousing' in part:return 'camera'
    if 'Gear25' in part or 'Gear40' in part:return 'gear'
    if 'Spring' in part:return 'spring'
    if 'HARNESS' in part:return 'wire'
    if any(t in part for t in ['STM32','MC33926','RS485','Buck','FuseTerminal']):return 'pcb'
    if 'Pipe_ID' in part:return 'pipe'
    return 'metal'

def mesh_color(part):
    return {'shell':(0.63,0.78,0.88),'camera':(0.75,0.64,0.83),'gear':(0.86,0.55,0.25),
            'spring':(.58,.37,.68),'wire':(.30,.55,.39),'pcb':(.18,.47,.61),'metal':(.34,.40,.44),'pipe':(.9,.95,.98)}[role(part)]

def iso(d,components,bounds,eye=(.7,-1,.70),callouts=None):
    target=np.array([25,0,0],float)
    eye=np.array(eye,float);eye/=np.linalg.norm(eye)
    right=np.cross(-eye,[0,0,1]);right/=np.linalg.norm(right)
    up=np.cross(right,-eye)
    triangles=[];coords=[]
    for c in components:
        part=c['part']
        if role(part)=='pipe':continue
        p=np.array(SCENE['geometries'][part],float).reshape(-1,3)
        t=np.array(c['transform'],float)
        world=p@t[:9].reshape(3,3)+t[9:12]
        proj=np.column_stack(((world-target)@right,(world-target)@up))
        coords.append(proj)
        base=np.array(mesh_color(part));light=np.array([.3,-.4,.85]);light/=np.linalg.norm(light)
        for i in range(0,len(world),3):
            tri=world[i:i+3]
            normal=np.cross(tri[1]-tri[0],tri[2]-tri[0]);length=np.linalg.norm(normal)
            if length<1e-10:continue
            shade=.55+.45*abs(np.dot(normal/length,light))
            color=colors.Color(*(np.clip(base*shade,0,1)))
            triangles.append((float(tri.mean(axis=0)@eye),proj[i:i+3],color,part))
    allp=np.concatenate(coords);lo=allp.min(axis=0);hi=allp.max(axis=0)
    x,y,w,h=bounds;scale=min(w/(hi[0]-lo[0]),h/(hi[1]-lo[1]))
    origin=np.array([x+w/2,y+h/2])-(lo+hi)/2*scale
    for depth,tri,col,part in sorted(triangles,key=lambda r:r[0]):
        q=tri*scale+origin
        poly=Polygon(q.reshape(-1).tolist(),fillColor=col,strokeColor=None)
        if role(part) in ['shell','camera']:poly.fillOpacity=.35
        d.add(poly)
    def projected(point):
        a=np.array(point)-target
        return np.array([a@right,a@up])*scale+origin
    if callouts:
        for point,text,tx,ty in callouts:
            p=projected(point)
            arrow(d,[(tx,ty-8),(tx,ty-20),(p[0],p[1])],MUTED,width=1.2,head=False)
            d.add(Circle(float(p[0]),float(p[1]),3,fillColor=BLUE,strokeColor=None))
            label(d,tx,ty,text,16,bold=True)

def figure01():
    d=header(1,'整机模型与装配分区','原生装配显示网格 / X为前进方向，Y为轮轴方向，Z为竖直方向；非制造孔位图')
    callouts=[([-122,0,-43],'后主动轮 M03',40,440),([0,0,44],'中心轮 + S01扭簧',360,500),
              ([122,0,-43],'前主动轮 M03',755,125),([-72,0,33],'后电源舱 P04',55,180),
              ([72,0,33],'前控制舱 E01 / E02 / E05',560,445),([175,0,0],'旧相机舱：V01/V02待实测改壳',635,78)]
    iso(d,SCENE['components'],(115,150,760,310),callouts=callouts)
    label(d,25,523,'管内：三轮、金属骨架、控制/供电、相机；管外：电脑、24V电源、急停、下载器。',16)
    note(d,'形状含轮子/电机/板卡包络。新增Luckfox、实际5V降压、夹持座和三路拖线入口尚未在当前CAD中定版。')
    return d

def figure02():
    d=header(2,'一组驱动如何把转矩传给轮子','前后各一组；图中圆圈是节圆示意，不是可加工齿形')
    scale=6;origin=155;cy=365
    centers=[0,25,57.5];radii=[12.5,12.5,20]
    for i,(cx,r) in enumerate(zip(centers,radii)):
        x=origin+cx*scale
        d.add(Circle(x,cy,r*scale,fillColor=colors.HexColor('#F7E4CF'),strokeColor=ORANGE,strokeWidth=2))
        d.add(Circle(x,cy,11,fillColor=colors.white,strokeColor=INK,strokeWidth=1.5))
        label(d,x,cy+25,['25T','25T','40T'][i],21,bold=True,anchor='middle')
        label(d,x,cy-r*scale-28,['M05\n电机齿轮','M06\n惰轮','M07\n轮轴齿轮'][i],17,anchor='middle')
        label(d,x,cy+r*scale+30,['顺时针','逆时针','顺时针'][i],16,MUTED,anchor='middle')
    arrow(d,[(155,cy+140),(305,cy+140)],MUTED,width=1.2,head=False);label(d,230,cy+152,'25.0 mm',16,anchor='middle')
    arrow(d,[(305,cy+140),(500,cy+140)],MUTED,width=1.2,head=False);label(d,403,cy+152,'32.5 mm',16,anchor='middle')
    box(d,690,350,270,115,'M01 电机 + 内置减速箱\n→ 25T → 25T → 40T')
    label(d,690,315,'总减速比 40 / 25 = 1.6',20,bold=True)
    label(d,690,280,'两次外啮合后，轮与电机同向。',16,width=270)
    box(d,55,90,890,100,'轮轴支撑关系： S03座［M08轴承］— M04轮毂 / M03轮子 / M07齿轮 —［M08轴承］S03座\nM10轴承受并传递载荷；压壁力不直接压到电机输出轴。',size=18)
    note(d,'惰轮轴/轴承/止动结构和轴向尺寸链仍待制造图；不把“φ6×14包络”当成已核实的最终轴长。')
    return d

def figure03():
    d=header(3,'六件电子外壳如何拼起来','采购编号E01是主控板；外壳图号E-01是电源舱上盖，二者不同')
    names=['PowerBay_TopCover','PowerBay_BasePlate','ControlBay_TopCover','ControlBay_BasePlate','CameraHousing_Body','CameraHousing_FrontBezel']
    comps=[]
    for i,prefix in enumerate(names):
        part=next(k for k in SCENE['geometries'] if k.startswith(prefix))
        xyz=[(-100,0,23),(-100,0,-23),(0,0,23),(0,0,-23),(100,0,0),(140,0,0)][i]
        comps.append({'part':part,'transform':[1,0,0,0,1,0,0,0,1,*xyz,1,0,0,0]})
    iso(d,comps,(90,180,810,325),eye=(.65,-1,-.8))
    label(d,25,520,'E-01/E-02 电源舱：4组M3      E-03/E-04 控制舱：4组M3      E-05/E-06 相机舱：4组M2.5',16)
    box(d,50,62,900,100,'拼接顺序：试孔 → 装热熔螺母并冷却 → 固定/绝缘PCB → 安排过线 → 挡舌定位 → 对角轻拧螺钉\nF02/F03：设备舱低圆头M3，头径≤5.7、头高≤1.7mm；F04/F05：相机盖M2.5。',size=18)
    note(d,'设备舱挡舌单边0.30mm，相机挡舌0.50mm，仅为试配起点。E-05/E-06须随新视觉硬件改图。')
    return d

def figure04():
    d=header(4,'V形机构、弹簧与装管锁销','此图是运动/受力示意；不显示未完成的轴承座、簧腿孔及加工细节')
    for y in [160,445]:d.add(Line(75,y,925,y,strokeColor=BORDER,strokeWidth=2))
    A=(245,215);B=(500,390);C=(755,215)
    d.add(Line(*A,*B,strokeColor=INK,strokeWidth=12));d.add(Line(*B,*C,strokeColor=INK,strokeWidth=12))
    for p in [A,B,C]:d.add(Circle(*p,55,fillColor=colors.white,strokeColor=BLUE,strokeWidth=3));d.add(Circle(*p,8,fillColor=INK,strokeColor=None))
    for dy in [-14,14]:d.add(Circle(B[0]+dy,B[1],23,fillColor=None,strokeColor=PURPLE,strokeWidth=3))
    arrow(d,[(500,446),(500,478)],PURPLE,width=3)
    arrow(d,[(245,160),(245,127)],BLUE,width=3);arrow(d,[(755,160),(755,127)],BLUE,width=3)
    label(d,88,465,'管壁',17);label(d,650,450,'中心轮压壁力约50–53N',18,bold=True)
    label(d,125,315,'后主动轮 M03',18);label(d,710,315,'前主动轮 M03',18)
    label(d,500,245,'S06 铰链 / S01 镜像扭簧',18,anchor='middle')
    label(d,155,370,'S02 连杆',17);arrow(d,[(245,362),(365,298)],MUTED,width=1.1,head=False)
    label(d,755,370,'S02 连杆',17);arrow(d,[(745,362),(635,298)],MUTED,width=1.1,head=False)
    label(d,280,83,'S07限制90°–125°；S08在专用耳孔中锁定装管姿态。',19)
    note(d,'先装限位、检查空载转动，再用工装预紧。不能把中心轮自转和两根连杆开合同时锁死。')
    return d

def figure05():
    d=header(5,'供电树：24V、12V、5V不能混接','橙线为24/12V功率路径，蓝线为5V；各电源需正负成对连接，箭头仅表示功能路径')
    box(d,25,410,165,95,'P01 管外电源\n24V / 5A')
    box(d,230,410,155,95,'P06总保险\nP10急停')
    box(d,435,410,200,95,'C01 两芯拖线\n24V正 / 返回线')
    box(d,705,410,260,95,'P04 降压\n24V → 12V')
    arrow(d,[(190,457),(230,457)],ORANGE);arrow(d,[(385,457),(435,457)],ORANGE);arrow(d,[(635,457),(705,457)],ORANGE)
    box(d,705,260,260,80,'P07保险 → E02驱动')
    box(d,705,120,260,80,'M01 两台12V电机')
    arrow(d,[(835,410),(835,340)],ORANGE);arrow(d,[(835,260),(835,200)],ORANGE)
    box(d,430,260,200,80,'P05 12V → 5V')
    arrow(d,[(720,410),(720,375),(530,375),(530,340)],ORANGE)
    box(d,405,100,250,95,'P08逻辑保险 / 分配\nE01主控 / 编码器 / V01视觉')
    arrow(d,[(530,260),(530,195)],BLUE)
    box(d,35,120,300,175,'P11 电解：接驱动VIN与GND\nP12 100nF：跨接电机两端\nOUT1/OUT2：两根都是电机输出，不能直接接系统地。',size=17)
    label(d,35,340,'机器人内部的非隔离逻辑/功率模块需有正确的公共参考地。',17,width=330)
    label(d,35,525,'管外',16,bold=True);label(d,705,525,'管内',16,bold=True)
    note(d,'按“先空载测电压，再逐支路带负载”的顺序调试；急停切24V后图传也可能中断，人工回收用独立绳。')
    return d

def figure06():
    d=header(6,'运动控制、反馈与RS485','逻辑端口已按器件功能标注；F411具体GPIO和六芯电机线序尚未冻结')
    box(d,25,400,170,100,'电脑\n控制/状态')
    box(d,245,400,180,100,'E06隔离\nUSB–RS485')
    box(d,485,400,170,100,'E05 SP3485\n3.3V收发器')
    box(d,740,400,225,100,'E01 STM32F411\n速度控制 / 保护')
    arrow(d,[(195,450),(245,450)],BLUE);arrow(d,[(425,450),(485,450)],BLUE)
    label(d,430,475,'C03 A/B+参考',13)
    arrow(d,[(655,450),(740,450)],BLUE);label(d,657,475,'UART / DE',13)
    box(d,720,195,245,120,'E02 MC33926\nIN1/IN2、D2 PWM、EN\nFB→ADC；SF→故障输入')
    arrow(d,[(805,400),(805,315)],BLUE)
    arrow(d,[(915,315),(915,400)],MUTED)
    label(d,925,350,'FB/SF',13)
    box(d,360,200,265,110,'M01 两台电机\n每台：功率两线 + 编码器4线')
    arrow(d,[(720,250),(625,250)],ORANGE)
    box(d,360,60,265,80,'E07 编码器电平缓冲\nA1/B1/A2/B2 → 3.3V')
    arrow(d,[(490,200),(490,140)],GREEN)
    arrow(d,[(625,100),(990,100),(990,540),(850,540),(850,500)],GREEN)
    box(d,25,100,265,185,'采用D2调速时：D1置低。\nEN启动时保持低。\nE09：120Ω仅总线两端。\nE03/E04：SWD仅调试时连接。',size=17)
    note(d,'E06参考线使用隔离侧SGND/COM（如有），不跨接USB地。电机编码器电平与模块DIR/OE设置要实测确认。')
    return d

def figure07():
    d=header(7,'视频不经过STM32：相机、图传板、电脑','主购SC3336(B)+Luckfox；画出的板形和端口用于理解，不是安装孔位或实物尺寸图')
    box(d,25,325,205,180,'')
    d.add(Circle(127,458,25,fillColor=colors.white,strokeColor=INK,strokeWidth=2))
    label(d,127,407,'V02 SC3336(B)',17,anchor='middle')
    label(d,127,378,'手调焦 / 图像传感器',15,anchor='middle')
    label(d,127,348,'镜头朝机器人前方',16,anchor='middle')
    box(d,330,325,255,180,'V01 Luckfox Pico Max\nCSI输入 → 视频编码\n100M以太网输出')
    box(d,720,325,250,180,'电脑 / 可选V05网卡\n实时看图 / 拍照 / 录像\n后续识别程序读视频流')
    arrow(d,[(230,405),(330,405)],GREEN);label(d,245,435,'短CSI排线',15)
    arrow(d,[(585,405),(720,405)],GREEN);label(d,593,435,'C02 15m网线',15)
    box(d,330,145,255,90,'P05 / P08\n规定5V供电入口')
    arrow(d,[(455,235),(455,325)],BLUE)
    box(d,25,95,225,135,'V07白光灯环\n12V恒压、自带限流\nV08 MOS开关控制')
    arrow(d,[(140,230),(140,300)],ORANGE)
    box(d,680,95,290,145,'新视觉舱需包含：\n图传板 / RJ45弯线 / 镜头 / 灯环\n按实测重画E-05/E-06。\n旧50×48×32mm舱仅为预留包络。',size=17)
    note(d,'初始目标1080p、25/30fps不等于实测结果。FFC断电插拔；不能把5V或24V接进3V3、CSI或RJ45。')
    return d

def figure08():
    d=header(8,'拖线束：四种不同任务，不混成一根线接','回收绳承担回收拉力；各线按实际直径过壳，原单φ7/M12入口需更新')
    for i,(title,sub,col)in enumerate([
        ('C01 两芯功率线','24V正 / 功率返回 → P04',ORANGE),
        ('C02 两对以太网线','RJ45 1/2、3/6 → V01网络口',GREEN),
        ('C03 RS485屏蔽线','A/B + 参考 → E05收发器',BLUE),
        ('C04 独立回收绳','金属机架受力点，不接PCB或插头',PURPLE)]):
        y=455-i*100
        box(d,30,y-28,260,76,title,size=18)
        arrow(d,[(290,y+10),(650,y+10)],col,width=5,head=False)
        box(d,650,y-28,320,76,sub,size=17)
    box(d,90,40,820,78,'C05接头、C06格兰头、C07护套、C10固定/标号共同组成线束。\n新增视频线后，称完整1m线束；超0.12kg/m需重算牵引。',size=18)
    note(d,'C08/C09主要是机器人内部短线与信号接插件。地面急停、回收绳与线束应力释放需在进管前验证。')
    return d

FIGURES=[figure01,figure02,figure03,figure04,figure05,figure06,figure07,figure08]
FIGURE_NAMES=['overall-model','drive-train','enclosure-exploded','v-linkage','power-tree','control-rs485','vision-path','tether-bundle']
for i,(fn,name) in enumerate(zip(FIGURES,FIGURE_NAMES),1):
    drawing=fn()
    renderSVG.drawToFile(drawing,str(OUT/f'{i:02d}-{name}.svg'))
    pdf=TMP/f'{i:02d}-{name}.pdf';renderPDF.drawToFile(drawing,str(pdf))
    subprocess.run([str(POPPLER),'-r','130','-singlefile','-png',str(pdf),str(OUT/f'{i:02d}-{name}')],check=True,stdout=subprocess.DEVNULL)

styles={
    'title':ParagraphStyle('title',fontName='CJKB',fontSize=19,leading=26,textColor=INK,spaceAfter=12,wordWrap='CJK',keepWithNext=True),
    'h':ParagraphStyle('h',fontName='CJKB',fontSize=13,leading=19,textColor=BLUE,spaceAfter=7,spaceBefore=6,wordWrap='CJK'),
    'body':ParagraphStyle('body',fontName='CJK',fontSize=10.5,leading=16,textColor=INK,spaceAfter=5,wordWrap='CJK'),
    'small':ParagraphStyle('small',fontName='CJK',fontSize=9,leading=14,textColor=MUTED,spaceAfter=5,wordWrap='CJK')
}
def para(text,kind='body'):return Paragraph(escape(text),styles[kind])
story=[]
for i,name in enumerate(FIGURE_NAMES,1):
    story.append(Image(str(OUT/f'{i:02d}-{name}.png'),width=770,height=477.4))
    story.append(PageBreak())
story += [para('09  建议装配与首次上电顺序','title')]
steps=[
('1. 先核对实物','按采购编号清点。用T02测板卡、轴伸、轮毂孔型和接头；对“先确认/图纸后加工”项目不把概念包络当实物尺寸。'),
('2. 完成金属支撑','先出轮轴/轴承座/电机座/铰链制造图，空载试装。三根轴转动顺畅且不窜动；两组齿轮中心距及齿宽对齐。'),
('3. 先做限位，再装扭簧','验证90°/125°止挡和锁销工况，再在工装中按厂家曲线设置预紧。中心轮自转与连杆开合要分别保持自由。'),
('4. 外壳先试配','先试打孔和挡舌，装嵌件并冷却，再用绝缘柱安装板卡。相机壳按V01/V02、灯环、插头和弯线实测改图。'),
('5. 断电查线','核对24V/12V/5V/3.3V端口、正负、公共参考与接插件标记。检查没有短路、金属碎屑、夹线或侵入齿轮的线。'),
('6. 分支上电','先不接电机，空载测降压输出；再单独接主控和视觉。外部供电与USB同时连接前核对板卡电源路径。'),
('7. 单电机台架','轮/传动卸载后，验证EN启动为低、方向、PWM、编码器计数、FB电流与SF状态。保护阈值按新电机实测，不复用旧2.4A假设。'),
('8. 独立调视频','先V06短网线+厂家系统，调焦和白光；能看图/录像后换C02长拖线，测延迟、掉帧与反光。'),
('9. 合盖与低速试验','确认线束不碰轴/轮/电机/齿轮。检查急停、故障切断与回收绳后，再由1m三种内径短管进入10m试验。'),
('10. 记录并复核','称整机与完整线束；新增视觉硬件或质量超原范围后更新CAD/牵引计算。本图解不是新增硬件已通过干涉或强度验证的声明。')
]
for head,text in steps:story += [para(head,'h'),para(text)]
story.append(PageBreak())
story += [para('10  功能端口对照：接线前按丝印核对','title'),
          para('以下是功能连接关系，不是已经冻结的MCU GPIO表。不要按任意插头的颜色判断针脚。')]
ports=[
('P04输入','C01机器人端24V正 / 返回线','只接降压允许的VIN/GND，不接任何逻辑脚。'),
('P04输出','12V母线→P07/E02与P05','正母线分开标记；不能与24V正极短接。'),
('P05输出','5V→P08→各板允许供电入口','E05/E07使用3.3V；5V不能接进3V3脚。'),
('E02逻辑','每路IN1/IN2、D2/PWM；共用EN','D2调速时D1为低；EN启动保持低。'),
    ('E02反馈','每路FB→ADC，SF→故障监控','FB是模拟电压；SF低有效，但输出禁用也可为低，须结合使能状态判读。'),
('E02功率','每路OUT1/OUT2→对应电机两线','两线都由H桥驱动；不将某一端固定接系统GND。'),
('电机编码器','供电/地/A/B→规定逻辑供电、E07、主控','确认输出类型/电压、线序与编码器倍率。'),
('E05 UART','主控TX→DI，RO→主控RX，DE/RE控制方向','保持正确参考地，末位发完后才切回接收。'),
('RS485长线','A/B与参考线→E06隔离侧','端接电阻仅物理两端；A/B极性按两厂家定义核对。'),
('相机与图传','V02 CSI→V01；V01以太网→电脑','FFC方向以厂家图为准；不把网线当24V电源。'),
('补光灯','12V自带限流灯板→V08开关','光路、散热与3.3V触发方式都要验证。'),
('SWD','SWDIO/SWCLK/GND/VTref，可选NRST','VTref是目标电平参考，不作为整机电源。')
]
table_data=[[para('功能端口','small'),para('接到哪里','small'),para('核对要点','small')]]+[[para(a,'small'),para(b,'small'),para(c,'small')]for a,b,c in ports]
table=Table(table_data,colWidths=[130,285,350],repeatRows=1,hAlign='LEFT')
table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),PALE),('VALIGN',(0,0),(-1,-1),'TOP'),('BOTTOMPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),('LINEBELOW',(0,0),(-1,0),.8,BORDER)]))
story.append(table);story.append(PageBreak())

group_fig={'电控':'图06','动力传动':'图01/02','结构弹簧':'图01/04','供电保护':'图05','视觉图传':'图07','拖线线束':'图08','打印紧固':'图03','调试工具':'第09/10节','实验材料':'第09节'}
last=None
for row in BOM['items']:
    ident,group,name,model,need,buy,unit,stage,spec,purchase_note,source=row
    if group!=last:
        story.append(CondPageBreak(190))
        story += [para(f'逐项说明  {group}（{group_fig[group]}）','title')]
        last=group
    role_text,location,connect,assembly=EX[ident]
    block=[para(f'{ident}  {name}','h'),para(f'型号/采购规格：{model}；装机{need}{unit}，建议采购{buy}{unit}；阶段：{stage}。','small'),
           para('作用：'+role_text),para('位置：'+location+'  连接：'+connect),para('装配要点：'+assembly)]
    story.append(KeepTogether(block));story.append(Spacer(1,7))

story += [PageBreak(),para('资料与当前版本边界','title'),
          para('采购数据：2026-10-28的87项清单。空间模型：当前Rev C原生装配的显示网格，50种零件、127个组件。只读导出前后SaveFlag均为False，未修改或覆盖原生CAD。'),
          para('没有在旧CAD里假装安装新Luckfox板、实际灯环、5V降压、机架夹持座和三路拖线入口。它们的功能位置和线路见图05–08，安装尺寸仍需实测与后续CAD复核。'),
          para('MC33926的引脚默认状态、3.3V逻辑和电流反馈依据厂家资料。GPIO、六芯电机线序、焊点、实际载板和保护参数仍需最终接线图与台架测试。')]
for key in ['driver','f411','motor','485','usb485','level','vision','camera','csi','buck12','buck5','power24','insert']:
    name,url=BOM['sources'][key]
    story += [para(name,'h'),para(url,'small')]

pdf_path=OUT/'V型管道机器人_87项器材讲解与装配图解_2026-10-28.pdf'
def page_decor(canvas,doc):
    canvas.setFont('CJK',8);canvas.setFillColor(MUTED)
    canvas.drawString(34,18,'V型管道机器人 / 采购编号图解 / 2026-10-28 / 功能装配说明，非制造放行图')
    canvas.drawRightString(806,18,f'{doc.page}')
doc=SimpleDocTemplate(str(pdf_path),pagesize=landscape(A4),leftMargin=34,rightMargin=34,topMargin=25,bottomMargin=34,
    title='V型管道机器人：87项器材讲解与装配图解',author='Codex')
doc.build(story,onFirstPage=page_decor,onLaterPages=page_decor)

markdown=['# 87项器材讲解与装配图解','', '本说明与2026-10-28采购清单使用相同编号。采购E01是主控板；外壳E-01是电源舱上盖。', '',
          '原生模型未修改。新增视觉硬件、5V降压、机架夹持和三路拖线入口仍需实测与制造级设计。', '']
for i,name in enumerate(FIGURE_NAMES,1):markdown += [f'![图{i:02d}](../cad/assembly_guide/{i:02d}-{name}.png)','']
last=None
for row in BOM['items']:
    ident,group,name,model,need,buy,unit,stage,spec,purchase_note,source=row
    if group!=last:markdown += ['## '+group,''];last=group
    role_text,location,connect,assembly=EX[ident]
    markdown += [f'### {ident} {name}', '', f'型号：{model}。装机{need}{unit}，建议采购{buy}{unit}；{stage}。', '',
        '- 作用：'+role_text,'- 位置：'+location,'- 连接：'+connect,'- 装配要点：'+assembly,'']
markdown += ['## 阅读顺序','', '先看图01分区，再看图02/04机械、图03外壳、图05/06电路、图07视觉和图08拖线；按PDF第09节的顺序逐步装配和上电。','']
(ROOT/'design/assembly_guide_20261028.md').write_text('\n'.join(markdown),encoding='utf-8')
print('PDF='+str(pdf_path));print('ITEMS_EXPLAINED=87 FIGURES=8')
