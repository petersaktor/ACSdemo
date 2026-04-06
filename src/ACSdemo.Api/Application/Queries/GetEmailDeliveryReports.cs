using Ardalis.GuardClauses;
using ACSdemo.Application.Ports;
using ACSdemo.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ACSdemo.Application.Queries;

public record GetEmailDeliveryReportByIdQuery(string MessageId) : IRequest<EmailDeliveryReport?>;

public class GetEmailDeliveryReportByIdHandler : IRequestHandler<GetEmailDeliveryReportByIdQuery, EmailDeliveryReport?>
{
    private readonly IEmailDeliveryReportRepository _repository;
    private readonly ILogger<GetEmailDeliveryReportByIdHandler> _logger;

    public GetEmailDeliveryReportByIdHandler(
        IEmailDeliveryReportRepository repository,
        ILogger<GetEmailDeliveryReportByIdHandler> logger)
    {
        _repository = Guard.Against.Null(repository, nameof(repository));
        _logger = Guard.Against.Null(logger, nameof(logger));
    }

    public async Task<EmailDeliveryReport?> Handle(
        GetEmailDeliveryReportByIdQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching EmailDeliveryReport for MessageId={MessageId}", query.MessageId);
        return await _repository.GetByIdAsync(query.MessageId, cancellationToken);
    }
}

public class GetEmailDeliveryReportByIdValidator : AbstractValidator<GetEmailDeliveryReportByIdQuery>
{
    public GetEmailDeliveryReportByIdValidator()
    {
        RuleFor(q => q.MessageId).NotEmpty();
    }
}

public record GetEmailDeliveryReportsByRecipientQuery(
    string RecipientAddress,
    int Skip = 0,
    int Take = 20) : IRequest<IReadOnlyList<EmailDeliveryReport>>;

public class GetEmailDeliveryReportsByRecipientHandler
    : IRequestHandler<GetEmailDeliveryReportsByRecipientQuery, IReadOnlyList<EmailDeliveryReport>>
{
    private readonly IEmailDeliveryReportRepository _repository;
    private readonly ILogger<GetEmailDeliveryReportsByRecipientHandler> _logger;

    public GetEmailDeliveryReportsByRecipientHandler(
        IEmailDeliveryReportRepository repository,
        ILogger<GetEmailDeliveryReportsByRecipientHandler> logger)
    {
        _repository = Guard.Against.Null(repository, nameof(repository));
        _logger = Guard.Against.Null(logger, nameof(logger));
    }

    public async Task<IReadOnlyList<EmailDeliveryReport>> Handle(
        GetEmailDeliveryReportsByRecipientQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Fetching EmailDeliveryReports for RecipientAddress={RecipientAddress}",
            query.RecipientAddress);
        return await _repository.GetByRecipientAsync(
            query.RecipientAddress, query.Skip, query.Take, cancellationToken);
    }
}

public class GetEmailDeliveryReportsByRecipientValidator
    : AbstractValidator<GetEmailDeliveryReportsByRecipientQuery>
{
    public GetEmailDeliveryReportsByRecipientValidator()
    {
        RuleFor(q => q.RecipientAddress).NotEmpty();
        RuleFor(q => q.Take).InclusiveBetween(1, 100);
    }
}
