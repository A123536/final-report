# V型管道机器人 CubeMX整机配置清单 v1.1

初版日期为2026-11-03，更新日期为2026-11-05。

配置目标为STM32F411CEU6 / WeAct MiniF4 V3.1、双MC33926、双AB编码电机、四路模拟采样、RS-485、补光、关节测角和FreeRTOS。本文记录目标配置与当前生成工程的复查状态，控制参数、采样窗口和保护阈值需通过整机实测确定。

本清单是[当前机械模型基线](../README.md)的电控设计增补。关节测角的机械安装与验证要求见[中心关节角度传感器方案](center_joint_angle_sensor.md)。供电与视觉组合采用后续电控规划，其实际板卡和线束需要在所选机械模型中完成安装设计。

开始配置前，为现有IOC和工程保存可追溯副本。当前复查对象是本地生成的Final_project5工程，CubeMX版本6.18.1、固件包STM32Cube FW_F4 V1.28.3，接口采用CMSIS-RTOS V2。

| 配置项 | 目标与2026-11-05复查状态 |
|---|---|
| 电机控制GPIO、ADC连续DMA、编码器TI12及滤波上拉 | 当前生成配置已具备 |
| 六任务优先级与栈、32KB堆及异常检测宏 | 当前生成配置已具备，运行时栈/堆仍需验收 |
| TIM1/TIM11共享中断 | 当前入口仍派发htim1，需按第8节消除启动期未初始化句柄访问 |
| SPI2及PB12片选 | 引脚及初始电平已具备，当前25MHz/Mode 0/8位需按最终传感器调整 |
| 栈溢出和内存分配失败Hook | 函数已生成，异常处理逻辑待实现 |
| 外设启动、任务业务与IWDG健康刷新 | 待实现，启动看门狗后需要及时建立健康刷新流程 |
| 软件构建 | 当前生成工程Debug构建通过，硬件启动及波形待实测 |

## 1. 模块边界与必要硬件

| 模块 | 承担功能 | CubeMX设置 |
|---|---|---|
| STM32F411 | 实时运动、采样、安全、通信、状态管理 | 本文全部配置 |
| 双MC33926 #1213 | 两台有刷电机的H桥驱动、电流反馈、状态输出 | TIM1、方向/EN、ADC、GPIO |
| 两台AB编码电机 | 运动及转速反馈 | TIM2、TIM4 |
| SP3485 3.3V模块 | 机器人端RS-485转换 | USART2、DE/nRE GPIO |
| 隔离USB-RS485 | 电脑端通信 | 电脑端驱动和软件，不在CubeMX配置 |
| 地面24V、机器人12V/5V降压 | 电机、逻辑、视觉供电 | 采样分压后的24V和12V。电源本身不占任务 |
| 3.3V可控MOS补光开关 | 恒压补光灯开关 | PA10普通输出。本版不做调光 |
| Luckfox Pico Max＋SC3336 | 图像采集、编码、有线以太网视频 | 不在STM32上配置DCMI、网络栈或视频任务 |
| 电脑 | 操作界面、录像、后续视觉识别 | 控制走RS-485，视频走以太网 |
| 绝对关节角度传感器 | 两连杆相对夹角监测 | SPI2与PB12片选，候选AS5048A |
| ST-Link | SWD下载和调试 | PA13、PA14，保留NRST |
| 独立回收绳、断电开关/急停 | 机械回收和硬件断能 | 不能靠RTOS任务代替 |

还需要两路电压分压/滤波保护、FB输入保护、EN外接10kΩ下拉、RS485方向控制的默认接收电平。编码器按供电/输出类型选择3.3V直连或经SN74LVC245A缓冲。不能把不同厂家六线颜色互相套用。

板级检查 WeAct V3.1原理图中的可选SPI Flash占用PA4/PA5/PA6/PA7。本配置使用这四脚作ADC，要求该Flash芯片未焊装/已电气隔离。若你的板上已装Flash，先处理硬件冲突，不能仅在CubeMX禁用SPI。PA0同时与板载用户按键相关，不能再当用户按键使用，运行时不要按。需要时按原理图隔离该按键支路。

## 2. 最终引脚分配（以此表为准）

### 2.1 必须改动的旧方向线

| 原信号 | 旧引脚 | 新引脚 | 原因 |
|---|---|---|---|
| IN1_1 | PA5 | PB8 | 释放ADC1_IN5 |
| IN1_2 | PA6 | PB9 | 释放ADC1_IN6 |
| IN2_1 | PA7 | PB10 | 释放ADC1_IN7 |

PB0的IN2_2不变。PA8/PA9 PWM、PA0/PA1和PB6/PB7编码器、PB1 EN不变。不要按旧台架说明继续连接这三根方向线。旧驱动代码也需同步更新，不能仅改线后直接烧录旧BIN。

