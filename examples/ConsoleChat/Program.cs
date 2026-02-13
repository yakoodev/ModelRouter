using System.Text.Json;
using System.Text.Json.Serialization;
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

var authResolver = new CodexAuthResolver();
if (!authResolver.TryResolveBearerToken(options, out var bearerToken, out var authError))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(authError);
    Console.ResetColor();
    ConsoleChatOptions.PrintUsage();
    return;
}

var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
{
    ProviderId = options.ProviderId,
    BaseUrl = options.BaseUrl,
    Model = options.Model,
    Headers = BuildHeaders(bearerToken)
});

var client = new LlmClient([provider]);
var session = new ConsoleChatSession(client, options);

await session.RunAsync();

static IReadOnlyDictionary<string, string> BuildHeaders(string? bearerToken)
{
    if (string.IsNullOrWhiteSpace(bearerToken))
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Authorization"] = $"Bearer {bearerToken}"
    };
}

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
        Console.WriteLine("=== ConsoleChat (Codex auth ready) ===");
        Console.WriteLine($"Provider: {options.ProviderId}");
        Console.WriteLine($"Base URL: {options.BaseUrl}");
        Console.WriteLine($"Model: {options.Model}");
        Console.WriteLine($"Auth mode: {options.AuthMode}");
        Console.WriteLine($"Streaming: {(options.UseStreaming ? "on" : "off")}");
        PrintCommands();
    }

    private static void PrintCommands()
    {
        Console.WriteLine("Команды: /help, /clear, /exit");
    }
}

internal enum AuthMode
{
    Codex,
    ApiKey,
    None
}

internal sealed record ConsoleChatOptions(
    string ProviderId,
    string BaseUrl,
    string Model,
    string? ApiKey,
    AuthMode AuthMode,
    string? CodexHome,
    bool UseStreaming)
{
    private const string BaseUrlEnv = "LLM_BASE_URL";
    private const string ModelEnv = "LLM_MODEL";
    private const string ApiKeyEnv = "OPENAI_API_KEY";
    private const string CodexHomeEnv = "CODEX_HOME";

    public static ConsoleChatOptions From(string[] args, System.Collections.IDictionary environment)
    {
        var parser = new ArgsParser(args);

        var providerId = parser.GetString("provider") ?? "openai-compatible";
        var baseUrl = parser.GetString("base-url") ?? environment[BaseUrlEnv]?.ToString() ?? "https://api.openai.com/v1";
        var model = parser.GetString("model") ?? environment[ModelEnv]?.ToString() ?? string.Empty;
        var apiKey = parser.GetString("api-key") ?? environment[ApiKeyEnv]?.ToString();
        var codexHome = parser.GetString("codex-home") ?? environment[CodexHomeEnv]?.ToString();
        var authMode = ParseAuthMode(parser.GetString("auth"));
        var useStreaming = parser.GetBool("stream") ?? true;

        return new ConsoleChatOptions(providerId, baseUrl, model, apiKey, authMode, codexHome, useStreaming);
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
        Console.WriteLine("Пример запуска (через Codex auth):");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth codex");
        Console.WriteLine("Пример запуска (через API ключ):");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>");
        Console.WriteLine("Опции: --provider, --base-url, --model, --stream, --auth [codex|apikey|none], --codex-home.");
        Console.WriteLine("Env: LLM_MODEL, LLM_BASE_URL, OPENAI_API_KEY, CODEX_HOME.");
    }

    private static AuthMode ParseAuthMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AuthMode.Codex;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "codex" => AuthMode.Codex,
            "apikey" => AuthMode.ApiKey,
            "api-key" => AuthMode.ApiKey,
            "none" => AuthMode.None,
            _ => AuthMode.Codex
        };
    }
}

internal interface IAuthResolver
{
    bool TryResolveBearerToken(ConsoleChatOptions options, out string? token, out string? error);
}

internal sealed class CodexAuthResolver : IAuthResolver
{
    public bool TryResolveBearerToken(ConsoleChatOptions options, out string? token, out string? error)
    {
        token = null;

        if (options.AuthMode is AuthMode.None)
        {
            error = null;
            return true;
        }

        if (options.AuthMode is AuthMode.ApiKey)
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                error = "Для --auth apikey укажите --api-key или OPENAI_API_KEY.";
                return false;
            }

            token = options.ApiKey;
            error = null;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            token = options.ApiKey;
            error = null;
            return true;
        }

        var authFile = ResolveAuthFilePath(options.CodexHome);
        if (!File.Exists(authFile))
        {
            error = $"Не найден Codex auth файл: {authFile}. Выполните 'codex login --device-auth' и повторите.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(authFile);
            var auth = JsonSerializer.Deserialize<CodexAuthFile>(json);

            if (string.IsNullOrWhiteSpace(auth?.OpenAiApiKey))
            {
                error = $"В {authFile} нет OPENAI_API_KEY. Выполните вход через Codex CLI.";
                return false;
            }

            token = auth.OpenAiApiKey;
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось прочитать Codex auth файл {authFile}: {exception.Message}";
            return false;
        }
    }

    private static string ResolveAuthFilePath(string? codexHome)
    {
        var home = codexHome;
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("CODEX_HOME");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            home = Path.Combine(userProfile, ".codex");
        }

        return Path.Combine(home, "auth.json");
    }

    private sealed class CodexAuthFile
    {
        [JsonPropertyName("OPENAI_API_KEY")]
        public string? OpenAiApiKey { get; init; }
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
