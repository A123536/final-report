# STM32F411整机配置阶段快照

快照日期为2026-11-06，来源为用户当前的Final_project5工程。CubeMX版本为6.18.1，固件包为STM32Cube FW_F4 V1.28.3，目标器件为STM32F411CEU6，生成方式为CMake，RTOS接口为CMSIS-RTOS V2。

本目录保存当前IOC、Core应用框架、HAL/CMSIS驱动、FreeRTOS源码、启动文件、链接脚本和CMake构建文件。原工程源文件按字节复制，包含用户已加入的共享中断启动期判空防护。

## 已配置内容

| 模块 | 当前配置 |
|---|---|
| 系统时钟 | 25MHz HSE，PLLM=12、PLLN=96、PLLP=2，SYSCLK=100MHz |
| HAL与RTOS时基 | TIM11提供HAL毫秒计时，SysTick提供FreeRTOS节拍 |
| 电机PWM | TIM1_CH1/CH2，PA8/PA9，20kHz |
| ADC触发 | TIM1内部CH3比较事件，CCR3=500 |
| 编码器 | TIM2 PA0/PA1、TIM4 PB6/PB7，TI12，输入滤波6，上拉 |
| 模拟采样 | ADC1 PA4～PA7，连续DMA请求，DMA2 Stream0 Channel0环形搬运 |
| RS-485 | USART2 PA2/PA3，115200 8N1，RX环形DMA、TX普通DMA，PB5方向控制 |
| 电机GPIO | PB8/PB9/PB10/PB0方向，PB1使能，PB3/PB4状态输入 |
| 补光与指示 | PA10补光控制，PC13状态灯 |
| 关节传感器接口 | SPI2 PB13/PB14/PB15，PB12软件片选，默认High |
| FreeRTOS | 六任务框架，32KB堆，栈溢出与内存分配失败检测已启用 |
| IWDG | LSI、分频64、Reload999，名义超时约2秒 |

完整目标配置及复查说明见[整机CubeMX清单](../../design/cubemx_full_system_configuration.md)。关节传感器的机械安装与能力边界见[测角方案](../../design/center_joint_angle_sensor.md)。

## 构建

安装CMake 3.22或更高版本、Ninja和Arm GNU工具链，并将cmake、ninja、arm-none-eabi-gcc所在目录加入当前终端PATH。可使用STM32Cube工具包提供的对应程序。

在本目录运行以下命令。

```powershell
cmake --preset Debug
cmake --build --preset Debug
```

输出为 `build/Debug/Final_project5.elf`。本快照交付验证使用Arm GCC 14.3.1、CMake 4.3.1和Ninja 1.13.2，在仓库内从新目录完成Debug构建。上板启动、通信及采样波形待实测。

## 当前待完成项

1. 六个任务当前执行osDelay(1)占位循环，需要实现状态机、双电机闭环、数据采集、通信和诊断职责。
2. ADC DMA、PWM、编码器和UART接收的运行启动及回调处理需要补写，还需创建任务间同步对象并检查创建结果。
3. IWDG已经在main初始化流程中启动，需要SafetyTask在关键任务和数据健康时统一刷新。当前框架持续运行且没有刷新时，约2秒后将复位。
4. 栈溢出和内存分配失败Hook目前保留生成模板，异常断能、故障锁存及处理流程需要实现。
5. SPI2当前为8位、Mode 0、分频2，在50MHz PCLK1下约25MHz。最终角度模块需要确认。若使用候选AS5048A，应按其时序匹配相位、帧宽、片选和读回流程，初次接入可采用Mode 1、16位、分频64作为待验证起点。
6. TIM1_TRG_COM_TIM11_IRQHandler已在USER CODE区加入htim1.Instance为空时先处理TIM11并返回的防护。以后CubeMX重新生成需核对该段保留情况。运行时若反初始化TIM1，需检查其关闭共享IRQ的操作对TIM11计时的影响。
7. FB有效采样窗口、参考电压、电流及电压标定、控制与保护阈值需要硬件试验确认。

当前工程的编译验证覆盖构建完整性，整机带载验收需完成以上业务逻辑和实测。

## 依赖许可

HAL、CMSIS和FreeRTOS保持原有版权头及各自LICENSE文件。STM32CubeF4包级许可文本随本目录的Package_license.md提供，适用条款按组件对应关系查阅。
