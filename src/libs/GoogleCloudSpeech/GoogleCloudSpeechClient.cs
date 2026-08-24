#nullable enable
#pragma warning disable MEAI001

using System.Buffers;
using System.Runtime.CompilerServices;
using Google.Cloud.Speech.V2;
using Google.Protobuf;
using Microsoft.Extensions.AI;

namespace GoogleCloudSpeech;

/// <summary>
/// Microsoft.Extensions.AI speech-to-text adapter over the official Google Cloud Speech-to-Text V2 client.
/// </summary>
public sealed class GoogleCloudSpeechClient : ISpeechToTextClient
{
    public const string DefaultLocation = "global";
    public const string DefaultRecognizerId = "_";
    public const string DefaultModel = "long";

    private readonly string _defaultModelId;
    private SpeechToTextClientMetadata? _metadata;

    /// <summary>Creates the official client synchronously with Application Default Credentials.</summary>
    public GoogleCloudSpeechClient(
        string projectId,
        string location = DefaultLocation,
        string recognizerId = DefaultRecognizerId,
        string defaultModelId = DefaultModel)
        : this(SpeechClient.Create(), projectId, location, recognizerId, defaultModelId)
    {
    }

    public GoogleCloudSpeechClient(
        SpeechClient speechClient,
        string projectId,
        string location = DefaultLocation,
        string recognizerId = DefaultRecognizerId,
        string defaultModelId = DefaultModel)
    {
        SpeechClient = speechClient ?? throw new ArgumentNullException(nameof(speechClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(recognizerId);
        _defaultModelId = !string.IsNullOrWhiteSpace(defaultModelId)
            ? defaultModelId
            : throw new ArgumentException("A default model ID is required.", nameof(defaultModelId));
        Recognizer = $"projects/{projectId}/locations/{location}/recognizers/{recognizerId}";
    }

    /// <summary>Creates the official client with Application Default Credentials.</summary>
    public static async Task<GoogleCloudSpeechClient> CreateAsync(
        string projectId,
        string location = DefaultLocation,
        string recognizerId = DefaultRecognizerId,
        string defaultModelId = DefaultModel,
        CancellationToken cancellationToken = default)
    {
        var speechClient = await SpeechClient.CreateAsync(cancellationToken).ConfigureAwait(false);
        return new GoogleCloudSpeechClient(speechClient, projectId, location, recognizerId, defaultModelId);
    }

    /// <summary>The official Google Cloud client, including raw batch-recognition APIs.</summary>
    public SpeechClient SpeechClient { get; }

    /// <summary>The fully qualified recognizer resource used by this adapter.</summary>
    public string Recognizer { get; }

    /// <summary>The official Google client manages shared channel lifetime; this adapter owns no disposable transport.</summary>
    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceKey is not null ? null :
            serviceType == typeof(SpeechToTextClientMetadata)
                ? (_metadata ??= new(
                    "google-cloud-speech",
                    new Uri($"https://{SpeechClient.DefaultEndpoint}"),
                    _defaultModelId)) :
            serviceType.IsInstanceOfType(this) ? this :
            serviceType.IsInstanceOfType(SpeechClient) ? SpeechClient :
            null;
    }

    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        using var audioBuffer = new MemoryStream();
        await audioSpeechStream.CopyToAsync(audioBuffer, cancellationToken).ConfigureAwait(false);
        var request = new RecognizeRequest
        {
            Recognizer = Recognizer,
            Config = CreateRecognitionConfig(options),
            Content = ByteString.CopyFrom(audioBuffer.GetBuffer(), 0, checked((int)audioBuffer.Length)),
        };

        var response = await SpeechClient.RecognizeAsync(request, cancellationToken).ConfigureAwait(false);
        return CreateResponse(response, GetModelId(options));
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var responseId = Guid.NewGuid().ToString("N");
        var modelId = GetModelId(options);
        yield return new SpeechToTextResponseUpdate
        {
            Kind = SpeechToTextResponseUpdateKind.SessionOpen,
            ResponseId = responseId,
            ModelId = modelId,
        };

        await foreach (var response in RecognizeStreamingAsync(audioSpeechStream, options, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (var result in response.Results)
            {
                if (CreateUpdate(result, response.Metadata, responseId, modelId) is { } update)
                {
                    yield return update;
                }
            }
        }

        yield return new SpeechToTextResponseUpdate
        {
            Kind = SpeechToTextResponseUpdateKind.SessionClose,
            ResponseId = responseId,
            ModelId = modelId,
        };
    }

    /// <summary>Maps an official synchronous response to Microsoft.Extensions.AI.</summary>
    public static SpeechToTextResponse CreateResponse(RecognizeResponse response, string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        var resultAlternatives = response.Results
            .Select(result => new
            {
                Result = result,
                Alternatives = CreateAlternatives(result.Alternatives),
            })
            .ToArray();
        var primaryAlternatives = resultAlternatives
            .Where(static item => item.Alternatives.Length > 0)
            .Select(static item => item.Alternatives[0])
            .ToArray();
        var words = primaryAlternatives.SelectMany(static alternative => alternative.Words).ToArray();
        var startTimes = words.Where(static word => word.StartTime.HasValue).Select(static word => word.StartTime!.Value).ToArray();
        var endTimes = words.Where(static word => word.EndTime.HasValue).Select(static word => word.EndTime!.Value).ToArray();

        var properties = new AdditionalPropertiesDictionary
        {
            [GoogleCloudSpeechPropertyNames.Results] = response.Results.ToArray(),
            [GoogleCloudSpeechPropertyNames.Alternatives] = resultAlternatives.Select(static item => item.Alternatives).ToArray(),
            [GoogleCloudSpeechPropertyNames.Words] = words,
        };
        if (response.Metadata is not null)
        {
            properties[GoogleCloudSpeechPropertyNames.Metadata] = response.Metadata;
        }

        return new SpeechToTextResponse(
            string.Join(' ', primaryAlternatives.Select(static alternative => alternative.Text).Where(static text => text.Length > 0)))
        {
            ResponseId = NullIfEmpty(response.Metadata?.RequestId),
            ModelId = modelId,
            StartTime = startTimes.Length > 0 ? startTimes.Min() : null,
            EndTime = endTimes.Length > 0 ? endTimes.Max() : null,
            RawRepresentation = response,
            AdditionalProperties = properties,
        };
    }

    /// <summary>Maps one official streaming result to Microsoft.Extensions.AI.</summary>
    public static SpeechToTextResponseUpdate? CreateUpdate(
        StreamingRecognitionResult result,
        RecognitionResponseMetadata? metadata,
        string responseId,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);

        var alternatives = CreateAlternatives(result.Alternatives);
        if (alternatives.Length == 0)
        {
            return null;
        }

        var primary = alternatives[0];
        var startTimes = primary.Words
            .Where(static word => word.StartTime.HasValue)
            .Select(static word => word.StartTime!.Value)
            .ToArray();
        var properties = new AdditionalPropertiesDictionary
        {
            [GoogleCloudSpeechPropertyNames.Alternatives] = alternatives,
            [GoogleCloudSpeechPropertyNames.Words] = primary.Words,
            [GoogleCloudSpeechPropertyNames.Stability] = result.Stability,
        };
        if (!string.IsNullOrWhiteSpace(result.LanguageCode))
        {
            properties[GoogleCloudSpeechPropertyNames.LanguageCode] = result.LanguageCode;
        }
        if (result.ChannelTag > 0)
        {
            properties[GoogleCloudSpeechPropertyNames.ChannelTag] = result.ChannelTag;
        }
        if (metadata is not null)
        {
            properties[GoogleCloudSpeechPropertyNames.Metadata] = metadata;
        }

        return new SpeechToTextResponseUpdate(primary.Text)
        {
            Kind = result.IsFinal
                ? SpeechToTextResponseUpdateKind.TextUpdated
                : SpeechToTextResponseUpdateKind.TextUpdating,
            ResponseId = responseId,
            ModelId = modelId,
            StartTime = startTimes.Length > 0 ? startTimes.Min() : null,
            EndTime = result.ResultEndOffset?.ToTimeSpan(),
            RawRepresentation = result,
            AdditionalProperties = properties,
        };
    }

