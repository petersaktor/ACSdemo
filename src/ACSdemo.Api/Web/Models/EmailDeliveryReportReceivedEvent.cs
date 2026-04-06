using System.Text.Json.Serialization;

namespace ACSdemo.Web.Models;

public record EmailDeliveryReportReceivedEvent(
    [property: JsonPropertyName("sender")] string Sender,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("deliveryStatusDetails")] DeliveryStatusDetails? DeliveryStatusDetails,
    [property: JsonPropertyName("deliveryAttemptTimeStamp")] DateTimeOffset DeliveryAttemptTimeStamp);

public record DeliveryStatusDetails(
    [property: JsonPropertyName("statusMessage")] string? StatusMessage);
