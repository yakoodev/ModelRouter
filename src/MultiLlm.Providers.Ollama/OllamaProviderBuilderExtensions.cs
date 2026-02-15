using MultiLlm.Core.Abstractions;

namespace MultiLlm.Providers.Ollama;

public static class OllamaProviderBuilderExtensions
{
    public static LlmClientBuilder Configure(
        this LlmClientBuilder builder,
        OllamaProviderOptions options,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.Configure(new OllamaOpenAiCompatProvider(options, httpClient));
    }

    public static LlmClientBuilder Configure(
        this LlmClientBuilder builder,
        OllamaOpenAiCompatProviderOptions options,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.Configure(new OllamaOpenAiCompatProvider(options, httpClient));
    }
}
