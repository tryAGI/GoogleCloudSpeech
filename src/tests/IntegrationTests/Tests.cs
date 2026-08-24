namespace GoogleCloudSpeech.IntegrationTests;

[TestClass]
public partial class Tests
{
    private static GoogleCloudSpeechClient GetAuthenticatedClient()
    {
        var projectId =
            Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") is { Length: > 0 } projectValue
                ? projectValue
                : Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT_ID") is { Length: > 0 } projectIdValue
                    ? projectIdValue
                    : throw new AssertInconclusiveException(
                        "GOOGLE_CLOUD_PROJECT or GOOGLE_CLOUD_PROJECT_ID environment variable is not found.");
        var location = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_LOCATION") is { Length: > 0 } locationValue
            ? locationValue
            : GoogleCloudSpeechClient.DefaultLocation;
        var recognizerId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_RECOGNIZER") is { Length: > 0 } recognizerValue
            ? recognizerValue
            : GoogleCloudSpeechClient.DefaultRecognizerId;

        return new GoogleCloudSpeechClient(projectId, location, recognizerId);
    }
}
