using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

var options = ConsoleChatOptions.From(args, Environment.GetEnvironmentVariables());

if (!options.IsValid(out var validationError))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(validationError);
    Console.ResetColor();
    ConsoleChatOptions.PrintUsage();
    return;
}

var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
{
    ProviderId = options.ProviderId,
    BaseUrl = options.BaseUrl,
    Model = options.Model,
    Headers = options.BuildHeaders()
});

var client = new LlmClient([provider]);
var session = new ConsoleChatSession(client, options);

await session.RunAsync();

internal sealed class ConsoleChatSession(ILlmClient client, ConsoleChatOptions options)
{
    private readonly List<Message> _messages = [];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        PrintBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("you> ");
            var userInput = Console.ReadLine();
            if (userInput is null)
            {
                Console.WriteLine();
                break;
            }

            if (!TryHandleCommand(userInput.Trim()))
            {
                await SendUserMessageAsync(userInput, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool TryHandleCommand(string input)
    {
        if (string.Equals(input, "/exit", StringComparison.OrdinalIgnoreCase))
        {
            Environment.Exit(0);
        }

        if (string.Equals(input, "/clear", StringComparison.OrdinalIgnoreCase))
        {
            _messages.Clear();
            Console.WriteLine("История чата очищена.");
            return true;
        }

        if (string.Equals(input, "/help", StringComparison.OrdinalIgnoreCase))
        {
            PrintCommands();
            return true;
        }

        return string.IsNullOrWhiteSpace(input);
    }

    private async Task SendUserMessageAsync(string userInput, CancellationToken cancellationToken)
    {
        var userMessage = new Message(MessageRole.User, [new TextPart(userInput)]);
        _messages.Add(userMessage);

        var request = new ChatRequest(
            Model: $"{options.ProviderId}/{options.Model}",
            Messages: _messages.ToArray());

        Console.Write("assistant> ");

        if (options.UseStreaming)
        {
            var completeText = await StreamAndCollectAsync(request, cancellationToken).ConfigureAwait(false);
            _messages.Add(new Message(MessageRole.Assistant, [new TextPart(completeText)]));
            Console.WriteLine();
            return;
        }

        var response = await client.ChatAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = ExtractText(response.Message);
        Console.WriteLine(responseText);
        _messages.Add(response.Message);
    }

    private async Task<string> StreamAndCollectAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder();

        await foreach (var delta in client.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (delta.IsFinal)
            {
                continue;
            }

            sb.Append(delta.Delta);
            Console.Write(delta.Delta);
        }

        return sb.ToString();
    }

    private static string ExtractText(Message message)
    {
        var text = string.Concat(message.Parts.OfType<TextPart>().Select(static part => part.Text));
        return string.IsNullOrWhiteSpace(text) ? "(пустой ответ от модели)" : text;
    }

    private void PrintBanner()
    {
        Console.WriteLine("=== ConsoleChat for vLLM ===");
        Console.WriteLine($"Provider: {options.ProviderId}");
        Console.WriteLine($"Base URL: {options.BaseUrl}");
        Console.WriteLine($"Model: {options.Model}");
        Console.WriteLine($"Streaming: {(options.UseStreaming ? "on" : "off")}");
        PrintCommands();
    }

    private static void PrintCommands()
    {
        Console.WriteLine("Команды: /help, /clear, /exit");
    }
}

internal sealed record ConsoleChatOptions(
    string ProviderId,
    string BaseUrl,
    string Model,
    string? ApiKey,
    bool UseStreaming)
{
    private const string BaseUrlEnv = "VLLM_BASE_URL";
    private const string ModelEnv = "VLLM_MODEL";
    private const string ApiKeyEnv = "VLLM_API_KEY";

    public static ConsoleChatOptions From(string[] args, System.Collections.IDictionary environment)
    {
        var parser = new ArgsParser(args);

        var providerId = parser.GetString("provider") ?? "vllm";
        var baseUrl = parser.GetString("base-url") ?? environment[BaseUrlEnv]?.ToString() ?? "http://localhost:8000/v1";
        var model = parser.GetString("model") ?? environment[ModelEnv]?.ToString() ?? string.Empty;
        var apiKey = parser.GetString("api-key") ?? environment[ApiKeyEnv]?.ToString();
        var useStreaming = parser.GetBool("stream") ?? true;

        return new ConsoleChatOptions(providerId, baseUrl, model, apiKey, useStreaming);
    }

    public IReadOnlyDictionary<string, string> BuildHeaders()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = $"Bearer {ApiKey}"
        };
    }

    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            error = $"Не задана модель. Укажите --model или переменную окружения {ModelEnv}.";
            return false;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            error = "Некорректный --base-url.";
            return false;
        }

        error = null;
        return true;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Пример запуска:");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model Qwen/Qwen2.5-3B-Instruct --base-url http://localhost:8000/v1 --stream true");
        Console.WriteLine("Можно через env: VLLM_MODEL, VLLM_BASE_URL, VLLM_API_KEY.");
    }
}

internal sealed class ArgsParser(string[] args)
{
    private readonly Dictionary<string, string> _values = Parse(args);

    public string? GetString(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public bool? GetBool(string key)
    {
        if (!_values.TryGetValue(key, out var value))
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[key] = "true";
                continue;
            }

            result[key] = args[++i];
        }

        return result;
    }
}