### 2.2 完整I/O表

| 引脚 | CubeMX用户标签 | 功能/复用 | 外部连接 |
|---|---|---|---|
| PA0 | ENC1_A | TIM2_CH1 / AF1 | 电机1编码器A |
| PA1 | ENC1_B | TIM2_CH2 / AF1 | 电机1编码器B |
| PA2 | RS485_TX | USART2_TX / AF7 | SP3485 DI |
| PA3 | RS485_RX | USART2_RX / AF7 | SP3485 RO |
| PA4 | MOTOR1_FB | ADC1_IN4 | 第一路FB，适当保护 |
| PA5 | MOTOR2_FB | ADC1_IN5 | 第二路FB，适当保护 |
| PA6 | V12_SENSE | ADC1_IN6 | 12V分压后的模拟信号 |
| PA7 | V24_SENSE | ADC1_IN7 | 24V分压后的模拟信号 |
| PA8 | MOTOR1_PWM | TIM1_CH1 / AF1 | 第一路D2/PWM |
| PA9 | MOTOR2_PWM | TIM1_CH2 / AF1 | 第二路D2/PWM |
| PA10 | LIGHT_EN | GPIO_Output | 外置MOS开关控制输入 |
| PA11/PA12 | 不分配 | 保留USB物理线路 | 不另接控制信号 |
| PA13 | SWDIO | SYS_JTMS-SWDIO | ST-Link SWDIO |
| PA14 | SWCLK | SYS_JTCK-SWCLK | ST-Link SWCLK |
| PA15 | 备用 | 不配置外设 | 预留 |
| PB0 | IN2_2 | GPIO_Output | 第二路IN2 |
| PB1 | MOTOR_EN | GPIO_Output | 共用EN，外接10kΩ下拉 |
| PB2 | 保留 | 不配置外设 | 保留板上启动相关连接 |
| PB3 | MOTOR1_SF | GPIO_Input | 第一路SF |
| PB4 | MOTOR2_SF | GPIO_Input | 第二路SF |
| PB5 | RS485_DIR | GPIO_Output | DE与低有效nRE并接 |
| PB6 | ENC2_A | TIM4_CH1 / AF2 | 电机2编码器A |
| PB7 | ENC2_B | TIM4_CH2 / AF2 | 电机2编码器B |
| PB8 | IN1_1 | GPIO_Output | 第一路IN1 |
| PB9 | IN1_2 | GPIO_Output | 第一路IN2 |
| PB10 | IN2_1 | GPIO_Output | 第二路IN1 |
| PB12 | JOINT_CS | GPIO_Output，初始High | 角度传感器CSn |
| PB13 | JOINT_SCK | SPI2_SCK / AF5 | 角度传感器SCK |
| PB14 | JOINT_MISO | SPI2_MISO / AF5 | 角度传感器数据输出 |
| PB15 | JOINT_MOSI | SPI2_MOSI / AF5 | 角度传感器数据输入 |
| PC13 | STATUS_LED | GPIO_Output | 板载LED，低电平点亮 |
| PC14/PC15 | 备用 | 本版关闭LSE | 不外接其他功能 |
| PH0/PH1 | 系统自动分配 | HSE晶振 | 板载25MHz晶振 |
| NRST | 系统复位 | 保留 | 复位按键/调试器 |

TIM1_CH3只作内部比较事件，触发ADC，不输出到PA10。PA10仍是LIGHT_EN普通GPIO，不要把CH3配置成带引脚输出的PWM。

GPIO统一设置为以下状态。方向、EN、LIGHT_EN、RS485_DIR均Push-Pull、Low Speed、No Pull，初始输出Low。PC13初始High（灯灭）。JOINT_CS采用Push-Pull、Low Speed、No Pull，初始High。SF用Input、Pull-Up，不开EXTI。编码器AF输入先用Pull-Up/Low Speed，外部开漏长线需要独立合适的上拉/缓冲，不能只依靠芯片弱上拉。

MC33926固定接线 两路D1→GND。INV→GND。SLEW→3V3。VDD→3V3。已有覆盖焊盘不可与外接电平冲突。VIN接12V，逻辑和动力共地。两路OUT1/OUT2各接一台电机，Motor−不接GND。

## 3. System Core与时钟

### 3.1 MCU与SYS

- MCU Selector STM32F411CEU6，UFQFPN48，512KB Flash/128KB RAM。
- SYS → Debug Serial Wire。不要选JTAG或Trace SW，PB3/PB4用于SF。
- SYS → Timebase Source TIM11（HAL使用）。SysTick留给FreeRTOS。
- NVIC Priority Group 4 bits for pre-emption priority / 0 bits subpriority（NVIC_PRIORITYGROUP_4）。
- RCC HSE Crystal/Ceramic Resonator。HSE输入频率25MHz。
- LSI Enable，供IWDG使用。LSE Disable。RTC 不启用，时间戳由上位机/系统计时提供。

