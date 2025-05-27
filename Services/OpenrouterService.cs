using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace explaina.Services
{
    public class OpenrouterService
    {
        private HttpClient _httpClient;
        private string _apiKey;
        private string _baseUrl = "https://openrouter.ai/api/v1/chat/completions";
        
        // 用于通知UI新消息到达的事件
        public event EventHandler<string> MessageReceived;

        public OpenrouterService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://explaina.app"); // 替换为您的应用域名
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Explaina App"); // 您的应用名称
        }

        /// <summary>
        /// 向OpenRouter发送prompt并流式接收回复
        /// </summary>
        /// <param name="prompt">用户输入的提示</param>
        /// <param name="model">要使用的模型，默认为Claude</param>
        /// <returns>完整的回复文本</returns>
        public async Task<string> SendMessageStreamAsync(string prompt, string model = "anthropic/claude-3-sonnet")
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                stream = true
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = requestContent
            };

            StringBuilder fullResponse = new StringBuilder();

            try
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    // 忽略空行和data: [DONE]
                    if (string.IsNullOrWhiteSpace(line) || line == "data: [DONE]")
                        continue;

                    // 解析SSE数据
                    if (line.StartsWith("data: "))
                    {
                        string json = line.Substring("data: ".Length);
                        try
                        {
                            var streamResponse = JsonDocument.Parse(json);
                            var choices = streamResponse.RootElement.GetProperty("choices");

                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var contentElement))
                                {
                                    string textChunk = contentElement.GetString();
                                    if (!string.IsNullOrEmpty(textChunk))
                                    {
                                        fullResponse.Append(textChunk);
                                        // 触发事件，通知UI有新的内容
                                        MessageReceived?.Invoke(this, textChunk);
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // 解析错误，跳过该行
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取OpenRouter响应时出错: {ex.Message}", ex);
            }

            return fullResponse.ToString();
        }
    }
}