using Newtonsoft.Json;

namespace Vonage_App_POC.Models.Webhooks
{
    public record TranscribedWebhook(
    [property: JsonProperty("conversation_uuid", Required = Required.Always)]
    string ConversationUuid,
    [property: JsonProperty("type", Required = Required.Always)]
    string Type,
    [property: JsonProperty("recording_uuid", Required = Required.Always)]
    string RecordingUuid,
    [property: JsonProperty("status", Required = Required.Always)]
    string Status,
    [property: JsonProperty("transcription_url", Required = Required.Always)]
    Uri TranscriptionUrl);
}
