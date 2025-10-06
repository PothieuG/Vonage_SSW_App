namespace Vonage_App_POC.Models.Results;

public class GoogleDriveCallData
{
    public string ConversationUuid { get; set; } = string.Empty;
    public string FolderUrl { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string? RecordingUrl { get; set; }
    public string? TranscriptUrl { get; set; }
    public string? SummaryUrl { get; set; }
    public bool IsProcessingTranscription { get; set; } = false;
    public bool IsTranscriptionComplete { get; set; } = false;
    public bool IsRecordingProcessed { get; set; } = false;
}