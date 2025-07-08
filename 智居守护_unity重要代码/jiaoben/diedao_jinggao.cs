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
    public string fallMessage = "警告，有人跌倒！";

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
        Debug.Log("MQTT警报面板初始化开始");

        // 初始化面板
        if (panel != null)
        {
            panel.transform.localScale = Vector3.zero;
            panel.SetActive(false);
            Debug.Log("警告面板已初始化");
        }
        else
        {
            Debug.LogError("警告: 面板对象未分配!");
        }

        // 尝试自动查找文本组件
        if (alertText == null && panel != null)
        {
            alertText = panel.GetComponentInChildren<Text>();
            if (alertText != null) Debug.Log("已自动找到警告文本组件");
        }

        // 初始化MQTT连接
        await InitializeMQTT();
    }

    async Task InitializeMQTT()
    {
        Debug.Log("开始初始化MQTT连接...");

        try
        {
            // 创建MQTT工厂和客户端
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // 配置MQTT选项
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerAddress, brokerPort)
                .WithClientId(clientId + Guid.NewGuid().ToString().Substring(0, 8))
                .WithCleanSession()
                .Build();

            Debug.Log($"尝试连接到MQTT服务器: {brokerAddress}:{brokerPort}");

            // 注册异步事件处理器
            _mqttClient.ConnectedAsync += HandleConnectedAsync;
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceivedAsync;

            // 尝试连接
            Debug.Log("正在连接服务器...");
            var result = await _mqttClient.ConnectAsync(options);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                Debug.Log("MQTT连接成功");
                _isConnected = true;

                // 订阅主题
                await SubscribeToTopic();
            }
            else
            {
                Debug.LogError($"连接失败: {result.ResultCode} - {result.ReasonString}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT初始化失败: {ex.Message}");
            // 3秒后尝试重新连接
            await Task.Delay(3000);
            Debug.Log("尝试重新连接...");
            await InitializeMQTT();
        }
    }

    private async Task SubscribeToTopic()
    {
        try
        {
            Debug.Log($"正在订阅主题: {subscribeTopic}");

            // 创建订阅选项
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
                Debug.Log($"已成功订阅主题: {subscribeTopic}");
            }
            else
            {
                Debug.LogError("订阅失败: 无返回结果");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"订阅失败: {ex.Message}");
        }
    }

    // 连接成功处理
    private async Task HandleConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        Debug.Log($"MQTT连接成功");
        _isConnected = true;
        await SubscribeToTopic();
    }

    // 断开连接处理
    private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        Debug.LogWarning($"MQTT连接断开");
        _isConnected = false;

        // 5秒后尝试重新连接
        await Task.Delay(5000);
        Debug.Log("尝试重新连接...");
        await InitializeMQTT();
    }

    // 消息接收处理
    private Task HandleMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        try
        {
            // 解析消息内容
            var payload = arg.ApplicationMessage.PayloadSegment;
            if (payload.Count == 0) return Task.CompletedTask;

            var message = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
            Debug.Log($"收到消息: {message}");

            // 简单检查是否包含"fall"关键词
            if (message.IndexOf("fall", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log("检测到跌倒消息，触发警报");
                _alertQueue.Enqueue(true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"消息处理错误: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    void Update()
    {
        // 处理警报队列
        while (_alertQueue.TryDequeue(out _))
        {
            TriggerAlert();
        }
    }

    // 触发警告
    public void TriggerAlert()
    {
        if (panel == null) return;

        // 如果面板已经可见，先停止当前动画
        if (_isPanelVisible && _currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }

        // 显示警告面板
        _currentAnimation = StartCoroutine(ShowPanel());
    }

    // 显示面板动画
    IEnumerator ShowPanel()
    {
        _isPanelVisible = true;
        panel.SetActive(true);

        // 设置警告文本
        if (alertText != null)
        {
            alertText.text = fallMessage;
        }

        // 放大动画
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

        // 开始缩小动画
        _currentAnimation = StartCoroutine(HidePanel());
    }

    // 隐藏面板动画
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
        // 清理MQTT连接
        if (_mqttClient != null)
        {
            // 取消事件注册
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

    // 测试方法
    [ContextMenu("测试警告面板")]
    public void TestAlert()
    {
        TriggerAlert();
    }

    // 在屏幕上显示状态
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        // 在屏幕左上角显示连接状态
        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = _isConnected ? Color.green : Color.red;

        GUI.Label(new Rect(10, 10, 300, 30),
                 $"MQTT状态: {(_isConnected ? "已连接" : "未连接")}", style);
    }
}