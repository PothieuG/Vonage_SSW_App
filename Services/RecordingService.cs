using Vonage.Voice;
using Vonage_App_POC.Models.Webhooks;
using Vonage_App_POC.Models.Results;

namespace Vonage_App_POC.Services;

public class RecordingService
{
    private readonly IVoiceClient _voiceClient;
    private readonly GoogleDriveService _googleDriveService;
    private readonly CallDataService _callDataService;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(
        IVoiceClient voiceClient,
        GoogleDriveService googleDriveService,
        CallDataService callDataService,
        ILogger<RecordingService> logger)
    {
        _voiceClient = voiceClient ?? throw new ArgumentNullException(nameof(voiceClient));
        _googleDriveService = googleDriveService ?? throw new ArgumentNullException(nameof(googleDriveService));
        _callDataService = callDataService ?? throw new ArgumentNullException(nameof(callDataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleRecorded(RecordedWebhook recordedWebhook)
    {
        var localPath = string.Empty;

        try
        {
            _logger.LogInformation("Processing recording {RecordingUuid} for conversation {ConversationUuid}",
                recordedWebhook.RecordingUuid, recordedWebhook.ConversationUuid);

            var existingCallData = _callDataService.GetCallData(recordedWebhook.ConversationUuid);
            if (existingCallData?.IsRecordingProcessed == true)
            {
                _logger.LogInformation("Recording already processed for conversation {ConversationUuid}", recordedWebhook.ConversationUuid);
                return;
            }

            var response = await _voiceClient.GetRecordingAsync(recordedWebhook.RecordingUrl);
            localPath = Path.Combine(Path.GetTempPath(), $"Recording_{recordedWebhook.RecordingUuid}.mp3");
            await File.WriteAllBytesAsync(localPath, response.ResultStream);

            var folderUrl = await _googleDriveService.CreateCallFolderAsync(recordedWebhook.ConversationUuid);
            var folderId = _googleDriveService.ExtractFolderIdFromUrl(folderUrl);

            if (string.IsNullOrEmpty(folderId))
                throw new InvalidOperationException("Failed to extract folder ID from Google Drive URL");

            var fileName = $"Recording_{recordedWebhook.RecordingUuid}.mp3";
            var recordingUrl = await _googleDriveService.UploadFileAsync(localPath, fileName, "audio/mpeg", folderId);

            var callData = new GoogleDriveCallData
            {
                ConversationUuid = recordedWebhook.ConversationUuid,
                FolderUrl = folderUrl,
                FolderId = folderId,
                RecordingUrl = recordingUrl,
                IsRecordingProcessed = true
            };

            _callDataService.StoreCallData(recordedWebhook.ConversationUuid, callData);

            _logger.LogInformation("Recording processed and uploaded successfully for conversation {ConversationUuid}",
                recordedWebhook.ConversationUuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recording {RecordingUuid} for conversation {ConversationUuid}",
                recordedWebhook.RecordingUuid, recordedWebhook.ConversationUuid);
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}", localPath);
                }
            }
        }
    }
}