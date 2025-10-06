using ChatGPT.Net;
using ChatGPT.Net.DTO.ChatGPT;
using Microsoft.Extensions.Options;
using Vonage_App_POC.Models.Responses;

namespace Vonage_App_POC.Services;

public class AiService
{
    private readonly IOptions<WorkshopOptions> _workshopOptions;
    private readonly ILogger<AiService> _logger;
    private const string DefaultPrompt = "Create a summary of the voice message I just received. " +
                                        "The summary will be sent over text messages, so keep it short and focus on the main information. " +
                                        "Only respond with the summary. Here it is: ";

    public AiService(IOptions<WorkshopOptions> workshopOptions, ILogger<AiService> logger)
    {
        _workshopOptions = workshopOptions ?? throw new ArgumentNullException(nameof(workshopOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateSummary(RetrieveTranscriptionResponse transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        if (string.IsNullOrWhiteSpace(_workshopOptions.Value.OpenAiKey))
            throw new InvalidOperationException("OpenAiKey is not configured.");

        if (string.IsNullOrWhiteSpace(transcript.Transcription))
        {
            _logger.LogWarning("Empty transcription provided for summarization");
            return "Aucune transcription disponible.";
        }

        try
        {
            _logger.LogInformation("Generating summary for transcription of length: {Length}", transcript.Transcription.Length);

            var client = new ChatGpt(
                _workshopOptions.Value.OpenAiKey,
                new ChatGptOptions
                {
                    Model = "gpt-4-turbo"
                });

            var summary = await client.Ask($"{DefaultPrompt}\n{transcript.Transcription}");

            _logger.LogInformation("Summary generated successfully, length: {Length}", summary.Length);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary for transcription");
            return transcript.Transcription.Length > 200
                ? transcript.Transcription[..200] + "..."
                : transcript.Transcription;
        }
    }

    public async Task<string> GenerateSummary(string transcription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcription);

        if (string.IsNullOrWhiteSpace(_workshopOptions.Value.OpenAiKey))
            throw new InvalidOperationException("OpenAiKey is not configured.");

        try
        {
            _logger.LogInformation("Generating summary for transcription of length: {Length}", transcription.Length);

            var client = new ChatGpt(
                _workshopOptions.Value.OpenAiKey,
                new ChatGptOptions
                {
                    Model = "gpt-4-turbo"
                });

            var summary = await client.Ask($"{DefaultPrompt}\n{transcription}");

            _logger.LogInformation("Summary generated successfully, length: {Length}", summary.Length);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary for transcription");
            return transcription.Length > 200
                ? transcription[..200] + "..."
                : transcription;
        }
    }
}
