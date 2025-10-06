using Newtonsoft.Json;

namespace Vonage_App_POC.Models.Webhooks
{
    public record RecordedWebhook(
    [property: JsonProperty("start_time", Required = Required.Always)]
    DateTime StartTime,
    [property: JsonProperty("recording_url", Required = Required.Always)]
    string RecordingUrl,
    [property: JsonProperty("size", Required = Required.Always)]
    int Size,
    [property: JsonProperty("recording_uuid", Required = Required.Always)]
    string RecordingUuid,
    [property: JsonProperty("end_time", Required = Required.Always)]
    DateTime EndTime,
    [property: JsonProperty("conversation_uuid", Required = Required.Always)]
    string ConversationUuid,
    [property: JsonProperty("timestamp", Required = Required.Always)]
    DateTime Timestamp);
}
