using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Codex;
using MultiLlm.Providers.OpenAICompatible;
using System.Diagnostics;
using System.Runtime.InteropServices;

var environment = Environment.GetEnvironmentVariables();
var parser = new ArgsParser(args);

if (parser.Has("help") || parser.Has("h"))
{
    ConsoleChatOptions.PrintUsage();
    return;
}

var options = ConsoleChatSetup.ResolveOptions(parser, environment);

if (!options.IsValid(out var validationError))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(validationError);
    Console.ResetColor();
    ConsoleChatOptions.PrintUsage();
    return;
}

var client = ClientFactory.Create(options);
var session = new ConsoleChatSession(client, options);

await session.RunAsync();

internal static class ConsoleChatSetup
{
    public static ConsoleChatOptions ResolveOptions(ArgsParser parser, System.Collections.IDictionary environment)
    {
        return HasDirectLaunchArgs(parser)
            ? ConsoleChatOptions.From(parser, environment)
            : ResolveOptionsInteractively(parser, environment);
    }

    private static bool HasDirectLaunchArgs(ArgsParser parser)
    {
        return parser.Has("model") && parser.Has("auth");
    }

    private static ConsoleChatOptions ResolveOptionsInteractively(ArgsParser parser, System.Collections.IDictionary environment)
    {
        Console.WriteLine("=== Interactive Route Setup ===");
        Console.WriteLine("Provide --model and --auth for direct run, or configure route here.");
        Console.WriteLine();

        while (true)
        {
            var authMode = PromptAuthMode(parser.GetString("auth"));
            var defaults = GetDefaults(authMode);

            var providerId = PromptString(
                "Provider id",
                parser.GetString("provider") ?? defaults.ProviderId);

            var baseUrl = PromptString(
                "Base URL",
                parser.GetString("base-url") ?? environment[ConsoleChatOptions.BaseUrlEnv]?.ToString() ?? defaults.BaseUrl);

            var model = PromptString(
                "Model",
                parser.GetString("model") ?? environment[ConsoleChatOptions.ModelEnv]?.ToString() ?? defaults.Model);

            var apiKey = parser.GetString("api-key") ?? environment[ConsoleChatOptions.ApiKeyEnv]?.ToString();
            if (authMode is AuthMode.ApiKey)
            {
                apiKey = PromptString("API key", apiKey, allowEmpty: false);
            }

            var codexHome = parser.GetString("codex-home") ?? environment[ConsoleChatOptions.CodexHomeEnv]?.ToString();
            if (authMode is AuthMode.Codex)
            {
                codexHome = PromptString("Codex home (optional)", codexHome, allowEmpty: true);
            }

            var useStreaming = PromptBool("Streaming", parser.GetBool("stream") ?? true);

            var options = new ConsoleChatOptions(
                providerId,
                baseUrl,
                model,
                apiKey,
                authMode,
                codexHome,
                useStreaming);

            if (options.IsValid(out var error))
            {
                return options;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
            Console.WriteLine("Please retry setup.");
            Console.WriteLine();
        }
    }

    private static (string ProviderId, string BaseUrl, string Model) GetDefaults(AuthMode authMode)
    {
        return authMode switch
        {
            AuthMode.Codex => ("codex", "https://chatgpt.com/backend-api/codex/", "gpt-5-codex"),
            AuthMode.ApiKey => ("openai-compatible", "https://api.openai.com/v1", "gpt-5-mini"),
            _ => ("openai-compatible", "http://localhost:11434/v1", "llama3.1:8b")
        };
    }

    private static AuthMode PromptAuthMode(string? currentValue)
    {
        var current = ConsoleChatOptions.ParseAuthMode(currentValue);

        while (true)
        {
            Console.WriteLine("Choose route/auth mode:");
            Console.WriteLine("1) codex  (ChatGPT backend via Codex login)");
            Console.WriteLine("2) apikey (OpenAI-compatible API key)");
            Console.WriteLine("3) none   (OpenAI-compatible endpoint without auth)");

            var defaultNumber = current switch
            {
                AuthMode.Codex => "1",
                AuthMode.ApiKey => "2",
                _ => "3"
            };

            Console.Write($"Select [default {defaultNumber}]: ");
            var raw = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return current;
            }

            if (raw == "1")
            {
                return AuthMode.Codex;
            }

            if (raw == "2")
            {
                return AuthMode.ApiKey;
            }

            if (raw == "3")
            {
                return AuthMode.None;
            }

            var parsed = ConsoleChatOptions.ParseAuthMode(raw, defaultValue: AuthMode.None);
            if (parsed is AuthMode.Codex or AuthMode.ApiKey or AuthMode.None)
            {
                return parsed;
            }

            Console.WriteLine("Invalid selection.");
        }
    }

