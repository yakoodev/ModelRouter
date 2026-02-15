using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Codex;
using MultiLlm.Providers.OpenAICompatible;
using System.Diagnostics;
using System.Runtime.InteropServices;

var options = ConsoleChatOptions.From(args, Environment.GetEnvironmentVariables());

if (!options.IsValid(out var validationError))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(validationError);
    Console.ResetColor();
    ConsoleChatOptions.PrintUsage();
    return;
}

var provider = ProviderFactory.Create(options);
var client = new LlmClient([provider]);
var session = new ConsoleChatSession(client, options);

await session.RunAsync();

internal static class ProviderFactory
{
    public static IModelProvider Create(ConsoleChatOptions options)
    {
        return options.AuthMode switch
        {
            AuthMode.Codex => CreateCodexProvider(options),
            AuthMode.ApiKey => CreateOpenAiCompatibleProvider(options, requireApiKey: true),
            _ => CreateOpenAiCompatibleProvider(options, requireApiKey: false)
        };
    }

    private static IModelProvider CreateCodexProvider(ConsoleChatOptions options)
    {
        var codexOptions = new CodexProviderOptions(
            IsDevelopment: true,
            EnableExperimentalAuthAdapters: false)
        {
            ProviderId = options.ProviderId,
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            CodexHome = options.CodexHome,
            UseChatGptBackend = true
        };

        return new CodexProvider(codexOptions, [new OfficialDeviceCodeBackend(codexOptions)]);
    }

    private static IModelProvider CreateOpenAiCompatibleProvider(ConsoleChatOptions options, bool requireApiKey)
    {
        if (requireApiKey && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Для --auth apikey укажите --api-key или OPENAI_API_KEY.");
        }

        var headers = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = $"Bearer {options.ApiKey}"
            };

        return new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            ProviderId = options.ProviderId,
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            Headers = headers
        });
    }
}

internal sealed class ConsoleChatSession(ILlmClient client, ConsoleChatOptions options)
{
    private readonly List<Message> _messages = [];
    private readonly List<ImagePart> _pendingImages = [];

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

        if (input.StartsWith("/image-file", StringComparison.OrdinalIgnoreCase))
        {
            HandleImageFileCommand(input);
            return true;
        }

        if (string.Equals(input, "/image-clipboard", StringComparison.OrdinalIgnoreCase))
        {
            HandleImageClipboardCommand();
            return true;
        }

