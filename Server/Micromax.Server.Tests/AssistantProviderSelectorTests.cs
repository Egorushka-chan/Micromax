using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant;
using Microsoft.Extensions.Logging.Abstractions;

namespace Micromax.Server.Tests;

public sealed class AssistantProviderSelectorTests
{
    private static readonly AiCommandContext EmptyContext = new([], []);

    [Fact]
    public async Task UsesOllamaWhenAvailable()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "open_products" });
        var selector = CreateSelector(ollama, openAi, new MockAiCommandProvider());

        var result = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("Ollama", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task FallsBackToOpenAiWhenOllamaFails()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, failure: new InvalidOperationException("ollama down"));
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(ollama, openAi, new MockAiCommandProvider());

        var result = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("OpenAi", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task FallsBackToMockWhenRealProvidersFail()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, failure: new InvalidOperationException("ollama down"));
        var openAi = new FakeProvider(AiProviderKind.OpenAi, failure: new InvalidOperationException("openai down"));
        var selector = CreateSelector(ollama, openAi, new MockAiCommandProvider());

        var result = await selector.InterpretAsync(EmptyContext, "покажи доступные команды", CancellationToken.None);

        Assert.Equal("Mock", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task ReturnsToOllamaAfterAvailabilityRestored()
    {
        var availability = new AiProviderAvailability();
        availability.MarkUnavailable(AiProviderKind.Ollama);

        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, failure: new InvalidOperationException("openai down"));
        var selector = CreateSelector(availability, ollama, openAi, new MockAiCommandProvider());

        var first = await selector.InterpretAsync(EmptyContext, "покажи доступные команды", CancellationToken.None);
        availability.MarkAvailable(AiProviderKind.Ollama);
        var second = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("Mock", first.Provider);
        Assert.Equal("Ollama", second.Provider);
    }

    [Fact]
    public async Task InvalidRealProviderResponseFallsBack()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "find_product", ProductId = 404 });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(ollama, openAi, new MockAiCommandProvider());

        var result = await selector.InterpretAsync(EmptyContext, "найди товар", CancellationToken.None);

        Assert.Equal("Server", result.Provider);
        Assert.Equal("Clarification", result.Mode);
    }

    private static AiProviderSelector CreateSelector(params IAiCommandProvider[] providers)
    {
        return CreateSelector(new AiProviderAvailability(), providers);
    }

    private static AiProviderSelector CreateSelector(AiProviderAvailability availability, params IAiCommandProvider[] providers)
    {
        return new AiProviderSelector(
            providers,
            availability,
            new AiCommandNormalizer(),
            NullLogger<AiProviderSelector>.Instance);
    }

    private sealed class FakeProvider(
        AiProviderKind kind,
        AssistantCommand? command = null,
        Exception? failure = null) : IAiCommandProvider
    {
        public AiProviderKind Kind { get; } = kind;
        public bool IsRealProvider => Kind != AiProviderKind.Mock;
        public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(failure is null);

        public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(command ?? new AssistantCommand { Mode = "Command", CommandType = "help" });
        }
    }
}
