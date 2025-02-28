using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    private static readonly string configFilePath = "config.json"; // 配置文件路径
    private static string apiKey; // API密钥
    private static int maxInputLength; // 输入文本的最大字符数
    private static int maxOutputTokens; // 输出文本的最大Token数
    private static bool isLoading = false; // 是否正在加载
    private static readonly char[] spinnerChars = new char[] { '/', '-', '\\', '|' }; // 旋转字符
    private static bool shouldRestart = true; // 是否重新运行程序

    static async Task Main(string[] args)
    {
        // 读取配置文件
        if (!LoadConfig())
        {
            Console.WriteLine("无法加载配置文件，请检查config.json文件是否存在且格式正确。");
            return;
        }

        do
        {
            // 获取输入文本
            string inputText;
            if (args.Length > 0 && args[0] != null)
            {
                inputText = args[0];
                shouldRestart = false; // 如果通过args传入文本，则不重新运行
            }
            else
            {
                Console.WriteLine($"请输入要请求的文本（最多{maxInputLength}字符）：");
                inputText = Console.ReadLine();
            }

            // 检查输入长度
            if (inputText.Length > maxInputLength)
            {
                Console.WriteLine($"输入文本长度超过{maxInputLength}字符，请缩短后重试。");
                continue;
            }

            // 启动Loading动画
            isLoading = true;
            var loadingTask = Task.Run(() => ShowLoadingAnimation());

            // 调用DeepSeek API
            string responseText = await CallDeepSeekApi(apiKey, inputText);

            // 停止Loading动画
            isLoading = false;
            await loadingTask; // 等待动画任务结束

            // 检查返回的Token是否超出限制
            if (responseText.Length > maxOutputTokens)
            {
                Console.WriteLine("警告：返回的Token数量超出限制！");
            }

            // 去掉Markdown格式并输出结果
            string cleanedText = RemoveMarkdown(responseText);
            Console.WriteLine("处理后的文本：");
            Console.WriteLine(cleanedText);

        } while (shouldRestart); // 根据条件决定是否重新运行
    }

    private static bool LoadConfig()
    {
        try
        {
            // 读取配置文件
            string configJson = File.ReadAllText(configFilePath);
            JObject config = JObject.Parse(configJson);

            // 解析配置
            apiKey = config["apiKey"]?.ToString();
            maxInputLength = config["maxInputLength"]?.ToObject<int>() ?? 500;
            maxOutputTokens = config["maxOutputTokens"]?.ToObject<int>() ?? 1000;

            return !string.IsNullOrEmpty(apiKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载配置文件时发生错误: {ex.Message}");
            return false;
        }
    }

    private static async Task<string> CallDeepSeekApi(string apiKey, string inputText)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // 构造请求体
            var requestBody = new
            {
                model = "deepseek-chat", // 使用deepseek-chat模型
                messages = new[]
                {
                    new { role = "user", content = inputText }
                },
                max_tokens = maxOutputTokens // 限制输出Token数量
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                // 发送请求
                HttpResponseMessage response = await client.PostAsync("https://api.deepseek.com/v1/chat/completions", content);

                // 处理响应
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    JObject responseObject = JObject.Parse(responseJson);

                    // 假设API返回的文本在choices[0].message.content中
                    string responseText = responseObject["choices"]?[0]?["message"]?["content"]?.ToString();
                    return responseText ?? string.Empty;
                }
                else
                {
                    // 处理错误码
                    HandleErrorResponse((int)response.StatusCode);
                    return string.Empty;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"网络请求失败: {ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生未知错误: {ex.Message}");
                return string.Empty;
            }
        }
    }

    private static void HandleErrorResponse(int statusCode)
    {
        switch (statusCode)
        {
            case 400:
                Console.WriteLine("400 - 格式错误: 请求体格式错误，请根据错误信息提示修改请求体。");
                break;
            case 401:
                Console.WriteLine("401 - 认证失败: API key 错误，认证失败，请检查您的 API key 是否正确。");
                break;
            case 402:
                Console.WriteLine("402 - 余额不足: 账号余额不足，请确认账户余额并前往充值页面进行充值。");
                break;
            case 422:
                Console.WriteLine("422 - 参数错误: 请求体参数错误，请根据错误信息提示修改相关参数。");
                break;
            case 429:
                Console.WriteLine("429 - 请求速率达到上限: 请求速率（TPM 或 RPM）达到上限，请合理规划您的请求速率。");
                break;
            case 500:
                Console.WriteLine("500 - 服务器故障: 服务器内部故障，请等待后重试。若问题一直存在，请联系我们解决。");
                break;
            case 503:
                Console.WriteLine("503 - 服务器繁忙: 服务器负载过高，请稍后重试您的请求。");
                break;
            default:
                Console.WriteLine($"未知错误: 收到未处理的HTTP状态码 {statusCode}。");
                break;
        }
    }

    private static string RemoveMarkdown(string text)
    {
        // 简单的Markdown去除逻辑
        text = text.Replace("**", "") // 加粗
                   .Replace("__", "") // 加粗
                   .Replace("`", "")  // 代码块
                   .Replace("#", "") // 标题
                   .Replace("*", "") // 斜体或列表
                   .Replace("~", "")  // 删除线
                   .Replace("[", "")  // 链接
                   .Replace("]", "")  // 链接
                   .Replace("(", "")  // 链接
                   .Replace(")", ""); // 链接
        return text;
    }

    private static void ShowLoadingAnimation()
    {
        int spinnerIndex = 0;
        while (isLoading)
        {
            // 更新Loading提示
            Console.Write($"\rLoading {spinnerChars[spinnerIndex]}");
            spinnerIndex = (spinnerIndex + 1) % spinnerChars.Length; // 切换到下一个字符
            Thread.Sleep(100); // 等待100ms
        }
        Console.Write("\r          \r"); // 清除Loading提示
    }
}