        return string.IsNullOrWhiteSpace(input);
    }

    private async Task SendUserMessageAsync(string userInput, CancellationToken cancellationToken)
    {
        var messageParts = new List<MessagePart> { new TextPart(userInput) };
        messageParts.AddRange(_pendingImages);

        var userMessage = new Message(MessageRole.User, messageParts);
        _messages.Add(userMessage);
        var attachedImagesCount = _pendingImages.Count;
        _pendingImages.Clear();

        var request = new ChatRequest(
            Model: $"{options.ProviderId}/{options.Model}",
            Messages: _messages.ToArray());

        Console.Write("assistant> ");

        try
        {
            if (options.UseStreaming)
            {
                var completeText = await StreamAndCollectAsync(request, cancellationToken).ConfigureAwait(false);
                _messages.Add(new Message(MessageRole.Assistant, [new TextPart(completeText)]));
                Console.WriteLine();

                if (attachedImagesCount > 0)
                {
                    Console.WriteLine($"(к сообщению было прикреплено изображений: {attachedImagesCount})");
                }

                return;
            }

            var response = await client.ChatAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = ExtractText(response.Message);
            Console.WriteLine(responseText);
            _messages.Add(response.Message);

            if (attachedImagesCount > 0)
            {
                Console.WriteLine($"(к сообщению было прикреплено изображений: {attachedImagesCount})");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка запроса: {exception.Message}");
        }
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
        Console.WriteLine("=== ConsoleChat ===");
        Console.WriteLine($"Provider: {options.ProviderId}");
        Console.WriteLine($"Base URL: {options.BaseUrl}");
        Console.WriteLine($"Model: {options.Model}");
        Console.WriteLine($"Auth mode: {options.AuthMode}");
        Console.WriteLine($"Streaming: {(options.UseStreaming ? "on" : "off")}");
        PrintCommands();
    }

    private static void PrintCommands()
    {
        Console.WriteLine("Команды: /help, /clear, /exit, /image-file <путь>, /image-clipboard");
        Console.WriteLine("Загрузите одно или несколько изображений, затем отправьте текст — картинки уйдут вместе с этим сообщением.");
    }

    private void HandleImageFileCommand(string input)
    {
        var rawPath = input.Length > "/image-file".Length
            ? input["/image-file".Length..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            Console.Write("Путь к файлу изображения: ");
            rawPath = Console.ReadLine()?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            Console.WriteLine("Путь не указан.");
            return;
        }

        var normalizedPath = rawPath.Trim('"');
        if (!File.Exists(normalizedPath))
        {
            Console.WriteLine($"Файл не найден: {normalizedPath}");
            return;
        }

        if (!ImageLoader.TryCreateImagePartFromFile(normalizedPath, out var imagePart, out var error))
        {
            Console.WriteLine(error);
            return;
        }

        _pendingImages.Add(imagePart!);
        Console.WriteLine($"Изображение добавлено: {Path.GetFileName(normalizedPath)} (ожидает отправки)");
    }

    private void HandleImageClipboardCommand()
    {
        if (!ImageLoader.TryCreateImagePartFromClipboard(out var imagePart, out var error))
        {
            Console.WriteLine(error);
            return;
        }

        _pendingImages.Add(imagePart!);
        Console.WriteLine("Изображение из буфера обмена добавлено (ожидает отправки).");
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

        var authMode = ParseAuthMode(parser.GetString("auth"));
        var defaultProvider = authMode is AuthMode.Codex ? "codex" : "openai-compatible";
        var providerId = parser.GetString("provider") ?? defaultProvider;
        var defaultBaseUrl = authMode is AuthMode.Codex ? "https://chatgpt.com/backend-api/codex/" : "https://api.openai.com/v1";
        var baseUrl = parser.GetString("base-url") ?? environment[BaseUrlEnv]?.ToString() ?? defaultBaseUrl;
        var model = parser.GetString("model") ?? environment[ModelEnv]?.ToString() ?? string.Empty;
        var apiKey = parser.GetString("api-key") ?? environment[ApiKeyEnv]?.ToString();
        var codexHome = parser.GetString("codex-home") ?? environment[CodexHomeEnv]?.ToString();
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
        Console.WriteLine("Пример запуска (через codex cli login, ChatGPT backend):");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-codex --auth codex");
        Console.WriteLine("Пример запуска (через API ключ):");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>");
        Console.WriteLine("Опции: --provider, --base-url, --model, --stream, --auth [codex|apikey|none], --codex-home.");
        Console.WriteLine("Env: LLM_MODEL, LLM_BASE_URL, OPENAI_API_KEY, CODEX_HOME.");
        Console.WriteLine("В чате: /image-file <путь> или /image-clipboard для прикрепления изображения к следующему сообщению.");
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

internal static class ImageLoader
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    };

    public static bool TryCreateImagePartFromFile(string path, out ImagePart? imagePart, out string? error)
    {
        imagePart = null;
        error = null;

        var extension = Path.GetExtension(path);
        if (!SupportedImageExtensions.Contains(extension))
        {
            error = "Поддерживаются только: .png, .jpg, .jpeg, .webp, .gif.";
            return false;
        }

        var mimeType = extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => string.Empty
        };

        try
        {
            var bytes = File.ReadAllBytes(path);
            imagePart = new ImagePart(mimeType, bytes, Path.GetFileName(path));
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось прочитать файл изображения: {exception.Message}";
            return false;
        }
    }

    public static bool TryCreateImagePartFromClipboard(out ImagePart? imagePart, out string? error)
    {
        imagePart = null;
        error = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            error = "Буфер обмена с изображениями сейчас поддерживается только на Windows.";
            return false;
        }

        const string command = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;" +
            "Add-Type -AssemblyName System.Windows.Forms;" +
            "Add-Type -AssemblyName System.Drawing;" +
            "if (-not [System.Windows.Forms.Clipboard]::ContainsImage()) { exit 10 };" +
            "$image=[System.Windows.Forms.Clipboard]::GetImage();" +
            "$ms=New-Object System.IO.MemoryStream;" +
            "$image.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png);" +
            "$bytes=$ms.ToArray();" +
            "[Console]::OpenStandardOutput().Write($bytes,0,$bytes.Length);";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                error = "Не удалось запустить powershell для чтения буфера обмена.";
                return false;
            }

            using var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 10)
            {
                error = "В буфере обмена нет изображения.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(stderr)
                    ? $"Ошибка чтения буфера обмена, код: {process.ExitCode}."
                    : $"Ошибка чтения буфера обмена: {stderr.Trim()}";
                return false;
            }

            if (ms.Length == 0)
            {
                error = "В буфере обмена не найдено изображение.";
                return false;
            }

            imagePart = new ImagePart("image/png", ms.ToArray(), "clipboard.png");
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось получить изображение из буфера обмена: {exception.Message}";
            return false;
        }
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
