using Ardalis.GuardClauses;
using ACSdemo.Application.Commands;
using ACSdemo.Web.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ACSdemo.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailNotificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmailNotificationController> _logger;

    public EmailNotificationController(IMediator mediator, ILogger<EmailNotificationController> logger)
    {
        _mediator = Guard.Against.Null(mediator, nameof(mediator));
        _logger = Guard.Against.Null(logger, nameof(logger));
    }

    [HttpPost("email-delivery-report")]
    [Dapr.Topic("pubsub", "EmailDeliveryReportReceived")]
    public async Task<IActionResult> HandleEmailDeliveryReport(
        [FromBody] EmailDeliveryReportReceivedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received EmailDeliveryReportReceived event for MessageId={MessageId}",
            @event.MessageId);

        await _mediator.Send(new SaveEmailDeliveryReportCommand(@event), cancellationToken);

        return Ok();
    }
}
