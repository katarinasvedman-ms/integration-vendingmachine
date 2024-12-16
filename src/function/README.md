# ConfigurationHelper Azure Function

## Overview

The `ConfigurationHelper` Azure Function is designed to manage configuration settings stored in Azure Blob Storage and Azure Key Vault. It provides two main functionalities via HTTP triggers:

1. **GET Request**:
   - Retrieves a JSON configuration file from Azure Blob Storage.
   - Deserializes the JSON content into a `Configuration` object.
   - Returns the configuration data as a JSON response.

2. **POST Request**:
   - Accepts a JSON payload containing configuration settings.
   - For each key-value pair in the configuration:
     - If the key is valid (matches a specific pattern), the value is stored in Azure Key Vault, and the value in the JSON is replaced with a Key Vault reference.
     - If the key is not valid, the key-value pair is retained in the JSON.
   - Updates the JSON configuration file in Azure Blob Storage with the modified values.
   - Triggers a GitHub Actions workflow after updating the blob.
   - Returns a success message indicating that the secrets have been added to Key Vault and the JSON file has been updated in Azure Storage.

## Environment Variables

The function relies on the following environment variables, which can be set in a `.env` file for local development or in the Azure Function App settings for deployment:

- `AZURE_STORAGE_BLOB_SERVICE_URI`: The URI of the Azure Blob Storage service.
- `AZURE_STORAGE_CONTAINER_NAME`: The name of the container in Azure Blob Storage.
- `AZURE_STORAGE_FILE_NAME`: The name of the JSON configuration file in Azure Blob Storage.
- `AZURE_KEY_VAULT_URI`: The URI of the Azure Key Vault.
- `GITHUB_TOKEN`: A GitHub personal access token with `repo` and `workflow` scopes.
- `GITHUB_REPO`: The repository in the format `owner/repo`.
- `GITHUB_WORKFLOW_ID`: The ID or filename of the workflow you want to trigger.

## Example `.env` File

```properties
AZURE_STORAGE_BLOB_SERVICE_URI=https://functionapikapeltol.blob.core.windows.net
AZURE_STORAGE_CONTAINER_NAME=visma-config
AZURE_STORAGE_FILE_NAME=config1.json
AZURE_KEY_VAULT_URI=https://kv-visma-kapeltol.vault.azure.net/
GITHUB_TOKEN=your_github_token
GITHUB_REPO=your_username/your_repo
GITHUB_WORKFLOW_ID=your_workflow_file.yml
```
## Example CURL message
```properties
curl -X POST \
  -H "Content-Type: application/json" \
  -d '{
    "isEncrypted": false,
    "values": {
      "AzureWebJobsStorage": "your_storage_connection_string",
      "APP_KIND": "workflowapp",
      "WORKFLOWS_TENANT_ID": "actual_ms_tenant",
      "WORKFLOWS_SUBSCRIPTION_ID": "actual_subscription",
      "WORKFLOWS_RESOURCE_GROUP_NAME": "rg-visma-demo",
      "WORKFLOWS_LOCATION_NAME": "swedencentral",
      "WORKFLOWS_MANAGEMENT_BASE_URI": "https://management.azure.com/",
      "salesforce-connectionKey": "your_salesforce_connection_key",
      "salesforce-ConnectionRuntimeUrl": "your_salesforce_runtime_url",
      "hubspot-accesstoken": "your_hubspot_access_token"
    }
  }' \
  http://localhost:7071/api/ConfigurationHelper
  ```