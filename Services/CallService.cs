using Microsoft.Extensions.Options;
using Vonage.Voice;
using Vonage.Voice.Nccos;
using Vonage.Voice.Nccos.Endpoints;
using Vonage_App_POC.Models.Requests;
using Vonage_App_POC.Models.Responses;

namespace Vonage_App_POC.Services;

public class CallService
{
    private readonly IVoiceClient _voiceClient;
    private readonly IOptions<WorkshopOptions> _workshopOptions;
    private readonly ILogger<CallService> _logger;

    public CallService(
        IVoiceClient voiceClient,
        IOptions<WorkshopOptions> workshopOptions,
        ILogger<CallService> logger)
    {
        _voiceClient = voiceClient ?? throw new ArgumentNullException(nameof(voiceClient));
        _workshopOptions = workshopOptions ?? throw new ArgumentNullException(nameof(workshopOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitiateCallResponse> InitiateCall(InitiateCallRequest request)
    {
        var options = _workshopOptions.Value;
        if (options.PublicUrl == null)
            throw new InvalidOperationException("PublicUrl is not configured.");

        var formattedToNumber = FormatPhoneNumber(request.To);
        if (string.IsNullOrWhiteSpace(formattedToNumber))
            throw new ArgumentException($"Invalid phone number format: {request.To}");

        var formattedFromNumber = FormatPhoneNumber(options.From ?? string.Empty);
        if (string.IsNullOrWhiteSpace(formattedFromNumber))
            throw new ArgumentException($"Invalid from number configuration: {options.From}");

        var ncco = CreateNcco(options.PublicUrl);
        var callCommand = new CallCommand
        {
            To = new[] { new PhoneEndpoint { Number = formattedToNumber } },
            From = new PhoneEndpoint { Number = formattedFromNumber },
            Ncco = ncco
        };

        try
        {
            var callResponse = await _voiceClient.CreateCallAsync(callCommand);
            _logger.LogInformation("Call initiated successfully. UUID: {CallUuid}", callResponse.Uuid);

            return new InitiateCallResponse(
                Guid.Parse(callResponse.Uuid),
                callResponse.Status.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate call to {PhoneNumber}", formattedToNumber);
            throw;
        }
    }

    private static Ncco CreateNcco(Uri publicUrl)
    {
        var talkAction = new TalkAction
        {
            Text = "Bonjour, veuillez laisser un message après le bip svp.",
            Language = "fr-FR",
            Style = 1
        };

        var recordAction = new RecordAction
        {
            EventUrl = new[] { $"{publicUrl.AbsoluteUri}call/recorded" },
            EndOnSilence = "3",
            BeepStart = true,
            Transcription = new RecordAction.TranscriptionSettings
            {
                EventUrl = new[] { $"{publicUrl.AbsoluteUri}call/transcribed" },
                Language = "fr-FR"
            }
        };

        return new Ncco(talkAction, recordAction);
    }

    private static string FormatPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var cleaned = phoneNumber.Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(".", "");

        return cleaned switch
        {
            var s when s.StartsWith("+") && s[1..].All(char.IsDigit) && s.Length <= 16 => s,
            var s when s.StartsWith("0") && s.Length == 10 && s.All(char.IsDigit) => $"+33{s[1..]}",
            var s when s.All(char.IsDigit) && s.Length <= 15 => $"+{s}",
            _ => string.Empty
        };
    }
}