### 3.2 Clock Configuration

| 参数 | 值 |
|---|---|
| PLL Source | HSE |
| PLLM / PLLN / PLLP / PLLQ | 25 / 200 / 2 / 4 |
| SYSCLK / HCLK | 100MHz / 100MHz |
| AHB divider | /1 |
| APB1 divider / PCLK1 | /2 / 50MHz |
| APB1 timer clock | 100MHz |
| APB2 divider / PCLK2 | /1 / 100MHz |
| APB2 timer clock | 100MHz |
| Voltage regulator scale | Scale 1 |
| Flash latency | 3WS，检查生成结果 |
| ADC clock | PCLK2 /4 = 25MHz |

当前生成工程使用M=12、N=96、P=2，同样得到100MHz。25MHz/12约为2.083MHz，处于STM32F411数据手册DS10314 Rev 8表41的PLL输入上限2.10MHz内，该组合可保留。上表M=25、N=200是另一组配置目标。

此配置PLLQ输出为50MHz，因此不要启用要求48MHz时钟的USB、SDIO等模块。板上USB可按供电条件供电，但本方案不启用USB设备通信。无需为Luckfox图传开启STM32的以太网/USB。

## 4. TIM1 两路电机PWM＋ADC内部触发

TIM1的Clock Source选择Internal Clock。

| 项目 | 值 |
|---|---|
| CH1 | PWM Generation CH1，PA8 |
| CH2 | PWM Generation CH2，PA9 |
| CH3 | Output Compare No Output，仅内部比较 |
| CH4 | Disable |
| Prescaler | 0 |
| Counter Mode | Up |
| Counter Period / ARR | 4999 |
| Clock Division | DIV1 |
| Repetition Counter | 0 |
| Auto-reload Preload | Enable，固定ARR的当前工程采用Disable也可工作 |
| CH1/CH2 OC Mode | PWM mode 1 |
| CH1/CH2 Pulse | 0，启动默认不驱动 |
| CH1/CH2 polarity/idle | High / Reset |
| Fast Mode | Disable |
| CH3 OC Mode | Timing/Frozen（TIM_OCMODE_TIMING） |
| CH3 Pulse / CCR3 | 500，计数开始后约5μs的比较事件 |
| Master TRGO | Reset，ADC使用CC3事件而不是TRGO |
| Break / complementary outputs / dead time | Disable / 不使用 / 0 |
| TIM1 NVIC / DMA | 不开启 |

PWM频率=100MHz/(0+1)/(4999+1)=20kHz。MC33926内部负责H桥驱动，STM32不生成互补PWM。

ADC触发选TIM1_CC3，与电机PWM同周期，避免固定低速异步采样恰好一直采到D2关闭区间。CCR3=500只是同步采样起点，后续应检查实际FB建立时间。低占空比/换向/EN关闭时，要按两路分别标记电流采样无效，不能把0V直接解释成无电流。电流定量值、阈值、有效窗口需实测标定，硬件快速保护仍由驱动器/额外保护电路承担。

## 5. TIM2、TIM4 双编码器

两个定时器均选择 Combined Channels → Encoder Mode TI1 and TI2（TIM_ENCODERMODE_TI12），即AB相x4计数。

| 参数 | TIM2 | TIM4 |
|---|---|---|
| CH1/CH2引脚 | PA0/PA1 | PB6/PB7 |
| Prescaler | 0 | 0 |
| Period / ARR | 4294967295 | 65535 |
| Counter Mode | Up（硬件按相序改变方向） | 同左 |
| Clock Division | DIV1 | DIV1 |
| IC1/IC2 Polarity | Rising | Rising |
| IC1/IC2 Selection | Direct TI | Direct TI |
| IC1/IC2 Prescaler | DIV1 | DIV1 |
| IC1/IC2 Filter | 6 / 6 | 6 / 6 |
| Auto-reload Preload | Disable | Disable |
| NVIC / DMA | 不开 | 不开 |

不需要每个编码器边沿触发中断。MotorControlTask每10ms读取计数，代码处理TIM2的32位回绕和TIM4的16位回绕。RPM换算需要实测减速箱输出轴一整圈的x4计数。里程还要考虑外部1.6:1传动比和轮径，不能把电机轴转速直接当轮速。

## 6. ADC1与DMA 电流、电压

ADC1勾选IN4、IN5、IN6、IN7。外部输入必须在GND至VDDA允许范围内，12V/24V绝不能直连ADC。

