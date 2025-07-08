using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class MQTTPlayerController : MonoBehaviour
{
    [Header("MQTT Settings")]
    public string brokerAddress = "broker.emqx.io"; // 使用图片中的Broker地址
    public int brokerPort = 1883;                   // 使用图片中的端口
    public string clientId = "gdcaaa44";            // 使用图片中的客户端ID
    public string topic = "esp32/environment";      // 使用图片中的订阅主题

    [Header("Movement Settings")]
    public float moveDistance = 1f;
    public float moveSpeed = 2f;
    public LayerMask groundLayer;
    public float groundCheckDistance = 1.1f;

    private CharacterController characterController;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private IMqttClient mqttClient;
    private bool isConnected = false;

    async void Start()
    {
        characterController = GetComponent<CharacterController>();
        targetPosition = transform.position;

        // 创建MQTT客户端工厂
        var factory = new MqttFactory();

        // 创建MQTT客户端
        mqttClient = factory.CreateMqttClient();

        // 设置事件处理程序
        mqttClient.ConnectedAsync += HandleConnected;
        mqttClient.DisconnectedAsync += HandleDisconnected;
        mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceived;

        // 配置客户端选项 - 使用图片中的配置
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)  // 固定客户端ID
            .WithCleanSession()
            .WithTimeout(System.TimeSpan.FromSeconds(5)) // 添加超时设置
            .Build();

        // 连接代理
        try
        {
            Debug.Log($"正在连接到MQTT: {brokerAddress}:{brokerPort}");
            var connectResult = await mqttClient.ConnectAsync(options);

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Debug.Log("MQTT连接成功");
                isConnected = true;
            }
            else
            {
                Debug.LogError($"连接失败: {connectResult.ResultCode}");
                isConnected = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MQTT连接失败: {ex.Message}");
            isConnected = false;
            // 3秒后尝试重新连接
            await Task.Delay(3000);
            StartCoroutine(TryReconnectAfterDelay());
        }
    }

    private Task HandleConnected(MqttClientConnectedEventArgs args)
    {
        Debug.Log("MQTT连接成功");
        isConnected = true;

        // 订阅主题 - 使用图片中的主题
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        return mqttClient.SubscribeAsync(subscribeOptions);
    }

    private Task HandleDisconnected(MqttClientDisconnectedEventArgs args)
    {
        Debug.Log($"MQTT连接断开: {args.Reason}");
        isConnected = false;
        // 立即尝试重新连接
        StartCoroutine(TryReconnectAfterDelay());
        return Task.CompletedTask;
    }

    private IEnumerator TryReconnectAfterDelay()
    {
        yield return new WaitForSeconds(3);
        TryReconnect();
    }

    private Task HandleMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        string message = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
        UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessMQTTMessage(message));
        return Task.CompletedTask;
    }

    void Update()
    {
        // 平滑移动到目标位置
        if (isMoving)
        {
            Vector3 moveDirection = (targetPosition - transform.position).normalized;
            float step = moveSpeed * Time.deltaTime;

            // 使用CharacterController移动
            characterController.Move(moveDirection * step);

            // 检查是否到达目标位置
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                isMoving = false;
                Debug.Log("移动完成");
            }
        }
    }

    async void TryReconnect()
    {
        if (isConnected) return;

        Debug.Log("尝试重新连接MQTT...");

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        try
        {
            var result = await mqttClient.ConnectAsync(options);
            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                Debug.Log("MQTT重新连接成功");
                isConnected = true;
            }
            else
            {
                Debug.LogWarning($"重新连接失败: {result.ResultCode}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"重新连接失败: {ex.Message}");
        }
    }

    private void ProcessMQTTMessage(string message)
    {
        message = message.Trim().ToLower();

        Debug.Log($"收到指令: {message}");

        switch (message)
        {
            case "w":
                MovePlayer(transform.forward);
                break;
            case "s":
                MovePlayer(-transform.forward);
                break;
            case "a":
                MovePlayer(-transform.right);
                break;
            case "d":
                MovePlayer(transform.right);
                break;
            default:
                Debug.LogWarning($"未知指令: {message}");
                break;
        }
    }

    private void MovePlayer(Vector3 direction)
    {
        // 计算目标位置
        Vector3 newPosition = transform.position + direction.normalized * moveDistance;

        // 检查目标位置是否有效
        if (CanMoveTo(newPosition))
        {
            targetPosition = newPosition;
            isMoving = true;
            Debug.Log($"移动到: {targetPosition}");
        }
        else
        {
            Debug.LogWarning($"无法移动到位置: {newPosition}");
        }
    }

    private bool CanMoveTo(Vector3 position)
    {
        // 使用射线检测目标位置是否在地面上
        return Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        // 绘制目标位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.2f);

        // 绘制移动方向
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }

    async void OnDestroy()
    {
        // 断开MQTT连接
        if (mqttClient != null && mqttClient.IsConnected)
        {
            await mqttClient.DisconnectAsync();
        }
    }
}

// 主线程调度器 - 确保在场景中创建此组件
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private static readonly Queue<System.Action> executionQueue = new Queue<System.Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (instance == null)
        {
            var obj = new GameObject("MainThreadDispatcher");
            instance = obj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }
        return instance;
    }

    public void Enqueue(System.Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }
}