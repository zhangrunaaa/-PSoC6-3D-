#ifndef MQTT_PUBLISH_H__
#define MQTT_PUBLISH_H__

int mqtt_start(void);
int mqtt_publish_message(const char *topic, const char *msg);

#endif