    private async IAsyncEnumerable<StreamingRecognizeResponse> RecognizeStreamingAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var call = SpeechClient.StreamingRecognize();

        try
        {
            var writerTask = WriteStreamingRequestsAsync(call, audioSpeechStream, options, linkedCancellation.Token);
            try
            {
                var responses = call.GetResponseStream();
                while (await responses.MoveNextAsync(linkedCancellation.Token).ConfigureAwait(false))
                {
                    yield return responses.Current;
                }

                await writerTask.ConfigureAwait(false);
            }
            finally
            {
                await linkedCancellation.CancelAsync().ConfigureAwait(false);
                if (!writerTask.IsCompleted)
                {
                    try
                    {
                        await writerTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                    {
                    }
                }
            }
        }
        finally
        {
            call.Dispose();
        }
    }

    private async Task WriteStreamingRequestsAsync(
        SpeechClient.StreamingRecognizeStream call,
        Stream audioSpeechStream,
        SpeechToTextOptions? options,
        CancellationToken cancellationToken)
    {
        var streamingFeatures = options?.AdditionalProperties?.TryGetValue(
            GoogleCloudSpeechPropertyNames.StreamingFeatures,
            out var streamingFeaturesValue) == true && streamingFeaturesValue is StreamingRecognitionFeatures customFeatures
            ? customFeatures.Clone()
            : new StreamingRecognitionFeatures
            {
                InterimResults = true,
            };

        await call.WriteAsync(new StreamingRecognizeRequest
        {
            Recognizer = Recognizer,
            StreamingConfig = new StreamingRecognitionConfig
            {
                Config = CreateRecognitionConfig(options),
                StreamingFeatures = streamingFeatures,
            },
        }).WaitAsync(cancellationToken).ConfigureAwait(false);

        var chunkSize = GetInt32(options, GoogleCloudSpeechPropertyNames.ChunkSize, 15 * 1024);
        if (chunkSize is <= 0 or > 15 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Google Cloud streaming chunks must be between 1 and 15360 bytes.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            int read;
            while ((read = await audioSpeechStream
                .ReadAsync(buffer.AsMemory(0, chunkSize), cancellationToken)
                .ConfigureAwait(false)) > 0)
            {
                await call.WriteAsync(new StreamingRecognizeRequest
                {
                    Audio = ByteString.CopyFrom(buffer, 0, read),
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await call.WriteCompleteAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private RecognitionConfig CreateRecognitionConfig(SpeechToTextOptions? options)
    {
        RecognitionConfig config;
        if (options?.AdditionalProperties?.TryGetValue(
            GoogleCloudSpeechPropertyNames.RecognitionConfig,
            out var configValue) == true && configValue is RecognitionConfig customConfig)
        {
            config = customConfig.Clone();
        }
        else
        {
            config = new RecognitionConfig
            {
                AutoDecodingConfig = new AutoDetectDecodingConfig(),
                Features = new RecognitionFeatures
                {
                    EnableWordTimeOffsets = true,
                    EnableWordConfidence = true,
                    EnableAutomaticPunctuation = GetBoolean(
                        options,
                        GoogleCloudSpeechPropertyNames.EnableAutomaticPunctuation,
                        defaultValue: true),
                    MaxAlternatives = GetInt32(options, GoogleCloudSpeechPropertyNames.MaxAlternatives, 1),
                },
            };
        }

        if (string.IsNullOrWhiteSpace(config.Model))
        {
            config.Model = GetModelId(options);
        }
        if (config.LanguageCodes.Count == 0 && !string.IsNullOrWhiteSpace(options?.SpeechLanguage))
        {
            config.LanguageCodes.Add(options.SpeechLanguage);
        }

        return config;
    }

    private string GetModelId(SpeechToTextOptions? options) =>
        !string.IsNullOrWhiteSpace(options?.ModelId) ? options.ModelId : _defaultModelId;

    private static GoogleCloudSpeechAlternative[] CreateAlternatives(
        IEnumerable<SpeechRecognitionAlternative> alternatives)
    {
        return alternatives.Select(static alternative => new GoogleCloudSpeechAlternative(
            alternative.Transcript,
            alternative.Confidence,
            alternative.Words.Select(static word => new GoogleCloudSpeechWord(
                word.Word,
                word.StartOffset?.ToTimeSpan(),
                word.EndOffset?.ToTimeSpan(),
                word.Confidence,
                NullIfEmpty(word.SpeakerLabel))).ToArray())).ToArray();
    }

    private static int GetInt32(SpeechToTextOptions? options, string key, int defaultValue)
    {
        if (options?.AdditionalProperties?.TryGetValue(key, out var value) != true)
        {
            return defaultValue;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            _ => defaultValue,
        };
    }

    private static bool GetBoolean(SpeechToTextOptions? options, string key, bool defaultValue) =>
        options?.AdditionalProperties?.TryGetValue(key, out var value) == true && value is bool boolValue
            ? boolValue
            : defaultValue;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
