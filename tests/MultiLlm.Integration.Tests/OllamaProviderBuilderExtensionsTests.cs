using MultiLlm.Core.Abstractions;
using MultiLlm.Providers.Ollama;

namespace MultiLlm.Integration.Tests;

public class OllamaProviderBuilderExtensionsTests
{
    [Fact]
    public void Configure_OllamaProviderOptions_AddsOllamaProvider()
    {
        var builder = new LlmClientBuilder()
            .Configure(new OllamaProviderOptions
            {
                ProviderId = "ollama",
                BaseUrl = "http://localhost:11434/v1",
                Model = "llama3.1:8b"
            });

        Assert.Contains(builder.Providers, provider => provider.ProviderId == "ollama");
    }
}
