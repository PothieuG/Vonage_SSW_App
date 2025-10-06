using System.Collections.Concurrent;
using Vonage_App_POC.Models.Results;

namespace Vonage_App_POC.Services;

public class CallDataService
{
    private readonly ConcurrentDictionary<string, GoogleDriveCallData> _callData = new();
    private readonly ILogger<CallDataService> _logger;

    public CallDataService(ILogger<CallDataService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void StoreCallData(string conversationUuid, GoogleDriveCallData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationUuid);
        ArgumentNullException.ThrowIfNull(data);

        _callData.AddOrUpdate(conversationUuid, data, (key, existing) => data);
        _logger.LogDebug("Stored call data for conversation {ConversationUuid}", conversationUuid);
    }

    public GoogleDriveCallData? GetCallData(string conversationUuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationUuid);

        var found = _callData.TryGetValue(conversationUuid, out var data);
        if (!found)
            _logger.LogWarning("Call data not found for conversation {ConversationUuid}", conversationUuid);

        return data;
    }

    public void UpdateRecordingUrl(string conversationUuid, string recordingUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordingUrl);

        if (_callData.TryGetValue(conversationUuid, out var data))
        {
            data.RecordingUrl = recordingUrl;
            _logger.LogDebug("Updated recording URL for conversation {ConversationUuid}", conversationUuid);
        }
        else
        {
            _logger.LogWarning("Cannot update recording URL: call data not found for conversation {ConversationUuid}", conversationUuid);
        }
    }

    public void UpdateTranscriptUrl(string conversationUuid, string transcriptUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptUrl);

        if (_callData.TryGetValue(conversationUuid, out var data))
        {
            data.TranscriptUrl = transcriptUrl;
            _logger.LogDebug("Updated transcript URL for conversation {ConversationUuid}", conversationUuid);
        }
        else
        {
            _logger.LogWarning("Cannot update transcript URL: call data not found for conversation {ConversationUuid}", conversationUuid);
        }
    }

    public void RemoveCallData(string conversationUuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationUuid);

        var removed = _callData.TryRemove(conversationUuid, out _);
        if (removed)
            _logger.LogDebug("Removed call data for conversation {ConversationUuid}", conversationUuid);
        else
            _logger.LogWarning("Cannot remove call data: not found for conversation {ConversationUuid}", conversationUuid);
    }

    public int GetActiveCallCount() => _callData.Count;

    public IEnumerable<string> GetActiveConversations() => _callData.Keys.ToList();
}