using UnityEngine;
using UnityEngine.UI;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Concurrent;
using System;

public class MQTTAlertPanel : MonoBehaviour
{
    [Header("Animation Settings")]
    public AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float animationSpeed = 2f;
    public float displayDuration = 5f;

    [Header("UI References")]
    public GameObject panel;
    public Text alertText;
    public string fallMessage = "���棬���˵�����";

    [Header("MQTT Settings")]
    public string brokerAddress = "broker.emqx.io";
    public int brokerPort = 1883;
    public string clientId = "UnityAlertClient";
    public string subscribeTopic = "ld6002/fall_status";

    private IMqttClient _mqttClient;
    private readonly ConcurrentQueue<bool> _alertQueue = new ConcurrentQueue<bool>();
    private Coroutine _currentAnimation;
    private bool _isPanelVisible = false;
    private bool _isConnected = false;

    async void Start()
    {
        Debug.Log("MQTT��������ʼ����ʼ");

        // ��ʼ�����
        if (panel != null)
        {
            panel.transform.localScale = Vector3.zero;
            panel.SetActive(false);
            Debug.Log("��������ѳ�ʼ��");
        }
        else
        {
            Debug.LogError("����: ������δ����!");
        }

        // �����Զ������ı����
        if (alertText == null && panel != null)
        {
            alertText = panel.GetComponentInChildren<Text>();
            if (alertText != null) Debug.Log("���Զ��ҵ������ı����");
        }

        // ��ʼ��MQTT����
        await InitializeMQTT();
    }

    async Task InitializeMQTT()
    {
        Debug.Log("��ʼ��ʼ��MQTT����...");

        try
        {
            // ����MQTT�����Ϳͻ���
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // ����MQTTѡ��
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerAddress, brokerPort)
                .WithClientId(clientId + Guid.NewGuid().ToString().Substring(0, 8))
                .WithCleanSession()
                .Build();

            Debug.Log($"�������ӵ�MQTT������: {brokerAddress}:{brokerPort}");

            // ע���첽�¼�������
            _mqttClient.ConnectedAsync += HandleConnectedAsync;
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceivedAsync;

            // ��������
            Debug.Log("�������ӷ�����...");
            var result = await _mqttClient.ConnectAsync(options);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                Debug.Log("MQTT���ӳɹ�");
                _isConnected = true;

                // ��������
                await SubscribeToTopic();
            }
            else
            {
                Debug.LogError($"����ʧ��: {result.ResultCode} - {result.ReasonString}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT��ʼ��ʧ��: {ex.Message}");
            // 3�������������
            await Task.Delay(3000);
            Debug.Log("������������...");
            await InitializeMQTT();
        }
    }

    private async Task SubscribeToTopic()
    {
        try
        {
            Debug.Log($"���ڶ�������: {subscribeTopic}");

            // ��������ѡ��
            var subscribeOptions = new MqttClientSubscribeOptions();
            subscribeOptions.TopicFilters = new System.Collections.Generic.List<MQTTnet.Packets.MqttTopicFilter>
            {
                new MQTTnet.Packets.MqttTopicFilter
                {
                    Topic = subscribeTopic,
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce
                }
            };

            var result = await _mqttClient.SubscribeAsync(subscribeOptions);

            if (result.Items.Count > 0)
            {
                Debug.Log($"�ѳɹ���������: {subscribeTopic}");
            }
            else
            {
                Debug.LogError("����ʧ��: �޷��ؽ��");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"����ʧ��: {ex.Message}");
        }
    }

    // ���ӳɹ�����
    private async Task HandleConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        Debug.Log($"MQTT���ӳɹ�");
        _isConnected = true;
        await SubscribeToTopic();
    }

    // �Ͽ����Ӵ���
    private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        Debug.LogWarning($"MQTT���ӶϿ�");
        _isConnected = false;

        // 5�������������
        await Task.Delay(5000);
        Debug.Log("������������...");
        await InitializeMQTT();
    }

    // ��Ϣ���մ���
    private Task HandleMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        try
        {
            // ������Ϣ����
            var payload = arg.ApplicationMessage.PayloadSegment;
            if (payload.Count == 0) return Task.CompletedTask;

            var message = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
            Debug.Log($"�յ���Ϣ: {message}");

            // �򵥼���Ƿ����"fall"�ؼ���
            if (message.IndexOf("fall", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log("��⵽������Ϣ����������");
                _alertQueue.Enqueue(true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"��Ϣ�������: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    void Update()
    {
        // ����������
        while (_alertQueue.TryDequeue(out _))
        {
            TriggerAlert();
        }
    }

    // ��������
    public void TriggerAlert()
    {
        if (panel == null) return;

        // �������Ѿ��ɼ�����ֹͣ��ǰ����
        if (_isPanelVisible && _currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }

        // ��ʾ�������
        _currentAnimation = StartCoroutine(ShowPanel());
    }

    // ��ʾ��嶯��
    IEnumerator ShowPanel()
    {
        _isPanelVisible = true;
        panel.SetActive(true);

        // ���þ����ı�
        if (alertText != null)
        {
            alertText.text = fallMessage;
        }

        // �Ŵ󶯻�
        float timer = 0;
        while (timer <= 1)
        {
            float scale = showCurve.Evaluate(timer);
            panel.transform.localScale = Vector3.one * scale;
            timer += Time.deltaTime * animationSpeed;
            yield return null;
        }

        panel.transform.localScale = Vector3.one;
        yield return new WaitForSeconds(displayDuration);

        // ��ʼ��С����
        _currentAnimation = StartCoroutine(HidePanel());
    }

    // ������嶯��
    IEnumerator HidePanel()
    {
        float timer = 0;
        while (timer <= 1)
        {
            float scale = hideCurve.Evaluate(timer);
            panel.transform.localScale = Vector3.one * scale;
            timer += Time.deltaTime * animationSpeed;
            yield return null;
        }

        panel.transform.localScale = Vector3.zero;
        panel.SetActive(false);
        _isPanelVisible = false;
    }

    async void OnDestroy()
    {
        // ����MQTT����
        if (_mqttClient != null)
        {
            // ȡ���¼�ע��
            _mqttClient.ConnectedAsync -= HandleConnectedAsync;
            _mqttClient.DisconnectedAsync -= HandleDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync -= HandleMessageReceivedAsync;

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            _mqttClient.Dispose();
        }
    }

    // ���Է���
    [ContextMenu("���Ծ������")]
    public void TestAlert()
    {
        TriggerAlert();
    }

    // ����Ļ����ʾ״̬
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        // ����Ļ���Ͻ���ʾ����״̬
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = _isConnected ? Color.green : Color.red;

        GUI.Label(new Rect(10, 10, 300, 30),
                 $"MQTT״̬: {(_isConnected ? "������" : "δ����")}", style);
    }
}