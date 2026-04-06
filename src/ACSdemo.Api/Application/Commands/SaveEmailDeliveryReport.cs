using Ardalis.GuardClauses;
using ACSdemo.Application.Ports;
using ACSdemo.Domain.Entities;
using ACSdemo.Web.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ACSdemo.Application.Commands;

public record SaveEmailDeliveryReportCommand(EmailDeliveryReportReceivedEvent Event) : IRequest<Unit>;

public class SaveEmailDeliveryReportHandler : IRequestHandler<SaveEmailDeliveryReportCommand, Unit>
{
    private readonly IEmailDeliveryReportRepository _repository;
    private readonly ILogger<SaveEmailDeliveryReportHandler> _logger;

    public SaveEmailDeliveryReportHandler(
        IEmailDeliveryReportRepository repository,
        ILogger<SaveEmailDeliveryReportHandler> logger)
    {
        _repository = Guard.Against.Null(repository, nameof(repository));
        _logger = Guard.Against.Null(logger, nameof(logger));
    }

    public async Task<Unit> Handle(SaveEmailDeliveryReportCommand request, CancellationToken cancellationToken)
    {
        var ev = request.Event;

        var existing = await _repository.GetByIdAsync(ev.MessageId, cancellationToken);

        if (existing is not null && existing.HasStatusEntry(ev.DeliveryAttemptTimeStamp))
        {
            _logger.LogDebug(
                "Skipping duplicate event for MessageId={MessageId} Timestamp={Timestamp}",
                ev.MessageId, ev.DeliveryAttemptTimeStamp);
            return Unit.Value;
        }

        if (existing is not null)
        {
            existing.UpdateStatus(ev.Status, ev.DeliveryStatusDetails?.StatusMessage, ev.DeliveryAttemptTimeStamp);
            await _repository.SaveOrUpdateAsync(existing, cancellationToken);
        }
        else
        {
            var report = new EmailDeliveryReport(
                id: ev.MessageId,
                recipientAddress: ev.Recipient,
                senderAddress: ev.Sender,
                currentStatus: ev.Status,
                lastEventTimestamp: ev.DeliveryAttemptTimeStamp,
                statusHistory: [new DeliveryStatusEntry(ev.Status, ev.DeliveryStatusDetails?.StatusMessage, ev.DeliveryAttemptTimeStamp)]);

            await _repository.SaveOrUpdateAsync(report, cancellationToken);
        }

        return Unit.Value;
    }
}

public class SaveEmailDeliveryReportValidator : AbstractValidator<SaveEmailDeliveryReportCommand>
{
    public SaveEmailDeliveryReportValidator()
    {
        RuleFor(x => x.Event.MessageId).NotEmpty();
        RuleFor(x => x.Event.Recipient).NotEmpty();
        RuleFor(x => x.Event.Status).NotNull();
    }
}
