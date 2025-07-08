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
    public string brokerAddress = "broker.emqx.io"; // ʹ��ͼƬ�е�Broker��ַ
    public int brokerPort = 1883;                   // ʹ��ͼƬ�еĶ˿�
    public string clientId = "gdcaaa44";            // ʹ��ͼƬ�еĿͻ���ID
    public string topic = "esp32/environment";      // ʹ��ͼƬ�еĶ�������

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

        // ����MQTT�ͻ��˹���
        var factory = new MqttFactory();

        // ����MQTT�ͻ���
        mqttClient = factory.CreateMqttClient();

        // �����¼��������
        mqttClient.ConnectedAsync += HandleConnected;
        mqttClient.DisconnectedAsync += HandleDisconnected;
        mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceived;

        // ���ÿͻ���ѡ�� - ʹ��ͼƬ�е�����
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)  // �̶��ͻ���ID
            .WithCleanSession()
            .WithTimeout(System.TimeSpan.FromSeconds(5)) // ��ӳ�ʱ����
            .Build();

        // ���Ӵ���
        try
        {
            Debug.Log($"�������ӵ�MQTT: {brokerAddress}:{brokerPort}");
            var connectResult = await mqttClient.ConnectAsync(options);

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Debug.Log("MQTT���ӳɹ�");
                isConnected = true;
            }
            else
            {
                Debug.LogError($"����ʧ��: {connectResult.ResultCode}");
                isConnected = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MQTT����ʧ��: {ex.Message}");
            isConnected = false;
            // 3�������������
            await Task.Delay(3000);
            StartCoroutine(TryReconnectAfterDelay());
        }
    }

    private Task HandleConnected(MqttClientConnectedEventArgs args)
    {
        Debug.Log("MQTT���ӳɹ�");
        isConnected = true;

        // �������� - ʹ��ͼƬ�е�����
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        return mqttClient.SubscribeAsync(subscribeOptions);
    }

    private Task HandleDisconnected(MqttClientDisconnectedEventArgs args)
    {
        Debug.Log($"MQTT���ӶϿ�: {args.Reason}");
        isConnected = false;
        // ����������������
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
        // ƽ���ƶ���Ŀ��λ��
        if (isMoving)
        {
            Vector3 moveDirection = (targetPosition - transform.position).normalized;
            float step = moveSpeed * Time.deltaTime;

            // ʹ��CharacterController�ƶ�
            characterController.Move(moveDirection * step);

            // ����Ƿ񵽴�Ŀ��λ��
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                isMoving = false;
                Debug.Log("�ƶ����");
            }
        }
    }

    async void TryReconnect()
    {
        if (isConnected) return;

        Debug.Log("������������MQTT...");

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
                Debug.Log("MQTT�������ӳɹ�");
                isConnected = true;
            }
            else
            {
                Debug.LogWarning($"��������ʧ��: {result.ResultCode}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"��������ʧ��: {ex.Message}");
        }
    }

    private void ProcessMQTTMessage(string message)
    {
        message = message.Trim().ToLower();

        Debug.Log($"�յ�ָ��: {message}");

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
                Debug.LogWarning($"δָ֪��: {message}");
                break;
        }
    }

    private void MovePlayer(Vector3 direction)
    {
        // ����Ŀ��λ��
        Vector3 newPosition = transform.position + direction.normalized * moveDistance;

        // ���Ŀ��λ���Ƿ���Ч
        if (CanMoveTo(newPosition))
        {
            targetPosition = newPosition;
            isMoving = true;
            Debug.Log($"�ƶ���: {targetPosition}");
        }
        else
        {
            Debug.LogWarning($"�޷��ƶ���λ��: {newPosition}");
        }
    }

    private bool CanMoveTo(Vector3 position)
    {
        // ʹ�����߼��Ŀ��λ���Ƿ��ڵ�����
        return Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        // ����Ŀ��λ��
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.2f);

        // �����ƶ�����
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }

    async void OnDestroy()
    {
        // �Ͽ�MQTT����
        if (mqttClient != null && mqttClient.IsConnected)
        {
            await mqttClient.DisconnectAsync();
        }
    }
}

// ���̵߳����� - ȷ���ڳ����д��������
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