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

    // �����̰߳�ȫ���������
    private readonly ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();

    void Start()
    {
        // ����ԭʼ��̬
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;

        // ��ȡ�������
        _rb = GetComponent<Rigidbody>();

        // ��ʼ��MQTT����
        InitializeMQTT();

        Debug.Log("MQTT����������������");
    }

    async void InitializeMQTT()
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // ����MQTTѡ��
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        // ���ӳɹ��ص�
        _mqttClient.ConnectedAsync += async e =>
        {
            Debug.Log($"MQTT������! ������: {brokerAddress}:{brokerPort}");

            // ��������
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(subscribeTopic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            Debug.Log($"�Ѷ�������: {subscribeTopic}");
        };

        // ��Ϣ���ջص�
        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            try
            {
                // ��ȫ��飺ȷ����Ϣ��Ч
                if (e.ApplicationMessage.Payload == null ||
                    e.ApplicationMessage.Payload.Length == 0)
                {
                    if (debugMode) Debug.LogWarning("�յ�����Ϣ���Ѻ���");
                    return Task.CompletedTask;
                }

                // ������Ϣ����
                var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                if (debugMode)
                {
                    Debug.Log($"�յ���Ϣ: [{e.ApplicationMessage.Topic}] {message}");
                }

                // �����������У��̰߳�ȫ��
                _commandQueue.Enqueue(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"��Ϣ�������: {ex.Message}");
            }
            return Task.CompletedTask;
        };

        // �Ͽ����ӻص�
        _mqttClient.DisconnectedAsync += e =>
        {
            Debug.LogWarning("MQTT���ӶϿ���������������...");
            Task.Delay(5000).ContinueWith(t => InitializeMQTT());
            return Task.CompletedTask;
        };

        try
        {
            // ��������
            await _mqttClient.ConnectAsync(options);
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT����ʧ��: {ex.Message}");

            // ����ʧ�ܺ�����������
            Task.Delay(5000).ContinueWith(t => InitializeMQTT());
        }
    }

    void Update()
    {
        // ������������е�������̰߳�ȫ��
        while (_commandQueue.TryDequeue(out var command))
        {
            ProcessCommand(command);
        }

        // ����/վ������
        if (_isFalling && _fallProgress < 1f)
        {
            _fallProgress += Time.deltaTime / fallDuration;
            if (_fallProgress > 1f) _fallProgress = 1f;

            // �����ʹ������ģ�⣬Ӧ��ƽ������
            if (!usePhysics)
            {
                ApplyFallPose(_fallProgress);
            }
        }
        else if (!_isFalling && _fallProgress < 1f)
        {
            _fallProgress += Time.deltaTime / fallDuration;
            if (_fallProgress > 1f) _fallProgress = 1f;

            // �ָ�վ����̬
            ApplyStandPose(_fallProgress);

            // �ָ��������������������ʹ������ģ�⣩
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
            // ���������ַ���
            command = command.Trim().ToLower();

            if (command.Contains("fall") || command.Contains("����"))
            {
                StartFalling();
            }
            else if (command.Contains("stand") || command.Contains("վ��"))
            {
                StandUp();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"��������: {ex.Message}");
        }
    }

    void StartFalling()
    {
        if (!_isFalling)
        {
            _isFalling = true;
            _fallProgress = 0f;

            if (debugMode) Debug.Log("��������");

            // ����ģ�����
            if (usePhysics && _rb != null)
            {
                // ȷ�������Ƕ�̬��
                _rb.isKinematic = false;

                // �����ǰ����ʹģ�͵���
                _rb.AddForce(transform.forward * fallForce, ForceMode.Impulse);

                // �����תŤ��
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

            if (debugMode) Debug.Log("�ָ�վ��");

            // ��������״̬
            if (usePhysics && _rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                // ��ʱ��Ϊ�˶�ѧ��ƽ���ָ�
                _rb.isKinematic = true;
            }
        }
    }

    void ApplyFallPose(float progress)
    {
        // ���������̬
        Quaternion targetRotation = Quaternion.Euler(fallRotation);
        Vector3 targetPosition = _originalPosition + fallPositionOffset;

        // Ӧ�ù���
        transform.rotation = Quaternion.Slerp(_originalRotation, targetRotation, progress);
        transform.position = Vector3.Lerp(_originalPosition, targetPosition, progress);
    }

    void ApplyStandPose(float progress)
    {
        // ���㵱ǰ������̬����Ϊ������㣩
        Quaternion fallRotation = Quaternion.Euler(this.fallRotation);
        Vector3 fallPosition = _originalPosition + fallPositionOffset;

        // Ӧ�ù��ɻ�վ��״̬
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

    [ContextMenu("���Ե���")]
    public void TestFall()
    {
        StartFalling();
    }

    [ContextMenu("����վ��")]
    public void TestStand()
    {
        StandUp();
    }
}