#include <rtthread.h>
#include <rtdevice.h>
#include <stdint.h>
#include <string.h>
#include <drivers/serial.h>
#include "mqtt_publish.h"

#define UART_NAME           "uart5"
#define RX_BUF_SIZE         256
#define DATA_TIMEOUT_MS     5000    // Every 5 seconds

static rt_device_t serial;
static struct rt_semaphore rx_sem;
static uint8_t rx_buffer[RX_BUF_SIZE];
static volatile rt_size_t rx_index = 0;

/* Calculate checksum: XOR then invert */
static uint8_t get_checksum(uint8_t *data, uint8_t len)
{
    uint8_t ret = 0;
    for (int i = 0; i < len; i++)
        ret ^= data[i];
    return ~ret;
}

/* UART RX callback: release semaphore */
static rt_err_t uart_rx_callback(rt_device_t dev, rt_size_t size)
{
    rt_sem_release(&rx_sem);
    return RT_EOK;
}

/* Parse frame: check fall/normal status */
/* Parse frame: check fall/normal status */
static void parse_tinyframe(uint8_t *frame, rt_size_t len)
{
    if (len < 8) return; // 最小帧头长度

    // 解析帧头字段（大端序）
    uint16_t frame_len = (frame[3] << 8) | frame[4];
    uint16_t frame_type = (frame[5] << 8) | frame[6];
    uint8_t head_cksum = frame[7];

    // 校验帧头（SOF到TYPE共7字节）
    if (get_checksum(frame, 7) != head_cksum) {
        rt_kprintf("Header checksum error!\n");
        return;
    }

    // 处理跌倒检测消息 (0x0E02)
    if (frame_type == 0x0E02 && frame_len >= 1) {
        uint8_t status = frame[8]; // DATA部分第一个字节
        uint8_t data_cksum = frame[8 + frame_len];

        // 校验数据部分
        if (get_checksum(&frame[8], frame_len) != data_cksum) {
            rt_kprintf("Data checksum error!\n");
            return;
        }

        // 输出状态信息
        if (status == 0x00) {
            rt_kprintf("normal\n");  // 仅打印 normal
            mqtt_publish_message("ld6002/fall_status", "{\"status\":\"normal\"}");
        } else if (status == 0x01) {
            rt_kprintf("fall\n");    // 仅打印 fall
            mqtt_publish_message("ld6002/fall_status", "{\"status\":\"fall\"}");
        }

    }
}


/* RX thread: collect and parse data every 5 seconds */
static void serial_thread_entry(void *parameter)
{
    uint8_t ch;
    rt_tick_t last_tick = rt_tick_get();

    while (1) {
        if (rt_sem_take(&rx_sem, RT_WAITING_FOREVER) == RT_EOK) {
            // 读取串口数据
            while (rt_device_read(serial, 0, &ch, 1) == 1) {
                if (rx_index < RX_BUF_SIZE) {
                    rx_buffer[rx_index++] = ch;
                } else {
                    // 缓冲区满时重置
                    rx_index = 0;
                    rx_buffer[rx_index++] = ch;
                }
            }
        }

        // 每5秒或数据满时尝试解析
        if (rt_tick_get() - last_tick > rt_tick_from_millisecond(DATA_TIMEOUT_MS) ||
            rx_index >= RX_BUF_SIZE)
        {
            // 查找有效的TinyFrame(SOF=0x01)
            for (rt_size_t i = 0; i < rx_index; i++) {
                if (rx_buffer[i] == 0x01) {
                    // 检查帧长度是否足够
                    if (i + 8 < rx_index) {
                        uint16_t data_len = (rx_buffer[i+3] << 8) | rx_buffer[i+4];
                        if (i + 8 + data_len + 1 < rx_index) {
                            parse_tinyframe(&rx_buffer[i], 8 + data_len + 1);
                            i += 8 + data_len; // 跳过已处理帧
                        }
                    }
                }
            }
            rx_index = 0; // 清空缓冲区
            last_tick = rt_tick_get();
        }
    }
}

/* Main entry function */
static int ld6002_status_app(int argc, char *argv[])
{
    serial = rt_device_find(UART_NAME);
    if (!serial) return RT_ERROR;

    struct serial_configure config = RT_SERIAL_CONFIG_DEFAULT;
    config.baud_rate = BAUD_RATE_115200;
    config.data_bits = DATA_BITS_8;
    config.stop_bits = STOP_BITS_1;
    config.parity = PARITY_NONE;

    rt_device_control(serial, RT_DEVICE_CTRL_CONFIG, &config);
    rt_kprintf("UART %s initialized at 115200 bps.\n", UART_NAME);

    // Init semaphore
    rt_sem_init(&rx_sem, "rx_sem", 0, RT_IPC_FLAG_FIFO);

    // Open UART device and set RX interrupt callback
    rt_device_open(serial, RT_DEVICE_FLAG_INT_RX);
    rt_device_set_rx_indicate(serial, uart_rx_callback);

    // Create RX thread
    rt_thread_t thread = rt_thread_create("ld6002_rx",
                                          serial_thread_entry,
                                          RT_NULL,
                                          2048,
                                          20,
                                          10);
    if (thread)
    {
        rt_thread_startup(thread);
        rt_kprintf("Fall detection thread started.\n");
    }
    else
    {
        rt_kprintf("Failed to create thread.\n");
        return RT_ERROR;
    }

    return RT_EOK;
}
MSH_CMD_EXPORT(ld6002_status_app, HLK-LD6002 fall detection parser);