| 参数 | 值 |
|---|---|
| Resolution / alignment | 12 bits / Right |
| Clock Prescaler | PCLK2 divided by 4（25MHz） |
| Scan Conversion | Enable |
| Continuous Conversion | Disable |
| Discontinuous Conversion | Disable |
| Number of Conversions | 4 |
| External Trigger | Timer 1 Capture Compare 3 event（TIM1_CC3） |
| Trigger Edge | Rising |
| DMA Continuous Requests | Enable |
| EOC Selection | End of sequence（ADC_EOC_SEQ_CONV） |
| ADC global interrupt | 开启，优先级6，用于Overrun错误处理。正常数据使用DMA半满/满通知，不开启逐次EOC中断 |

| Rank | Channel | 信号 | Sampling Time |
|---|---|---|---|
| 1 | ADC1_IN4 / PA4 | 电机1 FB | 15 cycles，需核对FB源阻抗和建立时间 |
| 2 | ADC1_IN5 / PA5 | 电机2 FB | 15 cycles，同上 |
| 3 | ADC1_IN6 / PA6 | 12V分压 | 84 cycles |
| 4 | ADC1_IN7 / PA7 | 24V分压 | 84 cycles |

2026-11-04核对补充 HAL_ADC_Start_DMA会启用ADC_IT_OVR。应在ADC_IRQHandler中调用HAL_ADC_IRQHandler，并在HAL_ADC_ErrorCallback中处理Overrun、使数据失效及通知安全任务。不能仅靠DMA中断覆盖该类错误。EOC选择描述转换完成标志，不表示DMA只搬运每帧最后一个通道。

DMA Settings增加ADC1 DMA2 Stream0 Channel0，Peripheral to Memory，Circular，Peripheral Increment Disable，Memory Increment Enable，Peripheral/Memory width均Half Word，Priority Very High，FIFO Disable。

软件缓冲区 静态 `uint16_t adc_dma[1600]`，每帧按FB1/FB2/V12/V24排列。20k帧/秒、每帧4个值，整缓冲区20ms，半缓冲区10ms。HT/TC回调通知SensorTask处理刚写完的一半。数组放全局/静态RAM，不放任务栈。通知必须能区分半缓冲并检测处理超期，不能把覆盖后的旧数据继续当有效数据。

电压前端可从12V用47kΩ/10kΩ、24V用100kΩ/10kΩ分压作为设计起点，另配滤波和输入保护。对应名义倍率5.7和11。必须校核最高电压、器件误差和瞬态并实测校准。这些是外部电路，CubeMX无法替代。

FB约525mV/A仅为器件典型值，PWM关闭/制动时不代表真实电流，低电流下精度也有限。SF同样会在禁用状态拉低，所以PB3/PB4只用GPIO输入。**不要对SF下降沿开启EXTI，否则PWM可能造成大量伪故障中断。** 软件需在有效驱动窗口读取/确认SF，并锁存真实故障。

## 7. USART2与RS-485

USART2 → Asynchronous。TX PA2、RX PA3。不要选Half-Duplex Single Wire（外部485收发器依然使用独立TX/RX）。

| 参数 | 值 |
|---|---|
| Baud Rate | 115200 |
| Word Length / Parity / Stop Bits | 8 bits / None / 1 |
| Mode | TX and RX |
| Hardware Flow Control | None |
| Oversampling | 16 |
| RX GPIO | AF7、Pull-Up |
| TX GPIO | AF7、No Pull |
| PB5 RS485_DIR | GPIO Push-Pull、初始Low |

PB5接DE和nRE（低有效接收使能）共用控制 Low接收，High发送。按SP3485模块实际引脚核对。DMA不自动控制方向，代码要在发送前拉高PB5，并在UART TC真正结束后拉低，不能只等DMA搬完数据。

| DMA请求 | 控制器/Stream/Channel | 模式 | 数据宽度 | DMA仲裁优先级 |
|---|---|---|---|---|
| USART2_RX | DMA1 / Stream5 / Channel4 | Circular | Byte→Byte | High |
| USART2_TX | DMA1 / Stream6 / Channel4 | Normal | Byte→Byte | Medium |

二者均Peripheral Increment Disable、Memory Increment Enable、FIFO Disable。RX Peripheral→Memory，TX Memory→Peripheral。

RX缓冲区先设256字节。用HAL_UARTEx_ReceiveToIdle_DMA实现DMA环形收数，代码记录HT/TC/IDLE位置并处理环形回绕，不把一次回调等同于一个完整协议帧。CommTask是串口和DE的唯一正常使用者。线路两端匹配终端，参考地、屏蔽及隔离按模块手册接线。

