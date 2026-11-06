/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */

/* Define to prevent recursive inclusion -------------------------------------*/
#ifndef __MAIN_H
#define __MAIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* Includes ------------------------------------------------------------------*/
#include "stm32f4xx_hal.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Exported types ------------------------------------------------------------*/
/* USER CODE BEGIN ET */

/* USER CODE END ET */

/* Exported constants --------------------------------------------------------*/
/* USER CODE BEGIN EC */

/* USER CODE END EC */

/* Exported macro ------------------------------------------------------------*/
/* USER CODE BEGIN EM */

/* USER CODE END EM */

/* Exported functions prototypes ---------------------------------------------*/
void Error_Handler(void);

/* USER CODE BEGIN EFP */

/* USER CODE END EFP */

/* Private defines -----------------------------------------------------------*/
#define Encoder1_1_Pin GPIO_PIN_0
#define Encoder1_1_GPIO_Port GPIOA
#define Encoder1_2_Pin GPIO_PIN_1
#define Encoder1_2_GPIO_Port GPIOA
#define IN2_2_Pin GPIO_PIN_0
#define IN2_2_GPIO_Port GPIOB
#define En_Pin GPIO_PIN_1
#define En_GPIO_Port GPIOB
#define IN2_1_Pin GPIO_PIN_10
#define IN2_1_GPIO_Port GPIOB
#define CS_Pin GPIO_PIN_12
#define CS_GPIO_Port GPIOB
#define PWM1_Pin GPIO_PIN_8
#define PWM1_GPIO_Port GPIOA
#define PWM2_Pin GPIO_PIN_9
#define PWM2_GPIO_Port GPIOA
#define light_Pin GPIO_PIN_10
#define light_GPIO_Port GPIOA
#define SF1_Pin GPIO_PIN_3
#define SF1_GPIO_Port GPIOB
#define SF2_Pin GPIO_PIN_4
#define SF2_GPIO_Port GPIOB
#define RS485_direction_control_Pin GPIO_PIN_5
#define RS485_direction_control_GPIO_Port GPIOB
#define Encoder2_1_Pin GPIO_PIN_6
#define Encoder2_1_GPIO_Port GPIOB
#define Encoder2_2_Pin GPIO_PIN_7
#define Encoder2_2_GPIO_Port GPIOB
#define IN1_1_Pin GPIO_PIN_8
#define IN1_1_GPIO_Port GPIOB
#define IN1_2_Pin GPIO_PIN_9
#define IN1_2_GPIO_Port GPIOB

/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */
