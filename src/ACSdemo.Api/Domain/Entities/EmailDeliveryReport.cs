using Ardalis.GuardClauses;

namespace ACSdemo.Domain.Entities;

public class EmailDeliveryReport
{
    public string Id { get; private set; }
    public string RecipientAddress { get; private set; }
    public string SenderAddress { get; private set; }
    public string CurrentStatus { get; private set; }
    public DateTimeOffset LastEventTimestamp { get; private set; }
    public List<DeliveryStatusEntry> StatusHistory { get; private set; }

    public EmailDeliveryReport(
        string id,
        string recipientAddress,
        string senderAddress,
        string currentStatus,
        DateTimeOffset lastEventTimestamp,
        List<DeliveryStatusEntry> statusHistory)
    {
        Id = Guard.Against.NullOrEmpty(id, nameof(id));
        RecipientAddress = Guard.Against.NullOrEmpty(recipientAddress, nameof(recipientAddress));
        SenderAddress = Guard.Against.NullOrEmpty(senderAddress, nameof(senderAddress));
        CurrentStatus = Guard.Against.NullOrEmpty(currentStatus, nameof(currentStatus));
        LastEventTimestamp = lastEventTimestamp;
        StatusHistory = Guard.Against.Null(statusHistory, nameof(statusHistory));
    }

    public bool HasStatusEntry(DateTimeOffset timestamp) =>
        StatusHistory.Any(e => e.Timestamp == timestamp);

    public void UpdateStatus(string status, string? statusMessage, DateTimeOffset timestamp)
    {
        CurrentStatus = Guard.Against.NullOrEmpty(status, nameof(status));
        LastEventTimestamp = timestamp;
        StatusHistory.Add(new DeliveryStatusEntry(status, statusMessage, timestamp));
    }

    // Parameterless constructor required for Cosmos DB deserialization
    private EmailDeliveryReport()
    {
        Id = string.Empty;
        RecipientAddress = string.Empty;
        SenderAddress = string.Empty;
        CurrentStatus = string.Empty;
        LastEventTimestamp = DateTimeOffset.MinValue;
        StatusHistory = new List<DeliveryStatusEntry>();
    }
}

public record DeliveryStatusEntry(
    string Status,
    string? StatusMessage,
    DateTimeOffset Timestamp);