通信策略 电脑主站轮询、机器人应答。目标控制/心跳20Hz，遥测20Hz（随请求应答），CRC16与序号、帧超时由软件实现。有效控制链路超时先设300ms，最终按停车距离和链路实测确认。不要因收到无效字节或重复陈旧帧就刷新运动许可。无需为CRC16开启STM32固定CRC32外设。

### 7.1 关节角度传感器SPI2

PB12作为普通GPIO片选，初始High。PB13/PB14/PB15分别为SPI2 SCK/MISO/MOSI，AF5。主机双线全双工，软件NSS，由SensorTask独占访问并采用有界超时。

| 参数 | 候选AS5048A的接入起点 |
|---|---|
| Mode / Direction | Master / Full Duplex |
| NSS | Software，PB12控制片选 |
| Data Size | 16 bits，与器件协议帧对应 |
| Clock Polarity / Phase | Low / 2 Edge，SPI Mode 1 |
| Baud Rate Prescaler | /64，PCLK1为50MHz时约781.25kHz |
| First Bit | MSB |
| TI Mode / Hardware CRC | Disable / Disable |
| DMA / SPI IRQ | 初期采用有界轮询传输 |

这组时序以候选AS5048A为前提，最终型号及模块电路仍待确认。当前生成工程为8位、Mode 0、/2即25MHz，需要按所用器件调整。SPI软件校验、错误标志、读回流程和16位帧片选时序需在驱动中实现。使用3.3V逻辑并按模块手册接供电。

SensorTask每20ms读取一次关节角度及器件支持的状态，发布带时间戳和有效标志的数据。SafetyTask检查时效与异常，State按装管/行走等阶段采用对应策略。详细机械条件与标定见[角度传感器增补方案](center_joint_angle_sensor.md)。

## 8. TIM11、NVIC和IWDG

TIM11仅作HAL的1ms时基，由SYS Timebase自动生成，不占引脚。不要拿它做PWM。生成后检查HAL_IncTick只在TIM11回调递增，FreeRTOS SysTick处理只保留一份。


TIM11在HAL_Init阶段已经启动，TIM1句柄在MX_TIM1_Init阶段才初始化。生成后检查共享的TIM1_TRG_COM_TIM11_IRQHandler，当前规划要求其派发目标为htim11，避免启动期访问未初始化的htim1。目标入口如下，修改后需复查CubeMX重新生成结果。

```c
void TIM1_TRG_COM_TIM11_IRQHandler(void)
{
    HAL_TIM_IRQHandler(&htim11);
}
```

保持TIM11的共享IRQ使能，并取消TIM1触发/换相中断的参与。检查TIM1初始化及反初始化代码对共享IRQ的使能/禁用操作，防止影响HAL时基。将优先级改为15之后，仍需核对实际派发句柄。

### 8.1 中断优先级

| 中断 | Enable | Preemption priority | Subpriority | 是否调用RTOS |
|---|---|---:|---:|---|
| DMA2_Stream0（ADC） | 是 | 6 | 0 | 仅ISR安全通知 |
| DMA1_Stream5（UART RX） | 是 | 6 | 0 | 仅ISR安全通知 |
| DMA1_Stream6（UART TX） | 是 | 6 | 0 | 仅ISR安全通知 |
| USART2 global | 是 | 6 | 0 | 仅ISR安全通知 |
| ADC global（ADC_IRQn） | 是 | 6 | 0 | Overrun错误处理，可ISR安全通知 |
| TIM1_TRG_COM_TIM11（HAL时基） | 是 | 15 | 0 | 否，仅HAL时基 |
| SysTick | 是 | 15 | 0 | 内核时钟 |
| PendSV | 是 | 15 | 0 | 内核上下文切换 |
| SVCall | 保持Cube默认 | 默认 | 默认 | 内核管理 |
| TIM2 / TIM4 / SF EXTI | 否 | — | — | — |

STM32中断数字越小优先级越高，和FreeRTOS任务优先级方向相反。configLIBRARY_MAX_SYSCALL_INTERRUPT_PRIORITY=5时，调用RTOS ISR接口的外设中断必须设5至15。本表统一6。不允许在高于该门限的ISR中调用RTOS。ISR里不做PID、日志打印、协议解析或HAL_Delay。

### 8.2 IWDG

- IWDG Enable。LSI Enable。Prescaler=64。Reload=999，按32kHz LSI名义频率约2秒，实际随LSI误差变化。
- 只允许SafetyTask在关键任务和数据健康时每约100ms喂狗。不要在Idle、每个任务或通用循环中无条件喂狗。
- 上电初始化也要在超时范围内完成。故障后必须先关闭EN再记录故障/等待复位。EN外接下拉保证复位期间默认不驱动。
- 调试时可使用DBGMCU冻结IWDG，发布运行时恢复正常行为。停止CPU不等于停止PWM，软件看门狗监测也不会在暂停时执行。操作硬件断电后再单步。

