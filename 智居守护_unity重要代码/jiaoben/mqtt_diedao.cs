using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;

[RequireComponent(typeof(Rigidbody))]
public class MQTTFallController : MonoBehaviour
{
    [Header("MQTT Connection Settings")]
    public string brokerAddress = "broker.emqx.io";
    public int brokerPort = 1883;
    public string clientId = "gdcaaa44";
    public string subscribeTopic = "esp32/environment";

    [Header("Fall Settings")]
    public Vector3 fallRotation = new Vector3(90f, 0f, 0f);
    public Vector3 fallPositionOffset = new Vector3(0f, -1f, 0f);
    public float fallDuration = 0.2f;

    [Header("Physics Settings")]
    public bool usePhysics = true;
    public float fallForce = 8f;

    [Header("Debug")]
    public bool debugMode = false;

    private IMqttClient _mqttClient;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _isFalling = false;
    private float _fallProgress = 0f;
    private Rigidbody _rb;

    // 用于线程安全的命令队列
    private readonly ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();

    void Start()
    {
        // 保存原始姿态
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;

        // 获取刚体组件
        _rb = GetComponent<Rigidbody>();

        // 初始化MQTT连接
        InitializeMQTT();

        Debug.Log("MQTT跌倒控制器已启动");
    }

    async void InitializeMQTT()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // 配置MQTT选项
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        // 连接成功回调
        _mqttClient.ConnectedAsync += async e =>
        {
            Debug.Log($"MQTT已连接! 服务器: {brokerAddress}:{brokerPort}");

            // 订阅主题
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(subscribeTopic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            Debug.Log($"已订阅主题: {subscribeTopic}");
        };

        // 消息接收回调
        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                // 安全检查：确保消息有效
                if (e.ApplicationMessage.Payload == null ||
                    e.ApplicationMessage.Payload.Length == 0)
                {
                    if (debugMode) Debug.LogWarning("收到空消息，已忽略");
                    return Task.CompletedTask;
                }

                // 解析消息内容
                var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                if (debugMode)
                {
                    Debug.Log($"收到消息: [{e.ApplicationMessage.Topic}] {message}");
                }

                // 将命令加入队列（线程安全）
                _commandQueue.Enqueue(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"消息处理错误: {ex.Message}");
            }
            return Task.CompletedTask;
        };

        // 断开连接回调
        _mqttClient.DisconnectedAsync += e =>
        {
            Debug.LogWarning("MQTT连接断开，尝试重新连接...");
            Task.Delay(5000).ContinueWith(t => InitializeMQTT());
            return Task.CompletedTask;
        };

        try
        {
            // 尝试连接
            await _mqttClient.ConnectAsync(options);
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT连接失败: {ex.Message}");

            // 连接失败后尝试重新连接
            Task.Delay(5000).ContinueWith(t => InitializeMQTT());
        }
    }

    void Update()
    {
        // 处理命令队列中的命令（主线程安全）
        while (_commandQueue.TryDequeue(out var command))
        {
            ProcessCommand(command);
        }

        // 跌倒/站立过渡
        if (_isFalling && _fallProgress < 1f)
        {
            _fallProgress += Time.deltaTime / fallDuration;
            if (_fallProgress > 1f) _fallProgress = 1f;

            // 如果不使用物理模拟，应用平滑过渡
            if (!usePhysics)
            {
                ApplyFallPose(_fallProgress);
            }
        }
        else if (!_isFalling && _fallProgress < 1f)
        {
            _fallProgress += Time.deltaTime / fallDuration;
            if (_fallProgress > 1f) _fallProgress = 1f;

            // 恢复站立姿态
            ApplyStandPose(_fallProgress);

            // 恢复后重新启用物理（如果是使用物理模拟）
            if (_fallProgress >= 1f && usePhysics && _rb != null)
            {
                _rb.isKinematic = false;
            }
        }
    }

    void ProcessCommand(string command)
    {
        try
        {
            // 清理命令字符串
            command = command.Trim().ToLower();

            if (command.Contains("fall") || command.Contains("跌倒"))
            {
                StartFalling();
            }
            else if (command.Contains("stand") || command.Contains("站立"))
            {
                StandUp();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"命令处理错误: {ex.Message}");
        }
    }

    void StartFalling()
    {
        if (!_isFalling)
        {
            _isFalling = true;
            _fallProgress = 0f;

            if (debugMode) Debug.Log("触发跌倒");

            // 物理模拟跌倒
            if (usePhysics && _rb != null)
            {
                // 确保刚体是动态的
                _rb.isKinematic = false;

                // 添加向前的力使模型跌倒
                _rb.AddForce(transform.forward * fallForce, ForceMode.Impulse);

                // 添加旋转扭矩
                _rb.AddTorque(transform.right * fallForce * 5f, ForceMode.Impulse);
            }
        }
    }

    void StandUp()
    {
        if (_isFalling)
        {
            _isFalling = false;
            _fallProgress = 0f;

            if (debugMode) Debug.Log("恢复站立");

            // 重置物理状态
            if (usePhysics && _rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                // 暂时设为运动学以平滑恢复
                _rb.isKinematic = true;
            }
        }
    }

    void ApplyFallPose(float progress)
    {
        // 计算跌倒姿态
        Quaternion targetRotation = Quaternion.Euler(fallRotation);
        Vector3 targetPosition = _originalPosition + fallPositionOffset;

        // 应用过渡
        transform.rotation = Quaternion.Slerp(_originalRotation, targetRotation, progress);
        transform.position = Vector3.Lerp(_originalPosition, targetPosition, progress);
    }

    void ApplyStandPose(float progress)
    {
        // 计算当前跌倒姿态（作为过渡起点）
        Quaternion fallRotation = Quaternion.Euler(this.fallRotation);
        Vector3 fallPosition = _originalPosition + fallPositionOffset;

        // 应用过渡回站立状态
        transform.rotation = Quaternion.Slerp(fallRotation, _originalRotation, progress);
        transform.position = Vector3.Lerp(fallPosition, _originalPosition, progress);
    }

    void OnDestroy()
    {
        DisconnectMQTT();
    }

    async void DisconnectMQTT()
    {
        if (_mqttClient != null)
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            _mqttClient.Dispose();
        }
    }

    [ContextMenu("测试跌倒")]
    public void TestFall()
    {
        StartFalling();
    }

    [ContextMenu("测试站立")]
    public void TestStand()
    {
        StandUp();
    }
}