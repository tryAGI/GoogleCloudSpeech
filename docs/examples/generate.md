# Generate

Transcribe a WAV file with Google Cloud Speech-to-Text V2 and Microsoft.Extensions.AI.

This example assumes `using GoogleCloudSpeech;` is in scope, `projectId` contains a Google Cloud project ID, and Application Default Credentials are configured.

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