## 9. FreeRTOS内核配置

Middleware → FREERTOS Interface=CMSIS_V2。使用本机STM32Cube FW_F4 V1.28.3自带适配层，不另外混入不同版本的FreeRTOS移植文件。

### 9.1 参数表

| 项目 | 设置 |
|---|---|
| configUSE_PREEMPTION | 1 |
| configUSE_TIME_SLICING | 1 |
| configTICK_RATE_HZ | 1000 |
| configCPU_CLOCK_HZ | 使用SystemCoreClock，100MHz |
| configMAX_PRIORITIES | **56，CMSIS_V2适配层要求** |
| configUSE_PORT_OPTIMISED_TASK_SELECTION | **0，与56级优先级配套** |
| configUSE_16_BIT_TICKS | 0 |
| configUSE_TICKLESS_IDLE | 0 |
| configIDLE_SHOULD_YIELD | 1 |
| Memory Management | heap_4 |
| configTOTAL_HEAP_SIZE | 32768 bytes |
| configMINIMAL_STACK_SIZE | 256 words（1024 bytes） |
| configSUPPORT_DYNAMIC_ALLOCATION | 1，应用对象统一在初始化阶段创建 |
| configSUPPORT_STATIC_ALLOCATION | 1，保留CMSIS适配需求和后续静态分配能力 |
| configMAX_TASK_NAME_LEN | 24 |
| configUSE_MUTEXES / configUSE_RECURSIVE_MUTEXES | 1 / 1 |
| configUSE_COUNTING_SEMAPHORES | 1 |
| configUSE_TASK_NOTIFICATIONS | 1 |
| configUSE_TIMERS | 1 |
| configTIMER_TASK_PRIORITY | 2（FreeRTOS内部值，非CMSIS枚举），仅执行短回调 |
| configTIMER_QUEUE_LENGTH | 10 |
| configTIMER_TASK_STACK_DEPTH | 256 words起，当前工程512 words可保留，运行后核验 |
| configCHECK_FOR_STACK_OVERFLOW | 2 |
| configUSE_MALLOC_FAILED_HOOK | 1 |
| configUSE_IDLE_HOOK / configUSE_TICK_HOOK | 0 / 0 |
| configUSE_TRACE_FACILITY | 1 |
| configUSE_STATS_FORMATTING_FUNCTIONS | 0，诊断读原始统计，不依赖sprintf格式化接口 |
| configGENERATE_RUN_TIME_STATS | 0。本版不另占硬件统计定时器 |
| configUSE_NEWLIB_REENTRANT | 0。禁止任务随意并发使用非重入C库/printf |
| configQUEUE_REGISTRY_SIZE | 8 |
| configLIBRARY_LOWEST_INTERRUPT_PRIORITY | 15 |
| configLIBRARY_MAX_SYSCALL_INTERRUPT_PRIORITY | 5 |
| configKERNEL_INTERRUPT_PRIORITY | 15 << 4 = 240 / 0xF0（生成后核对） |
| configMAX_SYSCALL_INTERRUPT_PRIORITY | 5 << 4 = 80 / 0x50（生成后核对） |

启用INCLUDE_vTaskDelay、INCLUDE_vTaskDelayUntil、INCLUDE_vTaskDelete、INCLUDE_vTaskSuspend、INCLUDE_xTaskGetSchedulerState、INCLUDE_xTaskGetCurrentTaskHandle、INCLUDE_eTaskGetState、INCLUDE_uxTaskGetStackHighWaterMark、INCLUDE_uxTaskPriorityGet、INCLUDE_vTaskPrioritySet、INCLUDE_xTimerPendFunctionCall（名称以所用Kernel/Cube界面为准）。其中CMSIS适配层要求的宏不能随意关闭，生成后以freertos_os2.h的编译检查为准。

启用configASSERT。实现栈溢出、内存分配失败的hook，先强制EN低、锁存故障，再停机处理。它们不是仅勾选后就自动存在的保护代码。启用静态分配后，Idle/Timer的内存回调使用适配层/生成器提供的唯一实现，不重复定义。

### 9.2 Tasks表 创建6个应用任务

| Task Name | Entry Function | CMSIS priority | 周期/触发 | Stack bytes | 若界面单位是32位Words |
|---|---|---|---|---:|---:|
| SafetyTask | StartSafetyTask | osPriorityHigh（40） | 5ms＋故障通知 | 1024 | 256 |
| MotorControlTask | StartMotorControlTask | osPriorityAboveNormal（32） | 10ms | 2048 | 512 |
| SensorTask | StartSensorTask | osPriorityNormal（24） | ADC批处理约10ms，关节测角20ms | 1536 | 384 |
| CommTask | StartCommTask | osPriorityNormal（24） | 串口通知/队列，等待不超过50ms以更新健康状态 | 3072 | 768 |
| RobotStateTask | StartRobotStateTask | osPriorityBelowNormal（16） | 20ms＋命令 | 1536 | 384 |
| DiagnosticTask | StartDiagnosticTask | osPriorityLow（8） | 500ms＋诊断事件 | 2048 | 512 |

