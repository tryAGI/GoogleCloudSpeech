<div class="docs-hero">
  <h1>GoogleCloudSpeech</h1>
  <p class="docs-hero-lead">Microsoft.Extensions.AI speech-to-text adapter built on Google's official Cloud Speech-to-Text V2 .NET SDK.</p>
  <div class="docs-badge-row">
    <a href="https://www.nuget.org/packages/GoogleCloudSpeech/"><img alt="Nuget package" src="https://img.shields.io/nuget/vpre/GoogleCloudSpeech"></a>
    <a href="https://github.com/tryAGI/GoogleCloudSpeech/actions/workflows/dotnet.yml"><img alt="dotnet" src="https://github.com/tryAGI/GoogleCloudSpeech/actions/workflows/dotnet.yml/badge.svg?branch=main"></a>
    <a href="https://github.com/tryAGI/GoogleCloudSpeech/blob/main/LICENSE"><img alt="License: MIT" src="https://img.shields.io/github/license/tryAGI/GoogleCloudSpeech"></a>
    <a href="https://discord.gg/Ca2xhfBf3v"><img alt="Discord" src="https://img.shields.io/discord/1115206893015662663?label=Discord&amp;logo=discord&amp;logoColor=white&amp;color=d82679"></a>
  </div>
  <div class="docs-hero-actions">
    <a href="#usage">Get started</a>
    <a href="#support">Get support</a>
  </div>
</div>

<div class="docs-feature-grid">
  <div class="docs-feature-card">
    <h3>Official Google transport</h3>
    <p>Uses <code>Google.Cloud.Speech.V2</code> directly, including Application Default Credentials, regional endpoints, and the complete raw client.</p>
  </div>
  <div class="docs-feature-card">
    <h3>Unified STT abstraction</h3>
    <p>Implements <code>ISpeechToTextClient</code> for single-request and bidirectional-streaming transcription.</p>
  </div>
  <div class="docs-feature-card">
    <h3>Provider details preserved</h3>
    <p>Alternatives, word timings, confidence, speaker labels, language, channel, stability, and raw Google responses remain accessible.</p>
  </div>
  <div class="docs-feature-card">
    <h3>Raw batch API available</h3>
    <p>Access the official <code>SpeechClient</code> for Cloud Storage batch recognition and advanced V2 operations.</p>
  </div>
</div>

## Installation

```bash
dotnet add package GoogleCloudSpeech --prerelease
```

## Usage

```csharp
using GoogleCloudSpeech;
using Microsoft.Extensions.AI;

// Uses Google Application Default Credentials (ADC).
using var client = await GoogleCloudSpeechClient.CreateAsync(
    projectId: Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!);

await using var audio = File.OpenRead("sample.wav");
var response = await client.GetTextAsync(audio, new SpeechToTextOptions
{
    SpeechLanguage = "en-US",
    ModelId = "long",
});

Console.WriteLine(response.Text);
```

The adapter sends buffered audio through V2 `Recognize` and streams audio through
V2 `StreamingRecognize`. For long recordings stored in Cloud Storage, use
`client.SpeechClient.BatchRecognizeAsync(...)` directly.

Pass a provider-native `RecognitionConfig` with
`GoogleCloudSpeechPropertyNames.RecognitionConfig` when auto-detection is not
appropriate, such as headerless raw PCM. Streaming-specific
`StreamingRecognitionFeatures` can be supplied with
`GoogleCloudSpeechPropertyNames.StreamingFeatures`.

NativeAOT is not advertised by this package: the current official Google client
transitively depends on `Google.Api.Gax.Grpc`, `Google.Apis.Core`, and
`Newtonsoft.Json`, which produce ILLink warnings in a strict trimmed publish.
Normal .NET 10 builds are supported.

<!-- EXAMPLES:START -->
### Generate
Transcribe a WAV file with Google Cloud Speech-to-Text V2 and Microsoft.Extensions.AI.

```csharp
using var client = new GoogleCloudSpeechClient(projectId);
var samplePath =
    Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_SAMPLE_WAV") is { Length: > 0 } samplePathValue
        ? samplePathValue
        : throw new AssertInconclusiveException(
            "GOOGLE_CLOUD_SPEECH_SAMPLE_WAV environment variable is not found.");
var language = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_LANGUAGE") is { Length: > 0 } languageValue
    ? languageValue
    : "en-US";

await using var audio = File.OpenRead(samplePath);
var response = await client.GetTextAsync(audio, new SpeechToTextOptions
{
    SpeechLanguage = language,
});
```
<!-- EXAMPLES:END -->

## Support

<div class="docs-card-grid">
  <div class="docs-card">
    <h3>Bugs</h3>
    <p>Open an issue in <a href="https://github.com/tryAGI/GoogleCloudSpeech/issues">tryAGI/GoogleCloudSpeech</a>.</p>
  </div>
  <div class="docs-card">
    <h3>Ideas and questions</h3>
    <p>Use <a href="https://github.com/tryAGI/GoogleCloudSpeech/discussions">GitHub Discussions</a> for design questions and usage help.</p>
  </div>
  <div class="docs-card">
    <h3>Community</h3>
    <p>Join the <a href="https://discord.gg/Ca2xhfBf3v">tryAGI Discord</a> for broader discussion across SDKs.</p>
  </div>
</div>

## Acknowledgments

This adapter depends on Google's official
[`Google.Cloud.Speech.V2`](https://www.nuget.org/packages/Google.Cloud.Speech.V2)
package. Google Cloud Speech-to-Text is a Google service and is subject to its
own terms and pricing.

![JetBrains logo](https://resources.jetbrains.com/storage/products/company/brand/logos/jetbrains.png)

This project is supported by JetBrains through the [Open Source Support Program](https://jb.gg/OpenSourceSupport).
