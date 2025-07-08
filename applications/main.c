#include <rtthread.h>
#include <wlan_mgnt.h>
#include <wlan_cfg.h>
#include "mqtt_publish.h"

#define WLAN_SSID       "3-403"
#define WLAN_PASSWORD   "20201105"
#define NET_READY_TIMEOUT  (rt_tick_from_millisecond(15000))

static rt_sem_t net_ready_sem = RT_NULL;

static void wlan_ready_handler(int event, struct rt_wlan_buff *buff, void *parameter)
{
    rt_kprintf("[WiFi] Network is ready!\n");
    rt_sem_release(net_ready_sem);
}

void start_network_service(void)
{
    if (rt_wlan_is_ready())
    {
        rt_kprintf("[APP] WiFi already connected. Starting MQTT...\n");
        mqtt_start();
        return;
    }

    net_ready_sem = rt_sem_create("net_ready", 0, RT_IPC_FLAG_FIFO);
    rt_wlan_register_event_handler(RT_WLAN_EVT_READY, wlan_ready_handler, RT_NULL);
    rt_wlan_set_mode(RT_WLAN_DEVICE_STA_NAME, RT_WLAN_STATION);

    rt_kprintf("[APP] Connecting to WiFi SSID: %s...\n", WLAN_SSID);

    if (rt_wlan_connect(WLAN_SSID, WLAN_PASSWORD) == RT_EOK)
    {
        if (rt_sem_take(net_ready_sem, NET_READY_TIMEOUT) == RT_EOK)
        {
            rt_kprintf("[APP] WiFi connected. Starting MQTT...\n");
            mqtt_start();
        }
        else
        {
            rt_kprintf("[APP] Failed to get IP address.\n");
        }
    }
    else
    {
        rt_kprintf("[APP] WiFi connect failed.\n");
    }

    rt_wlan_unregister_event_handler(RT_WLAN_EVT_READY);
    rt_sem_delete(net_ready_sem);
}

MSH_CMD_EXPORT(start_network_service, connect WiFi and start MQTT);
int main(void)
{
    return 0;
}
