using Google.Cloud.Speech.V2;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.AI;

namespace GoogleCloudSpeech.IntegrationTests;

public partial class Tests
{
    [TestMethod]
    public void RecognizeResponse_MapsTextTimingsAndProviderMetadata()
    {
        var response = new RecognizeResponse
        {
            Metadata = new RecognitionResponseMetadata
            {
                RequestId = "request-id",
            },
            Results =
            {
                new SpeechRecognitionResult
                {
                    LanguageCode = "en-US",
                    Alternatives =
                    {
                        new SpeechRecognitionAlternative
                        {
                            Transcript = "hello world",
                            Confidence = 0.96F,
                            Words =
                            {
                                new WordInfo
                                {
                                    Word = "hello",
                                    StartOffset = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(100)),
                                    EndOffset = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(450)),
                                    Confidence = 0.94F,
                                },
                                new WordInfo
                                {
                                    Word = "world",
                                    StartOffset = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(500)),
                                    EndOffset = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(900)),
                                    Confidence = 0.98F,
                                    SpeakerLabel = "speaker-1",
                                },
                            },
                        },
                    },
                },
            },
        };

        var result = GoogleCloudSpeechClient.CreateResponse(response, "long");

        result.Text.Should().Be("hello world");
        result.ResponseId.Should().Be("request-id");
        result.ModelId.Should().Be("long");
        result.StartTime.Should().Be(TimeSpan.FromMilliseconds(100));
        result.EndTime.Should().Be(TimeSpan.FromMilliseconds(900));
        result.RawRepresentation.Should().BeSameAs(response);
        result.AdditionalProperties![GoogleCloudSpeechPropertyNames.Metadata].Should().BeSameAs(response.Metadata);

        var words = result.AdditionalProperties[GoogleCloudSpeechPropertyNames.Words]
            .Should().BeAssignableTo<IReadOnlyList<GoogleCloudSpeechWord>>().Subject;
        words.Should().HaveCount(2);
        words[1].SpeakerLabel.Should().Be("speaker-1");
    }

    [TestMethod]
    public void StreamingResult_MapsInterimAndFinalUpdates()
    {
        var interimResult = new StreamingRecognitionResult
        {
            IsFinal = false,
            Stability = 0.7F,
            LanguageCode = "en-US",
            ChannelTag = 2,
            ResultEndOffset = Duration.FromTimeSpan(TimeSpan.FromMilliseconds(750)),
            Alternatives =
            {
                new SpeechRecognitionAlternative
                {
                    Transcript = "hello",
                },
            },
        };

        var interim = GoogleCloudSpeechClient.CreateUpdate(interimResult, null, "response-id", "long");
        interim.Should().NotBeNull();
        interim!.Kind.Should().Be(SpeechToTextResponseUpdateKind.TextUpdating);
        interim.Text.Should().Be("hello");
        interim.EndTime.Should().Be(TimeSpan.FromMilliseconds(750));
        interim.AdditionalProperties![GoogleCloudSpeechPropertyNames.LanguageCode].Should().Be("en-US");
        interim.AdditionalProperties[GoogleCloudSpeechPropertyNames.ChannelTag].Should().Be(2);

        interimResult.IsFinal = true;
        var final = GoogleCloudSpeechClient.CreateUpdate(interimResult, null, "response-id", "long");
        final!.Kind.Should().Be(SpeechToTextResponseUpdateKind.TextUpdated);
    }
}
