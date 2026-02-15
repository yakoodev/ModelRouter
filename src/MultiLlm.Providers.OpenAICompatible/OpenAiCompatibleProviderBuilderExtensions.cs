using MultiLlm.Core.Abstractions;

namespace MultiLlm.Providers.OpenAICompatible;

public static class OpenAiCompatibleProviderBuilderExtensions
{
    public static LlmClientBuilder Configure(
        this LlmClientBuilder builder,
        OpenAiCompatibleProviderOptions options,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.Configure(new OpenAiCompatibleProvider(options, httpClient));
    }
}