    private static string PromptString(string label, string? defaultValue, bool allowEmpty = false)
    {
        while (true)
        {
            Console.Write($"{label}{FormatDefault(defaultValue)}: ");
            var raw = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }

            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                return defaultValue.Trim();
            }

            if (allowEmpty)
            {
                return string.Empty;
            }

            Console.WriteLine($"{label} is required.");
        }
    }

    private static bool PromptBool(string label, bool defaultValue)
    {
        while (true)
        {
            var suffix = defaultValue ? "" : " (default: n)";
            Console.Write($"{label} [Y/n]{suffix}: ");
            var raw = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (raw.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                raw == "1")
            {
                return true;
            }

            if (raw.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                raw == "0")
            {
                return false;
            }

            Console.WriteLine("Enter y or n.");
        }
    }

    private static string FormatDefault(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $" [default: {value}]";
    }
}

internal static class ClientFactory
{
    public static ILlmClient Create(ConsoleChatOptions options)
    {
        var builder = LlmClientBuilder.Create();

        return options.AuthMode switch
        {
            AuthMode.Codex => ConfigureCodex(builder, options).Build(),
            AuthMode.ApiKey => ConfigureOpenAiCompatible(builder, options, requireApiKey: true).Build(),
            _ => ConfigureOpenAiCompatible(builder, options, requireApiKey: false).Build()
        };
    }

    private static LlmClientBuilder ConfigureCodex(LlmClientBuilder builder, ConsoleChatOptions options)
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

