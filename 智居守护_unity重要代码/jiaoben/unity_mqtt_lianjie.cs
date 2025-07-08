using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public class MQTTManager : MonoBehaviour
{
    private IMqttClient _mqttClient;
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

    [Header("Connection Settings")]
    public string brokerAddress = "broker.emqx.io"; // EMQX 公共服务器
    public int brokerPort = 1883;
    public string clientId = "gdcaaa44"; // 使用截图中的客户端ID

    [Header("Topic Settings")]
    public string subscribeTopic = "esp32/environment"; // 订阅主题
    public string publishTopic = "esp32/environment"; // 发布主题

    async void Start()
    {
        await InitializeMQTT();
    }

    private async Task InitializeMQTT()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // 无密码连接选项
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithClientId(clientId)
            .WithCleanSession()
            .WithKeepAlivePeriod(System.TimeSpan.FromSeconds(60)) // Keep Alive 60秒
            .Build();

        _mqttClient.ConnectedAsync += HandleConnected;
        _mqttClient.DisconnectedAsync += HandleDisconnected;
        _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceived;

        try
        {
            await _mqttClient.ConnectAsync(options);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MQTT连接失败: {ex.Message}");
        }
    }

    private Task HandleConnected(MqttClientConnectedEventArgs arg)
    {
        Debug.Log("MQTT已连接!");
        SubscribeToTopics();
        return Task.CompletedTask;
    }

    private async Task HandleDisconnected(MqttClientDisconnectedEventArgs arg)
    {
        Debug.LogWarning($"MQTT断开连接: {arg.Reason}");

        if (!_mqttClient.IsConnected)
        {
            await Task.Delay(5000);
            try
            {
                await _mqttClient.ReconnectAsync();
            }
            catch
            {
                Debug.LogWarning("尝试重连失败...");
            }
        }
    }

    private Task HandleMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
    {
        var payload = arg.ApplicationMessage.PayloadSegment;
        var message = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);

        // 将消息加入队列（线程安全）
        _messageQueue.Enqueue($"[{arg.ApplicationMessage.Topic}] {message}");
        return Task.CompletedTask;
    }

    private async void SubscribeToTopics()
    {
        if (_mqttClient.IsConnected)
        {
            try
            {
                // 使用 QoS 0 级别（截图中的设置）
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(subscribeTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce) // QoS 0
                    .Build());

                Debug.Log($"已订阅主题: {subscribeTopic} (QoS 0)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"订阅失败: {ex.Message}");
            }
        }
    }

    void Update()
    {
        while (_messageQueue.TryDequeue(out var msg))
        {
            Debug.Log($"收到消息: {msg}");

            // 这里可以添加处理 JSON 消息的逻辑
            // 示例消息：{"msg": "hello wangqiang"}
        }
    }

    // 发布消息方法
    public async void Publish(string message, bool retain = false)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected) return;

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(publishTopic)
            .WithPayload(message)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce) // QoS 0
            .WithRetainFlag(retain)
            .Build();

        try
        {
            await _mqttClient.PublishAsync(msg);
            Debug.Log($"已发布消息: {message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"发布失败: {ex.Message}");
        }
    }

    // 发布 JSON 消息方法
    public async void PublishJson(string jsonMessage)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected) return;

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(publishTopic)
            .WithPayload(jsonMessage)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        try
        {
            await _mqttClient.PublishAsync(msg);
            Debug.Log($"已发布JSON消息: {jsonMessage}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"发布JSON失败: {ex.Message}");
        }
    }

    async void OnDestroy()
    {
        if (_mqttClient != null)
        {
            await _mqttClient.UnsubscribeAsync(subscribeTopic);

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
            }

            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }
}