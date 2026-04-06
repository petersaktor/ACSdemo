# ACSdemo

Azure Container App using **Dapr** and **Azure Communication Services (ACS)**.

## Features
- Send emails via ACS Email
- Send SMS via ACS SMS
- Dapr pub/sub (publish messages to topics, processed asynchronously)
- Dapr state store
- Azure Container Apps deployment with Bicep

## Prerequisites
- .NET 10 SDK
- Docker Desktop
- Dapr CLI (`dapr init`)
- Azure CLI

## Local Development

```bash
# Run with Docker Compose (includes Redis + Dapr sidecar)
docker-compose up --build

# Or run directly with Dapr CLI
dapr run --app-id acsdemo-api --app-port 5000 --components-path ./dapr/components \
  -- dotnet run --project src/ACSdemo.Api
```

## Configuration

Set your ACS connection string in `appsettings.Development.json` or via user secrets:
```bash
cd src/ACSdemo.Api
dotnet user-secrets set "AzureCommunicationServices:ConnectionString" "<your-connection-string>"
```

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/messages/email | Send email directly |
| POST | /api/messages/sms | Send SMS directly |
| POST | /api/messages/email/publish | Publish email to Dapr pub/sub |
| POST | /api/messages/sms/publish | Publish SMS to Dapr pub/sub |

Swagger UI available at: `http://localhost:5000/swagger`

## Deploy to Azure

```bash
# Login and set subscription
az login
az account set --subscription "<your-subscription>"

# Deploy infrastructure
az deployment group create \
  --resource-group rg-acsdemo \
  --template-file deploy/main.bicep \
  --parameters namePrefix=acsdemo acsConnectionString="<your-acs-conn-str>"
```
