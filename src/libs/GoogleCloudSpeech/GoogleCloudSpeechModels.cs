namespace GoogleCloudSpeech;

/// <summary>Word-level timing and speaker information returned by Google Cloud Speech.</summary>
public sealed record GoogleCloudSpeechWord(
    string Text,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    float Confidence,
    string? SpeakerLabel);

/// <summary>A transcription alternative returned by Google Cloud Speech.</summary>
public sealed record GoogleCloudSpeechAlternative(
    string Text,
    float Confidence,
    IReadOnlyList<GoogleCloudSpeechWord> Words);
