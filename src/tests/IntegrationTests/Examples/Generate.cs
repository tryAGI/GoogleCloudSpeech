/*
order: 10
title: Generate
slug: generate

Transcribe a WAV file with Google Cloud Speech-to-Text V2 and Microsoft.Extensions.AI.
*/

using Microsoft.Extensions.AI;

namespace GoogleCloudSpeech.IntegrationTests;

public partial class Tests
{
    [TestMethod]
    public async Task Example_TranscribeWav()
    {
        using var client = GetAuthenticatedClient();
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

        response.Text.Should().NotBeNullOrWhiteSpace();
    }
}
