using Microsoft.AspNetCore.Mvc;
using Vonage_App_POC.Models.Requests;
using Vonage_App_POC.Models.Responses;
using Vonage_App_POC.Models.Webhooks;
using Vonage_App_POC.Services;

namespace Vonage_App_POC.Controllers;

[ApiController]
[Route("[controller]")]
public class CallController : ControllerBase
{
    private readonly CallService _callService;
    private readonly RecordingService _recordingService;
    private readonly TranscriptionService _transcriptionService;
    private readonly ILogger<CallController> _logger;

    public CallController(
        CallService callService,
        RecordingService recordingService,
        TranscriptionService transcriptionService,
        ILogger<CallController> logger)
    {
        _callService = callService ?? throw new ArgumentNullException(nameof(callService));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<IActionResult> Call(InitiateCallRequest input)
    {
        if (input?.To is null or { Length: 0 })
            return BadRequest("Phone number is required.");

        try
        {
            var response = await _callService.InitiateCall(input);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid phone number format: {PhoneNumber}", input.To);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating call to {PhoneNumber}", input.To);
            return StatusCode(500, "Failed to initiate call.");
        }
    }

    [HttpPost("recorded")]
    public async Task<IActionResult> OnRecorded(RecordedWebhook recordedWebhook)
    {
        if (recordedWebhook == null)
        {
            _logger.LogWarning("Received null RecordedWebhook");
            return NoContent();
        }

        try
        {
            await _recordingService.HandleRecorded(recordedWebhook);
            _logger.LogInformation("Recording processed successfully for conversation {ConversationUuid}",
                recordedWebhook.ConversationUuid);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recording for conversation {ConversationUuid}",
                recordedWebhook.ConversationUuid);
            return NoContent();
        }
    }


    [HttpPost("transcribed")]
    public async Task<IActionResult> OnTranscribed(TranscribedWebhook transcribedWebhook)
    {
        if (transcribedWebhook == null)
        {
            _logger.LogWarning("Received null TranscribedWebhook");
            return NoContent();
        }

        _logger.LogInformation("Processing transcription for conversation {ConversationUuid}",
            transcribedWebhook.ConversationUuid);

        try
        {
            await _transcriptionService.HandleTranscription(transcribedWebhook);
            _logger.LogInformation("Transcription processed successfully for conversation {ConversationUuid}",
                transcribedWebhook.ConversationUuid);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transcription for conversation {ConversationUuid}",
                transcribedWebhook.ConversationUuid);
            return NoContent();
        }
    }
}