        return builder.Configure(codexOptions);
    }

    private static LlmClientBuilder ConfigureOpenAiCompatible(
        LlmClientBuilder builder,
        ConsoleChatOptions options,
        bool requireApiKey)
    {
        if (requireApiKey && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("For --auth apikey provide --api-key or OPENAI_API_KEY.");
        }

        var headers = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = $"Bearer {options.ApiKey}"
            };

        return builder.Configure(new OpenAiCompatibleProviderOptions
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
            Console.WriteLine("Chat history cleared.");
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
                    Console.WriteLine($"(attached images: {attachedImagesCount})");
                }

                return;
            }

            var response = await client.ChatAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = ExtractText(response.Message);
            Console.WriteLine(responseText);
            _messages.Add(response.Message);

            if (attachedImagesCount > 0)
            {
                Console.WriteLine($"(attached images: {attachedImagesCount})");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Request error: {exception.Message}");
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
        return string.IsNullOrWhiteSpace(text) ? "(empty model response)" : text;
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
        Console.WriteLine("Commands: /help, /clear, /exit, /image-file <path>, /image-clipboard");
        Console.WriteLine("Attach one or more images, then send text; images will be sent with that message.");
    }

    private void HandleImageFileCommand(string input)
    {
        var rawPath = input.Length > "/image-file".Length
            ? input["/image-file".Length..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            Console.Write("Image file path: ");
            rawPath = Console.ReadLine()?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            Console.WriteLine("Path was not provided.");
            return;
        }

        var normalizedPath = rawPath.Trim('"');
        if (!File.Exists(normalizedPath))
        {
            Console.WriteLine($"File not found: {normalizedPath}");
            return;
        }

        if (!ImageLoader.TryCreateImagePartFromFile(normalizedPath, out var imagePart, out var error))
        {
            Console.WriteLine(error);
            return;
        }

        _pendingImages.Add(imagePart!);
        Console.WriteLine($"Image queued: {Path.GetFileName(normalizedPath)}");
    }

    private void HandleImageClipboardCommand()
    {
        if (!ImageLoader.TryCreateImagePartFromClipboard(out var imagePart, out var error))
        {
            Console.WriteLine(error);
            return;
        }

        _pendingImages.Add(imagePart!);
        Console.WriteLine("Clipboard image queued.");
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
    internal const string BaseUrlEnv = "LLM_BASE_URL";
    internal const string ModelEnv = "LLM_MODEL";
    internal const string ApiKeyEnv = "OPENAI_API_KEY";
    internal const string CodexHomeEnv = "CODEX_HOME";

    public static ConsoleChatOptions From(string[] args, System.Collections.IDictionary environment)
    {
        var parser = new ArgsParser(args);
        return From(parser, environment);
    }

    public static ConsoleChatOptions From(ArgsParser parser, System.Collections.IDictionary environment)
    {
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
            error = $"Model is required. Set --model or {ModelEnv}.";
            return false;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            error = "Invalid --base-url.";
            return false;
        }

        if (AuthMode is AuthMode.ApiKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            error = $"For --auth apikey set --api-key or {ApiKeyEnv}.";
            return false;
        }

        error = null;
        return true;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Direct launch examples:");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-codex --auth codex");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model gpt-5-mini --auth apikey --api-key <KEY>");
        Console.WriteLine("dotnet run --project examples/ConsoleChat -- --model llama3.1:8b --auth none --base-url http://localhost:11434/v1");
        Console.WriteLine();
        Console.WriteLine("Interactive setup:");
        Console.WriteLine("dotnet run --project examples/ConsoleChat");
        Console.WriteLine();
        Console.WriteLine("Options: --provider, --base-url, --model, --stream, --auth [codex|apikey|none], --codex-home.");
        Console.WriteLine($"Env: {ModelEnv}, {BaseUrlEnv}, {ApiKeyEnv}, {CodexHomeEnv}.");
        Console.WriteLine("In chat: /image-file <path> or /image-clipboard.");
    }

    public static AuthMode ParseAuthMode(string? value, AuthMode defaultValue = AuthMode.Codex)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "codex" => AuthMode.Codex,
            "apikey" => AuthMode.ApiKey,
            "api-key" => AuthMode.ApiKey,
            "none" => AuthMode.None,
            _ => defaultValue
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
            error = "Supported formats: .png, .jpg, .jpeg, .webp, .gif.";
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
            error = $"Failed to read image file: {exception.Message}";
            return false;
        }
    }

    public static bool TryCreateImagePartFromClipboard(out ImagePart? imagePart, out string? error)
    {
        imagePart = null;
        error = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            error = "Clipboard images are currently supported only on Windows.";
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
                error = "Failed to start powershell for clipboard read.";
                return false;
            }

            using var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 10)
            {
                error = "Clipboard does not contain an image.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(stderr)
                    ? $"Clipboard read error, code: {process.ExitCode}."
                    : $"Clipboard read error: {stderr.Trim()}";
                return false;
            }

            if (ms.Length == 0)
            {
                error = "No image bytes found in clipboard.";
                return false;
            }

            imagePart = new ImagePart("image/png", ms.ToArray(), "clipboard.png");
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to get clipboard image: {exception.Message}";
            return false;
        }
    }
}

internal sealed class ArgsParser(string[] args)
{
    private readonly Dictionary<string, string> _values = Parse(args);

    public bool Has(string key) => _values.ContainsKey(key);

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
