using System;
using System.IO.Ports;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class SerialTHFilter : MonoBehaviour
{
    // 串口配置（必须和设备一致）
    public string portName = "COM5";
    public int baudRate = 115200;

    // UI输出：仅温湿度数值（必填）
    public Text tempText;  // 显示温度（如：35.2°C）
    public Text humText;   // 显示湿度（如：32.5%）

    // 可选：显示原始温湿度行（如需要调试可启用）
    public Text rawTHText; // 显示 "Temp *C = 35.18		Hum. % = 32.59" 这样的行

    private SerialPort sp;
    private Thread readThread;
    private bool isRunning = false;
    private readonly Queue<string> thQueue = new Queue<string>();

    // 仅匹配温湿度行的正则（精准过滤）
    private readonly string thPattern =
        @"Temp\s*\*C\s*=\s*([\d.]+)\s+Hum\.?\s*%\s*=\s*([\d.]+)";


    void Start()
    {
        // 调试：打印系统识别的串口
        Debug.Log("系统可用串口：" + string.Join(", ", SerialPort.GetPortNames()));
        OpenPort();
    }


    void OpenPort()
    {
        try
        {
            // 清理旧连接
            if (sp != null && sp.IsOpen)
            {
                sp.Close();
                sp.Dispose();
            }

            // 初始化串口（严格匹配设备参数）
            sp = new SerialPort(portName, baudRate)
            {
                Encoding = Encoding.UTF8,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 100
            };
            sp.Open();
            Debug.Log($"✅ 串口 [{portName}] 已连接");
            isRunning = true;

            // 启动行读取线程（仅处理温湿度行）
            readThread = new Thread(ReadFilteredLines) { IsBackground = true };
            readThread.Start();
        }
        catch (UnauthorizedAccessException)
        {
            Debug.LogError("❌ 串口被占用！请关闭串口助手等软件");
            Invoke(nameof(OpenPort), 3f);
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 连接失败：{e.Message}");
            Invoke(nameof(OpenPort), 3f);
        }
    }


    void ReadFilteredLines()
    {
        while (isRunning && sp != null && sp.IsOpen)
        {
            try
            {
                if (sp.BytesToRead > 0)
                {
                    string line = sp.ReadLine(); // 按行读取（自动分割换行）

                    // 仅保留温湿度行（精准过滤）
                    if (Regex.IsMatch(line, thPattern))
                    {
                        lock (thQueue)
                        {
                            thQueue.Enqueue(line); // 加入解析队列
                        }
                    }
                }
                else
                {
                    Thread.Sleep(10); // 降低CPU消耗
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"读取异常：{e.Message}");
                isRunning = false;
                break;
            }
        }
    }


    void Update()
    {
        // 自动重连逻辑
        if (!isRunning) OpenPort();

        // 处理温湿度行队列
        while (thQueue.Count > 0)
        {
            string line;
            lock (thQueue)
            {
                line = thQueue.Dequeue(); // 取出一行温湿度数据
            }

            // 可选：显示原始温湿度行（用于调试）
            if (rawTHText != null)
            {
                rawTHText.text += line + "\n";
                // 自动滚动到底部（如果有ScrollRect）
                var scrollRect = rawTHText.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollRect.verticalNormalizedPosition = 0;
                }
            }

            // 解析并更新温湿度数值
            Match match = Regex.Match(line, thPattern);
            if (match.Success)
            {
                // 提取温度
                if (float.TryParse(match.Groups[1].Value, out float temp))
                {
                    tempText.text = $"{temp:F1}°C";
                }

                // 提取湿度
                if (float.TryParse(match.Groups[2].Value, out float hum))
                {
                    humText.text = $"{hum:F1}%";
                }
            }
        }
    }


    void OnDestroy()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive)
            readThread.Join(500);
        if (sp != null && sp.IsOpen)
            sp.Close();
    }
}