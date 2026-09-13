using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace nxteste2
{
    // Uma mensagem da conversa, no formato que a Messages API da Anthropic
    // espera dentro do array "messages" ("role": "user" | "assistant").
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    // Cliente HTTP simples para a Messages API da Anthropic.
    // Doc oficial: https://docs.claude.com/en/api/messages
    //
    // NÃO usa JavaScriptSerializer/Newtonsoft.Json/System.Text.Json de
    // propósito — monta e lê o JSON "na mão" (veja MiniJson mais abaixo),
    // pra não depender de nenhuma referência de assembly ou pacote NuGet
    // extra. Só precisa de System.Net.Http, que já vem em qualquer projeto
    // .NET Framework 4.5+ ou .NET (Core) 5+/6+/8.
    public class AnthropicClient
    {
        // Ajuste aqui se a Anthropic lançar um modelo mais novo. Lista
        // atual de nomes de modelo: https://docs.claude.com/en/docs/about-claude/models
        private const string DefaultModel = "claude-sonnet-4-5-20250929";
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";
        private const int MaxTokens = 1024;

        private static readonly HttpClient _http = new HttpClient();

        public async Task<string> SendAsync(List<ChatMessage> conversation, string systemPrompt = null)
        {
            string apiKey = ApiKeyStore.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Chave da API Anthropic não configurada.");

            string json = BuildRequestJson(DefaultModel, MaxTokens, conversation, systemPrompt);

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, ApiUrl))
            {
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", ApiVersion);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _http.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        "Erro na API da Anthropic (" + (int)response.StatusCode + "): " + responseBody);
                }

                return ExtractAssistantText(responseBody);
            }
        }

        // Monta o corpo da requisição na mão:
        // { "model": "...", "max_tokens": N, "system": "...", "messages": [ {"role":"user","content":"..."}, ... ] }
        private static string BuildRequestJson(string model, int maxTokens, List<ChatMessage> conversation, string systemPrompt)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"model\":").Append(JsonString(model)).Append(',');
            sb.Append("\"max_tokens\":").Append(maxTokens.ToString(CultureInfo.InvariantCulture)).Append(',');
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                sb.Append("\"system\":").Append(JsonString(systemPrompt)).Append(',');
            sb.Append("\"messages\":[");
            for (int i = 0; i < conversation.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                ChatMessage m = conversation[i];
                sb.Append('{');
                sb.Append("\"role\":").Append(JsonString(m.Role)).Append(',');
                sb.Append("\"content\":").Append(JsonString(m.Content));
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        // Lê a resposta e concatena o texto de todos os blocos
        // "content": [ { "type": "text", "text": "..." }, ... ].
        private static string ExtractAssistantText(string responseJson)
        {
            object parsed = MiniJson.Parse(responseJson);
            Dictionary<string, object> root = parsed as Dictionary<string, object>;
            object contentObj;
            if (root != null && root.TryGetValue("content", out contentObj))
            {
                List<object> blocks = contentObj as List<object>;
                if (blocks != null)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (object block in blocks)
                    {
                        Dictionary<string, object> blockDict = block as Dictionary<string, object>;
                        if (blockDict == null)
                            continue;
                        object type;
                        object text;
                        if (blockDict.TryGetValue("type", out type) && "text".Equals(type as string)
                            && blockDict.TryGetValue("text", out text))
                        {
                            sb.Append(text as string);
                        }
                    }
                    return sb.ToString();
                }
            }
            return "(resposta sem texto)";
        }

        private static string JsonString(string s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }

    // Parser JSON mínimo, só o suficiente pra ler a resposta da Messages
    // API (objetos, arrays, strings, números, bool, null). Sem dependências
    // externas — troca JavaScriptSerializer/Newtonsoft.Json/System.Text.Json
    // por ~90 linhas que funcionam em qualquer versão do .NET.
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            return ParseValue(json, ref i);
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> obj = new Dictionary<string, object>();
            i++; // '{'
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // ':'
                object value = ParseValue(s, ref i);
                obj[key] = value;
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // '}'
                break;
            }
            return obj;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> arr = new List<object>();
            i++; // '['
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return arr; }
            while (true)
            {
                object value = ParseValue(s, ref i);
                arr.Add(value);
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ']'
                break;
            }
            return arr;
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // '"'
            StringBuilder sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i];
                if (c == '\\')
                {
                    i++;
                    char esc = s[i];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            string hex = s.Substring(i + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            i++; // closing '"'
            return sb.ToString();
        }

        private static object ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { i += 4; return true; }
            i += 5; return false;
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
                i++;
            string numStr = s.Substring(start, i - start);
            double d;
            double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
            return d;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
