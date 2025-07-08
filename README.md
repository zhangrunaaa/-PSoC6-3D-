# HLK-LD6002 跌倒检测系统（RT-Thread + MQTT）

本项目基于 [RT-Thread](https://www.rt-thread.io/) 实时操作系统，使用 **HLK-LD6002 毫米波雷达模块** 进行人体跌倒检测，并通过 MQTT 协议上传至云端。适用于老年人看护、居家健康监控等 IoT 场景。

---

## 🔧 项目功能概述

- 👣 支持 HLK-LD6002 跌倒检测帧完整解析（含校验）
- 🔗 WiFi 自动连接并建立 MQTT 连接
- ☁️ 通过 MQTT 将 `normal` / `fall` 状态上传
- 📶 可使用 MQTTX 工具实时查看状态

---

## 📂 项目结构
├── src/
│ ├── ld6002_status.c # 串口接收 + 数据帧解析 + MQTT上传
│ ├── wifi_connect.c # WiFi 自动连接逻辑
│ ├── mqtt_publish.c # MQTT 客户端初始化与连接
│ └── mqtt_publish.h # MQTT 接口声明头文件
├── README.md # 项目说明文档
├── Kconfig / SConscript # RT-Thread 工程构建文件

---

## 📡 硬件与平台

- **开发平台**：任意支持 RT-Thread + UART + WiFi 的主控（如 STM32 + ESP8266）
- **雷达模块**：[HLK-LD6002](https://item.taobao.com/item.htm?id=638521674214)
- **波特率**：115200
- **网络模块**：ESP8266 / ESP32 / 板载 WiFi 模块
- **MQTT Broker**：公网 `broker.emqx.io:1883`

---

## ⚙️ 参数配置

### 串口配置（`ld6002_status.c`）

```c
#define UART_NAME       "uart5"
#define BAUD_RATE       115200
