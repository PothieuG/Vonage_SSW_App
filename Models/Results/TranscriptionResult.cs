using Newtonsoft.Json;
using System.Text;

namespace Vonage_App_POC.Models.Results
{
    public record TranscriptionResult(
    [property: JsonProperty("ver", Required = Required.Always)]
    string Version,
    [property: JsonProperty("request_id", Required = Required.Always)]
    string RequestId,
    [property: JsonProperty("channels", Required = Required.Always)]
    IReadOnlyList<Channel> Channels
    );

    public record Channel(
    [property: JsonProperty("transcript", Required = Required.Always)]
    IReadOnlyList<Transcript> Transcript,
    [property: JsonProperty("duration", Required = Required.Always)]
    int Duration
)
    {
        public string ExtractTranscript() => this.Transcript.Aggregate(new StringBuilder(), (sb, transcript) => sb.AppendLine(transcript.Sentence)).ToString();
    };

    public record Transcript(
    [property: JsonProperty("sentence", Required = Required.Always)]
    string Sentence,
    [property: JsonProperty("raw_sentence", Required = Required.Always)]
    string RawSentence,
    [property: JsonProperty("duration", Required = Required.Always)]
    int Duration,
    [property: JsonProperty("timestamp", Required = Required.Always)]
    int Timestamp,
    [property: JsonProperty("words", Required = Required.Always)]
    IReadOnlyList<Word> Words
    );

    public record Word(
    [property: JsonProperty("word", Required = Required.Always)]
    string WordText,
    [property: JsonProperty("start_time", Required = Required.Always)]
    int StartTime,
    [property: JsonProperty("end_time", Required = Required.Always)]
    int EndTime,
    [property: JsonProperty("confidence", Required = Required.Always)]
    double Confidence
    );
}
