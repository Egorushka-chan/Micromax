namespace MicroMax.Server.Services.Assistant.Providers;

public static class AiProviderFailureReasons
{
    public const string ProbeTimeout = "probe_timeout";
    public const string InferenceTimeout = "inference_timeout";
    public const string InvalidJson = "invalid_json";
}
