# Microsoft.Extensions.AI Integration

!!! tip "Cross-SDK comparison"
    See the [centralized MEAI documentation](https://tryagi.github.io/docs/meai/) for feature matrices and comparisons across all tryAGI SDKs.

GoogleCloudSpeech implements `ISpeechToTextClient` on top of the official
Google Cloud Speech-to-Text V2 client. It supports buffered recognition and
true bidirectional streaming while preserving provider-native response data.

## Installation

```bash
dotnet add package GoogleCloudSpeech
```

## Usage

```csharp
using Microsoft.Extensions.AI;
using GoogleCloudSpeech;

using var client = await GoogleCloudSpeechClient.CreateAsync(
    projectId: Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!);

await using var audio = File.OpenRead("sample.wav");
var response = await client.GetTextAsync(audio, new SpeechToTextOptions
{
    SpeechLanguage = "en-US",
});

Console.WriteLine(response.Text);
```

Authentication uses Google Application Default Credentials. The adapter exposes
the underlying `SpeechClient` through its `SpeechClient` property and
`GetService` for batch recognition and provider-specific operations.

The current official Google dependency graph is not declared NativeAOT-safe;
strict trimmed publishing reports ILLink warnings from its transitive runtime
libraries. The adapter therefore does not advertise `IsAotCompatible`.

## Next Steps

- Check the [Examples](../index.md) for complete working code
- See the [centralized MEAI docs](https://tryagi.github.io/docs/meai/) for cross-SDK comparisons
- Visit the [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) for framework details
