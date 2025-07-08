using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class FallDetector : MonoBehaviour
{
    // 串口配置
    [Header("串口设置")]
    public string portName = "COM14"; // 串口号
    public int baudRate = 115200;   // 波特率

    // 跌倒设置
    [Header("跌倒设置")]
    public float fallDuration = 3f; // 跌倒持续时间(秒)
    public AudioClip fallSound; // 跌倒音效
    public AudioClip recoverSound; // 恢复音效

    // 内部组件
    private Animator animator;
    private AudioSource audioSource;

    // 内部状态
    private bool isFallen = false;
    private float fallTimer = 0f;
    private SerialPort serialPort;
    private Thread receiveThread;
    private bool isRunning = true;
    private bool fallTriggered = false; // 标记是否触发跌倒

    void Start()
    {
        // 获取组件引用
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();

        // 确保音频源存在
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始化串口
        InitializeSerialPort();

        // 初始状态
        animator.SetBool("isFall", false);
    }

    void Update()
    {
        // 处理跌倒触发
        if (fallTriggered && !isFallen)
        {
            TriggerFall();
            fallTriggered = false;
        }

        // 跌倒状态处理
        if (isFallen)
        {
            fallTimer += Time.deltaTime;

            // 检查恢复时间
            if (fallTimer >= fallDuration)
            {
                RecoverFromFall();
            }

            // 手动恢复检测 (按空格键)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                RecoverFromFall();
            }
        }
    }

    // 初始化串口连接
    void InitializeSerialPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 100,
                NewLine = "\n"
            };

            serialPort.Open();
            Debug.Log($"串口已打开: {portName}");

            // 启动接收线程
            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"串口连接失败: {e.Message}");
        }
    }

    // 串口数据接收线程
    void ReceiveData()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    string message = serialPort.ReadLine().Trim();
                    ProcessFallMessage(message);
                }
                else
                {
                    Thread.Sleep(20);
                }
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                Debug.LogError($"接收错误: {ex.Message}");
                Thread.Sleep(100);
            }
        }
    }

    // 处理跌倒消息
    void ProcessFallMessage(string message)
    {
        Debug.Log($"收到消息: {message}");

        // 精确匹配指令
        if (message == "FALL")
        {
            Debug.Log("检测到跌倒指令!");
            fallTriggered = true; // 标记需要触发跌倒
        }
    }

    // 触发跌倒
    void TriggerFall()
    {
        isFallen = true;
        fallTimer = 0f;

        // 设置动画参数
        animator.SetBool("isFall", true);

        // 播放音效
        if (fallSound != null)
        {
            audioSource.PlayOneShot(fallSound);
        }

        Debug.Log("角色跌倒!");
    }

    // 从跌倒中恢复
    void RecoverFromFall()
    {
        isFallen = false;

        // 重置动画参数
        animator.SetBool("isFall", false);

        // 播放音效
        if (recoverSound != null)
        {
            audioSource.PlayOneShot(recoverSound);
        }

        Debug.Log("角色恢复!");
    }

    // 清理资源
    void OnDestroy()
    {
        isRunning = false;

        // 等待接收线程结束
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(200);
        }

        // 关闭并释放串口
        if (serialPort != null)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
                serialPort.Dispose();
            }
            catch { }
            serialPort = null;
        }
    }

    // 在检视面板显示状态
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        string status = isFallen ? $"跌倒中 ({(fallDuration - fallTimer):F1}s)" : "正常";
        GUI.Label(new Rect(10, 10, 300, 30), $"状态: {status}", style);

        if (isFallen)
        {
            GUI.Label(new Rect(10, 40, 300, 30), "按空格键手动恢复", style);
        }

        string portStatus = (serialPort != null && serialPort.IsOpen) ? "已连接" : "未连接";
        GUI.Label(new Rect(10, 70, 300, 30), $"串口: {portName} ({portStatus})", style);
    }
}