using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;

public class SerialAlertPanel : MonoBehaviour
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

    [Header("Serial Port Settings")]
    public string portName = "COM6"; // 串口号
    public int baudRate = 9600;     // 波特率

    private SerialPort _serialPort;
    private readonly ConcurrentQueue<bool> _alertQueue = new ConcurrentQueue<bool>();
    private Coroutine _currentAnimation;
    private bool _isPanelVisible = false;
    private bool _isConnected = false;
    private Thread _receiveThread;
    private bool _isRunning = true;

    void Start()
    {
        Debug.Log("串口警报面板初始化开始");

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

        // 初始化串口连接
        InitializeSerialPort();
    }

    void InitializeSerialPort()
    {
        Debug.Log("开始初始化串口连接...");

        try
        {
            // 创建串口对象
            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 100,
                NewLine = "\n" // 设置换行符
            };

            // 打开串口
            _serialPort.Open();
            Debug.Log($"串口已打开: {portName}");
            _isConnected = true;

            // 启动接收线程
            _receiveThread = new Thread(ReceiveSerialData);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"串口连接失败: {ex.Message}");
            _isConnected = false;

            // 3秒后尝试重新连接
            Invoke("InitializeSerialPort", 3f);
        }
    }

    // 串口数据接收线程
    void ReceiveSerialData()
    {
        while (_isRunning && _serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                // 读取一行数据
                string message = _serialPort.ReadLine().Trim();
                Debug.Log($"收到串口消息: {message}");

                // 检查是否包含"FALL"关键词
                if (message.IndexOf("FALL", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("检测到跌倒消息，触发警报");
                    _alertQueue.Enqueue(true);
                }
            }
            catch (System.TimeoutException)
            {
                // 读取超时是正常现象，继续循环
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"串口读取错误: {ex.Message}");
                Thread.Sleep(100);
            }
        }
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

    void OnDestroy()
    {
        // 停止运行标志
        _isRunning = false;

        // 停止接收线程
        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(200); // 等待200ms
        }

        // 关闭串口
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            _serialPort.Dispose();
            Debug.Log("串口已关闭");
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
                 $"串口状态: {(_isConnected ? "已连接" : "未连接")}", style);

        if (_isConnected)
        {
            GUI.Label(new Rect(10, 40, 300, 30),
                     $"端口: {portName} @ {baudRate} bps", style);
        }
    }
}