合计应用栈11264 bytes，另有TCB、队列、Idle/Timer、DMA/通信全局缓冲、C栈及其他RAM。32KB RTOS heap为初始预算，运行时测剩余堆和栈高水位后再调整，不等于已经完成内存验收。

CubeMX版本可能显示Bytes或Words，必须看列单位。CMSIS生成的osThreadAttr_t.stack_size最终单位是字节。不要把2048 bytes误填成2048 words。

将自动defaultTask改作RobotStateTask或删除其创建项，不再多留一个无职责常驻任务。Idle与Timer Service由内核创建，预期运行列表为6个应用任务＋Idle＋Timer Service=8个。

### 9.3 任务职责和输出所有权

- Safety 检查电流/驱动/电压有效数据、控制链路时效和任务心跳。必要时强制EN低。锁存故障。只有健康条件满足才喂IWDG。
- MotorControl 同周期采集两编码器、计算速度/里程估计、双路PID/软启动/换向、正常PWM/方向/EN输出。禁止等待通信或打印日志。恢复EN前必须与故障锁存同步，防止覆盖安全停机。
- Sensor 处理刚完成的ADC缓冲半区，标定/滤波/有效性，按有效窗口确认SF，每20ms读取关节角度，发布带时间戳的数据。
- Comm 唯一UART/485方向使用者。CRC、分包、序号、请求/应答、通信健康。有效命令送State，不直接改PWM。
- State 初始化/待机/使能/运行/停止/故障状态机，发布运动目标，控制LIGHT_EN。故障恢复必须显式请求并检查条件。
- Diagnostic 故障环形记录、运行与栈/堆统计、PC13指示、受限诊断输出。无高频阻塞printf。

Safety周期5ms不代表故障一定5ms内检测到 还受ADC有效窗口、10ms批处理、滤波/确认时间及调度影响。短路等快速保护不能只依赖该任务。

### 9.4 RTOS对象

以下是最终逻辑对象清单。可在CubeMX对象页创建，也可在USER CODE初始化区按结构体sizeof创建。使用结构体时先在公共头文件定义并在生成模块包含它，不能把一整帧误配置成单个uint32_t。

| 对象 | 类型/容量 | 使用关系 |
|---|---|---|
| cmdQueue | Message Queue，8个RobotCommand | Comm→State。CRC/序号通过后入队 |
| motionMailbox | Message Queue，1个MotionTarget | State→Motor。只保留最新目标，覆盖/替换策略要写代码 |
| sensorMailbox | Message Queue，1个SensorSnapshot | Sensor→Safety。消费后Safety维护共享快照，其他任务不竞争消费 |
| telemetryMailbox | Message Queue，1个TelemetrySnapshot | State→Comm。生成时通过短锁读取已发布的运动/采样快照 |
| diagnosticQueue | Message Queue，16个DiagnosticEvent | 各任务→Diagnostic。非关键日志满时可丢弃并计数 |
| systemFlags | Event Flags | INIT_READY、FAULT_LATCHED等。故障停机不只依赖异步事件位 |
| snapshotMutex | Mutex，Priority Inheritance | 极短时间复制完整多字段快照。锁内不进行HAL阻塞通信 |
| SensorTask thread flags | 任务通知位 | ADC HT/TC与错误，由ISR通知。处理丢事件/缓冲覆盖 |
| CommTask thread flags | 任务通知位 | UART RX/TX/ERROR。索引记录必须可靠，不能只用一个布尔位表示多个位置 |

不需要另建UART互斥锁（唯一使用者是Comm），也不需要给每台电机、每个编码器或每个ADC通道单独建任务。正常速度指令和硬故障分开处理，急停不能被cmdQueue积压阻塞。除明确的ISR安全调用外，不从中断操作互斥锁。ISR的队列超时参数必须为0。

## 10. 初始化、生成工程与必须补写的代码

Project Manager Toolchain/IDE=CMake。Firmware Package保持已选F4版本。勾选Keep User Code。生成外设独立.c/.h。保留SWD。FPU/编译使用Cortex-M4、FPv4-SP-D16、hard ABI，与现有工程一致。

调度器运行前创建任务、队列与标志，硬件保持EN低。可由RobotStateTask执行一次性启动序列，其他任务阻塞等待INIT_READY

