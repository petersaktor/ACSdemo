using ACSdemo.Domain.Entities;

namespace ACSdemo.Application.Ports;

public interface IEmailDeliveryReportRepository
{
    Task SaveOrUpdateAsync(EmailDeliveryReport report, CancellationToken ct);
    Task<EmailDeliveryReport?> GetByIdAsync(string messageId, CancellationToken ct);
    Task<IReadOnlyList<EmailDeliveryReport>> GetByRecipientAsync(string recipientAddress, int skip, int take, CancellationToken ct);
}
