using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Vonage;
using Vonage.Messages;
using Vonage.Messages.Sms;
using Vonage.Request;
using Vonage.Voice;
using Vonage_App_POC.Models.Webhooks;
using Vonage_App_POC.Models.Results;
using Vonage_App_POC.Models.Responses;

namespace Vonage_App_POC.Services;

public class TranscriptionService
{
    private readonly IVoiceClient _voiceClient;
    private readonly Credentials _credentials;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IMessagesClient _messagesClient;
    private readonly IOptions<WorkshopOptions> _workshopOptions;
    private readonly GoogleDriveService _googleDriveService;
    private readonly CallDataService _callDataService;
    private readonly IClaudeAiService _claudeAiService;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(
        IVoiceClient voiceClient,
        Credentials credentials,
        ITokenGenerator tokenGenerator,
        IMessagesClient messagesClient,
        IOptions<WorkshopOptions> workshopOptions,
        GoogleDriveService googleDriveService,
        CallDataService callDataService,
        IClaudeAiService claudeAiService,
        ILogger<TranscriptionService> logger)
    {
        _voiceClient = voiceClient ?? throw new ArgumentNullException(nameof(voiceClient));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _messagesClient = messagesClient ?? throw new ArgumentNullException(nameof(messagesClient));
        _workshopOptions = workshopOptions ?? throw new ArgumentNullException(nameof(workshopOptions));
        _googleDriveService = googleDriveService ?? throw new ArgumentNullException(nameof(googleDriveService));
        _callDataService = callDataService ?? throw new ArgumentNullException(nameof(callDataService));
        _claudeAiService = claudeAiService ?? throw new ArgumentNullException(nameof(claudeAiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task HandleTranscription(TranscribedWebhook transcribedWebhook)
    {
        try
        {
            _logger.LogInformation("Processing transcription for recording {RecordingUuid} in conversation {ConversationUuid}",
                transcribedWebhook.RecordingUuid, transcribedWebhook.ConversationUuid);

            var callData = _callDataService.GetCallData(transcribedWebhook.ConversationUuid);
            if (callData == null)
                throw new InvalidOperationException($"Call data not found for conversation: {transcribedWebhook.ConversationUuid}");

            if (callData.IsTranscriptionComplete)
            {
                _logger.LogInformation("Transcription already processed for recording {RecordingUuid}", transcribedWebhook.RecordingUuid);
                return;
            }

            if (callData.IsProcessingTranscription)
            {
                _logger.LogInformation("Transcription is already being processed for recording {RecordingUuid}", transcribedWebhook.RecordingUuid);
                return;
            }

            callData.IsProcessingTranscription = true;
            _callDataService.StoreCallData(transcribedWebhook.ConversationUuid, callData);

            var call = await FindCallRecord(transcribedWebhook);
            var transcriptionResponse = await RetrieveTranscription(transcribedWebhook.TranscriptionUrl);

            var summary = await _claudeAiService.GenerateSummary(transcriptionResponse.Transcription);

            var transcriptFileName = $"Transcript_{transcribedWebhook.RecordingUuid}.txt";
            var transcriptUrl = await _googleDriveService.UploadTextFileAsync(
                transcriptionResponse.Transcription,
                transcriptFileName,
                callData.FolderId);

            var summaryFileName = $"Summary_{transcribedWebhook.RecordingUuid}.txt";
            var summaryUrl = await _googleDriveService.UploadTextFileAsync(
                summary,
                summaryFileName,
                callData.FolderId);

            callData.TranscriptUrl = transcriptUrl;
            callData.SummaryUrl = summaryUrl;
            callData.IsProcessingTranscription = false;
            callData.IsTranscriptionComplete = true;
            _callDataService.StoreCallData(transcribedWebhook.ConversationUuid, callData);

            var messageText = BuildMessageContent(call, summary, callData.FolderUrl);
            await SendSmsNotification(call.To.Number, messageText);

            _logger.LogInformation("Transcription processed and SMS sent for conversation {ConversationUuid}",
                transcribedWebhook.ConversationUuid);
        }
        catch (Exception ex)
        {
            var callData = _callDataService.GetCallData(transcribedWebhook.ConversationUuid);
            if (callData != null)
            {
                callData.IsProcessingTranscription = false;
                _callDataService.StoreCallData(transcribedWebhook.ConversationUuid, callData);
            }

            _logger.LogError(ex, "Error processing transcription for recording {RecordingUuid} in conversation {ConversationUuid}",
                transcribedWebhook.RecordingUuid, transcribedWebhook.ConversationUuid);
            throw;
        }
    }

    private async Task<CallRecord> FindCallRecord(TranscribedWebhook transcribedWebhook)
    {
        var calls = await _voiceClient.GetCallsAsync(new CallSearchFilter
        {
            ConversationUuid = transcribedWebhook.ConversationUuid
        });

        var call = calls.Embedded.Calls.FirstOrDefault();
        if (call == null)
            throw new InvalidOperationException($"No call found for conversation: {transcribedWebhook.ConversationUuid}");

        return call;
    }

    private async Task SendSmsNotification(string phoneNumber, string message)
    {
        var request = new SmsRequest
        {
            From = _workshopOptions.Value.From,
            To = phoneNumber,
            Text = message
        };

        try
        {
            var result = await _messagesClient.SendAsync(request);
            _logger.LogInformation("SMS sent successfully to {PhoneNumber}. Result: {Result}", phoneNumber, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            throw;
        }
    }

    private static string BuildMessageContent(CallRecord call, string summary, string googleDriveFolderUrl)
    {
        var builder = new StringBuilder();
        builder.AppendLine("From: #hidden");
        builder.AppendLine($"Duration: {call.Duration}s");
        builder.AppendLine($"Summary: {summary}");
        builder.AppendLine($"Files: {googleDriveFolderUrl}");
        return builder.ToString();
    }

    private async Task<RetrieveTranscriptionResponse> RetrieveTranscription(Uri url)
    {
        using var httpClient = new HttpClient();
        var token = _tokenGenerator.GenerateToken(_credentials).GetSuccessUnsafe();

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Empty transcription response received.");

            var transcription = JsonConvert.DeserializeObject<TranscriptionResult>(content);
            if (transcription?.Channels == null || transcription.Channels.Count == 0)
                throw new InvalidOperationException("Invalid transcription format: no channels found.");

            return new RetrieveTranscriptionResponse(transcription.Channels[0].ExtractTranscript());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error retrieving transcription from {Url}", url);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for transcription from {Url}", url);
            throw;
        }
    }
}