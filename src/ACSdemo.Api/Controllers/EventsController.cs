using Microsoft.AspNetCore.Mvc;
using ACSdemo.Api.Models;
using ACSdemo.Api.Services;

namespace ACSdemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly ICommunicationService _comms;
    private readonly ILogger<EventsController> _logger;

    public EventsController(ICommunicationService comms, ILogger<EventsController> logger)
    {
        _comms  = comms;
        _logger = logger;
    }

    /// <summary>Dapr subscriber: processes send-email events from pub/sub.</summary>
    [HttpPost("send-email")]
    [Dapr.Topic("pubsub", "send-email")]
    public async Task<IActionResult> HandleSendEmail([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Received send-email event for {To}", request.To);
        await _comms.SendEmailAsync(request, ct);
        return Ok();
    }

    /// <summary>Dapr subscriber: processes send-sms events from pub/sub.</summary>
    [HttpPost("send-sms")]
    [Dapr.Topic("pubsub", "send-sms")]
    public async Task<IActionResult> HandleSendSms([FromBody] SendSmsRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Received send-sms event for {To}", request.To);
        await _comms.SendSmsAsync(request, ct);
        return Ok();
    }
}