1. HAL与系统时钟就绪后先初始化GPIO安全电平，再配置DMA、ADC、UART、SPI2和各TIM。TIM11的HAL时基由HAL初始化流程建立。
2. 启动两路编码器计数。UART启动DMA接收，确认任务句柄已有效。
3. 先启动ADC DMA，再启动TIM1内部CH3比较事件和两路PWM。CCR1/CCR2为0，EN仍为低。
4. 检查外设启动返回值和采样时效，发布INIT_READY。异常保持禁能。
5. 进入待机，收到有效使能/速度命令且安全许可后才允许运动。

HAL/CubeMX初始化函数只配置外设，不会自动实现PID、启动PWM、启动编码器、启动ReceiveToIdle DMA、正确485换向、协议、故障锁存、看门狗健康判定、ADC同步有效性或任务间数据同步。这些均需在生成代码后补写。IWDG如果在调度器前已启动，应确保整段启动流程在超时内结束。

原先裸机main循环内MotorControl_Task及3秒测试控制逻辑不能同时保留为第二套输出者。迁移到RTOS时复用算法模块，但只由MotorControlTask驱动。正式工作模式通过状态机/通信控制，不再依赖修改调试变量。

## 11. 按顺序操作和验收

- [ ] 复制工程/另存IOC，选择STM32F411CEU6和CMake。
- [ ] 检查可选SPI Flash、PA0按键、板卡版本与25MHz晶振。
- [ ] SYS Serial Wire。HAL时基TIM11。HSE/LSI与100MHz时钟树。
- [ ] 迁移三根方向线的引脚分配。按完整表命名所有GPIO。
- [ ] TIM1 CH1/CH2 PWM，CH3内部比较。PA10保持GPIO，不输出CH3。
- [ ] TIM2/TIM4 TI12编码器、ARR、滤波与上拉。
- [ ] ADC1四通道、Rank、采样时间、TIM1_CC3触发与DMA2_Stream0。
- [ ] USART2 115200 8N1、RX/TX DMA、PB5方向控制。
- [ ] SPI2与PB12片选、最终器件时序和20ms测角周期。
- [ ] IWDG/LSI。NVIC优先级。确认未开启SF EXTI。
- [ ] 共享IRQ仅派发已初始化的必要句柄，启动期TIM11工作正常。
- [ ] CMSIS_V2、56级优先级、heap_4、内核参数、6任务与栈单位。
- [ ] 创建RTOS对象/公共结构体与初始化代码，保留唯一外设所有者。
- [ ] 生成后核对FreeRTOSConfig.h、stm32f4xx_it.c、HAL时基文件和CMake源文件列表。
- [ ] 空载测量PWM20kHz及默认EN低，确认两编码器正反向/x4计数。
- [ ] ADC输入量程、Rank、PWM有效窗口及故障状态解释实测通过。
- [ ] 校验485发送尾位、超时停车、CRC/坏帧处理、故障复位流程。
- [ ] 检查栈高水位、最小剩余堆、控制超期、ADC缓冲覆盖和看门狗复位。
- [ ] 整机负载、拖线、温升及实际停车距离验证后再定保护阈值。

## 12. 参考依据

- [项目采购及系统分工](https://github.com/A123536/final-report/blob/master/design/procurement_guide_20261028.md)
- [STM32F411数据手册 封装、引脚、时钟](https://www.st.com/resource/en/datasheet/stm32f411ce.pdf)
- [STM32F411 RM0383 DMA映射与外设](https://www.st.com/resource/en/reference_manual/rm0383-stm32f411xce-advanced-armbased-32bit-mcus-stmicroelectronics.pdf)
- [WeAct V3.1原理图](https://github.com/WeActStudio/WeActStudio.MiniSTM32F4x1/blob/master/Hardware/MiniF4x1Cx_V31%20SchDoc.pdf)
- [Pololu #1213引脚与FB/SF行为](https://www.pololu.com/product/1213)
- [AS5048A厂家资料](https://www.infineon.com/part/AS5048A)
- [CMSIS-FreeRTOS适配实现](https://github.com/ARM-software/CMSIS-FreeRTOS/tree/main/CMSIS/RTOS2/FreeRTOS)

本机核对依据还包括 STM32Cube FW_F4 V1.28.3中的cmsis_os2.h、freertos_os2.h和stm32f4xx_hal_adc.h。本机适配层明确要求configMAX_PRIORITIES=56、configUSE_PORT_OPTIMISED_TASK_SELECTION=0。DMA表核对RM0383的DMA1/2 request mapping。2026-11-05已对用户生成工程执行源码复查与Debug构建，并在开头记录实施状态。目标配置仍需在CubeMX中核对生成结果并上板验证，界面字段名称以安装版本显示为准。
