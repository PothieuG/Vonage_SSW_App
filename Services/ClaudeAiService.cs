using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Options;

namespace Vonage_App_POC.Services
{
    public interface IClaudeAiService
    {
        Task<string> GenerateSummary(string transcript);
    }

    public class ClaudeAiService : IClaudeAiService
    {
        private readonly AnthropicClient _client;
        private readonly ILogger<ClaudeAiService> _logger;

        public ClaudeAiService(IOptions<WorkshopOptions> workshopOptions, ILogger<ClaudeAiService> logger)
        {
            _logger = logger;

            var apiKey = workshopOptions.Value.ClaudeApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("ClaudeApiKey is not configured in workshopOptions");
            }

            _client = new AnthropicClient(apiKey);
        }

        public async Task<string> GenerateSummary(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning("Empty transcript provided for summarization");
                return "Aucune transcription disponible.";
            }

            try
            {
                _logger.LogInformation("Generating summary for transcript of length: {Length}", transcript.Length);

                var prompt = $@"You are an assistant that summarizes voice messages in French.
                    Here is the transcription of a voice message:

                    {transcript}

                    Create a concise and clear summary of this message in French, in a maximum of 2–3 sentences.
                    Focus on the key points and the intent of the message.
                    Do not mention that it is a transcription or a summary.";

                var messages = new List<Message>
                {
                    new Message(RoleType.User, prompt)
                };

                var parameters = new MessageParameters()
                {
                    Messages = messages,
                    MaxTokens = 150, // Limite pour résumé court
                    Model = AnthropicModels.Claude3Haiku, // Modèle le plus rapide et économique
                    Stream = false,
                    Temperature = 0.7m // Légèrement créatif mais cohérent
                };

                var result = await _client.Messages.GetClaudeMessageAsync(parameters);
                var summary = result.Message.ToString().Trim();

                _logger.LogInformation("Summary generated successfully, length: {Length}", summary.Length);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary for transcript");
                // En cas d'erreur, retourner la transcription originale tronquée
                return transcript.Length > 200
                    ? transcript.Substring(0, 200) + "..."
                    : transcript;
            }
        }
    }
}