#include <rtthread.h>
#include <string.h>
#include "paho_mqtt.h"
#include "mqtt_publish.h"

#define MQTT_BROKER_URI   "tcp://broker.emqx.io:1883"
#define MQTT_USERNAME     "emqx"
#define MQTT_PASSWORD     "public"

#define MQTT_PUB_TOPIC    "ld6002/fall_status"

MQTTClient client;
rt_sem_t mqtt_ready_sem = RT_NULL;

static void mqtt_sub_callback(MQTTClient *c, MessageData *data)
{
    ((char *)data->message->payload)[data->message->payloadlen] = '\0';
    rt_kprintf("MQTT recv: topic: %.*s, message: %.*s\n",
               data->topicName->lenstring.len, data->topicName->lenstring.data,
               data->message->payloadlen, (char *)data->message->payload);
}

int mqtt_publish_message(const char *topic, const char *msg)
{
    if (client.isconnected)
    {
        return paho_mqtt_publish(&client, QOS1, topic, msg);
    }
    else
    {
        rt_kprintf("MQTT not connected, publish failed\n");
        return -1;
    }
}

static void mqtt_thread_entry(void *param)
{
    char client_id[24];
    rt_snprintf(client_id, sizeof(client_id), "rtt_%d", rt_tick_get());

    MQTTPacket_connectData connectData = MQTTPacket_connectData_initializer;
    connectData.clientID.cstring = client_id;
    connectData.keepAliveInterval = 30;
    connectData.cleansession = 1;
    connectData.username.cstring = MQTT_USERNAME;
    connectData.password.cstring = MQTT_PASSWORD;

    client.uri = MQTT_BROKER_URI;
    client.condata = connectData;
    client.isconnected = 0;
    client.buf_size = client.readbuf_size = 1024;
    client.buf = rt_calloc(1, client.buf_size);
    client.readbuf = rt_calloc(1, client.readbuf_size);

    if (!(client.buf && client.readbuf))
    {
        rt_kprintf("No memory for MQTT buffers\n");
        return;
    }

    client.messageHandlers[0].topicFilter = "ld6002/command";
    client.messageHandlers[0].callback = mqtt_sub_callback;
    client.messageHandlers[0].qos = QOS1;

    paho_mqtt_start(&client);

    // 连接成功后释放信号量
    while (!client.isconnected)
    {
        rt_thread_mdelay(500);
    }

    rt_kprintf("[MQTT] Connected to broker.\n");

    if (mqtt_ready_sem)
        rt_sem_release(mqtt_ready_sem);
}

int mqtt_start(void)
{
    mqtt_ready_sem = rt_sem_create("mqtt_ready", 0, RT_IPC_FLAG_FIFO);
    rt_thread_t tid = rt_thread_create("mqtt", mqtt_thread_entry, RT_NULL,
                                       2048, 20, 10);
    if (tid)
        rt_thread_startup(tid);

    return 0;
}
