namespace Vonage_App_POC
{
    public class WorkshopOptions
    {
        public string? From { get; init; }
        public Uri? PublicUrl { get; init; }
        public string? OpenAiKey { get; set; }
        public string? ClaudeApiKey { get; set; }
        public string? GoogleCredentialsPath { get; set; }
        public string? GoogleDriveFolderId { get; set; }
    }
}
