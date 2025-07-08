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
    public string brokerAddress = "broker.emqx.io"; // EMQX ����������
    public int brokerPort = 1883;
    public string clientId = "gdcaaa44"; // ʹ�ý�ͼ�еĿͻ���ID

    [Header("Topic Settings")]
    public string subscribeTopic = "esp32/environment"; // ��������
    public string publishTopic = "esp32/environment"; // ��������

    async void Start()
    {
        await InitializeMQTT();
    }

    private async Task InitializeMQTT()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // ����������ѡ��
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithClientId(clientId)
            .WithCleanSession()
            .WithKeepAlivePeriod(System.TimeSpan.FromSeconds(60)) // Keep Alive 60��
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
            Debug.LogError($"MQTT����ʧ��: {ex.Message}");
        }
    }

    private Task HandleConnected(MqttClientConnectedEventArgs arg)
    {
        Debug.Log("MQTT������!");
        SubscribeToTopics();
        return Task.CompletedTask;
    }

    private async Task HandleDisconnected(MqttClientDisconnectedEventArgs arg)
    {
        Debug.LogWarning($"MQTT�Ͽ�����: {arg.Reason}");

        if (!_mqttClient.IsConnected)
        {
            await Task.Delay(5000);
            try
            {
                await _mqttClient.ReconnectAsync();
            }
            catch
            {
                Debug.LogWarning("��������ʧ��...");
            }
        }
    }

    private Task HandleMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
    {
        var payload = arg.ApplicationMessage.PayloadSegment;
        var message = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);

        // ����Ϣ������У��̰߳�ȫ��
        _messageQueue.Enqueue($"[{arg.ApplicationMessage.Topic}] {message}");
        return Task.CompletedTask;
    }

    private async void SubscribeToTopics()
    {
        if (_mqttClient.IsConnected)
        {
            try
            {
                // ʹ�� QoS 0 ���𣨽�ͼ�е����ã�
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(subscribeTopic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce) // QoS 0
                    .Build());

                Debug.Log($"�Ѷ�������: {subscribeTopic} (QoS 0)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"����ʧ��: {ex.Message}");
            }
        }
    }

    void Update()
    {
        while (_messageQueue.TryDequeue(out var msg))
        {
            Debug.Log($"�յ���Ϣ: {msg}");

            // ���������Ӵ��� JSON ��Ϣ���߼�
            // ʾ����Ϣ��{"msg": "hello wangqiang"}
        }
    }

    // ������Ϣ����
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
            Debug.Log($"�ѷ�����Ϣ: {message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"����ʧ��: {ex.Message}");
        }
    }

    // ���� JSON ��Ϣ����
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
            Debug.Log($"�ѷ���JSON��Ϣ: {jsonMessage}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"����JSONʧ��: {ex.Message}");
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