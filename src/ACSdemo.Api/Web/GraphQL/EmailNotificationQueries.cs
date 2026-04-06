using ACSdemo.Application.Queries;
using ACSdemo.Domain.Entities;
using ACSdemo.Web.GraphQL.Interfaces;
using HotChocolate;
using HotChocolate.Types;
using MediatR;

namespace ACSdemo.Web.GraphQL;

[QueryType]
public class EmailNotificationQueries : IGraphQLQuery
{
    public async Task<EmailDeliveryReport?> GetEmailDeliveryReportAsync(
        string messageId,
        [Service] IMediator mediator,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetEmailDeliveryReportByIdQuery(messageId), cancellationToken);
    }

    public async Task<EmailDeliveryReportConnection> GetEmailDeliveryReportsByRecipientAsync(
        string recipientAddress,
        int skip,
        int take,
        [Service] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new GetEmailDeliveryReportsByRecipientQuery(recipientAddress, skip, take),
            cancellationToken);

        return new EmailDeliveryReportConnection(items, items.Count == take);
    }
}

public record EmailDeliveryReportConnection(
    IReadOnlyList<EmailDeliveryReport> Items,
    bool HasNextPage);
