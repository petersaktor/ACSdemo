using Ardalis.GuardClauses;
using ACSdemo.Application.Ports;
using ACSdemo.Domain.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ACSdemo.Infrastructure.Persistence;

public class CosmosEmailDeliveryReportRepository : IEmailDeliveryReportRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosEmailDeliveryReportRepository> _logger;

    public CosmosEmailDeliveryReportRepository(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosEmailDeliveryReportRepository> logger)
    {
        Guard.Against.Null(cosmosClient, nameof(cosmosClient));
        Guard.Against.Null(configuration, nameof(configuration));
        _logger = Guard.Against.Null(logger, nameof(logger));

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "ACSdemo";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "EmailDeliveryReports";

        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task SaveOrUpdateAsync(EmailDeliveryReport report, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(report, nameof(report));

        _logger.LogDebug("Upserting EmailDeliveryReport {Id} for {Recipient}", report.Id, report.RecipientAddress);

        await _container.UpsertItemAsync(
            report,
            new PartitionKey(report.RecipientAddress),
            cancellationToken: cancellationToken);
    }

    public async Task<EmailDeliveryReport?> GetByIdAsync(string messageId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(messageId, nameof(messageId));

        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @messageId")
            .WithParameter("@messageId", messageId);

        using var iterator = _container.GetItemQueryIterator<EmailDeliveryReport>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var item = response.FirstOrDefault();
            if (item is not null)
                return item;
        }

        return null;
    }

    public async Task<IReadOnlyList<EmailDeliveryReport>> GetByRecipientAsync(
        string recipientAddress,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(recipientAddress, nameof(recipientAddress));
        Guard.Against.Negative(skip, nameof(skip));
        Guard.Against.NegativeOrZero(take, nameof(take));

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.recipientAddress = @recipient ORDER BY c.lastEventTimestamp DESC OFFSET @skip LIMIT @take")
            .WithParameter("@recipient", recipientAddress)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        var results = new List<EmailDeliveryReport>();

        using var iterator = _container.GetItemQueryIterator<EmailDeliveryReport>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(recipientAddress)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
