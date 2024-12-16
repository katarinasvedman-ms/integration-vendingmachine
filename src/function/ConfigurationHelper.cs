using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using DotNetEnv;

namespace Company.Function
{
    public class ConfigurationHelper
    {
        private readonly ILogger<ConfigurationHelper> _logger;
        private readonly SecretClient _secretClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;
        private readonly string _fileName;

        public ConfigurationHelper(ILogger<ConfigurationHelper> logger)
        {
            _logger = logger;
            LoadEnvironmentVariables();

            var keyVaultUri = new Uri(GetEnvironmentVariable("AZURE_KEY_VAULT_URI"));
            _secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());

            var blobServiceUri = new Uri(GetEnvironmentVariable("AZURE_STORAGE_BLOB_SERVICE_URI"));
            _blobServiceClient = new BlobServiceClient(blobServiceUri, new DefaultAzureCredential());

            _containerName = GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME");
            _fileName = GetEnvironmentVariable("AZURE_STORAGE_FILE_NAME");
        }

        private void LoadEnvironmentVariables()
        {
            // Load environment variables from .env file for local development
            Env.Load("c:/Users/kapeltol/src/keyvault-function-new/.env"); // Explicitly specify the path to the .env file          
        }
        private string GetEnvironmentVariable(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogError($"The environment variable '{variableName}' is not set.");
                throw new InvalidOperationException($"The environment variable '{variableName}' is not set.");
            }
            _logger.LogInformation($"{variableName}: {value}");
            return value;
        }

        [Function("ConfigurationHelper")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return req.Method switch
            {
                "GET" => await Get(req),
                "POST" => await Post(req),
                _ => new BadRequestResult(),
            };
        }

        private async Task<IActionResult> Get(HttpRequest req)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(_fileName);

            var response = await blobClient.DownloadAsync();
            using (var streamReader = new StreamReader(response.Value.Content))
            {
                string jsonContent = await streamReader.ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<Configuration>(jsonContent);

                var jsonResult = new JsonResult(data)
                {
                    ContentType = "application/json",
                    StatusCode = 200,
                    SerializerSettings = new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }
                };

                return jsonResult;
            }
        }

        private async Task<IActionResult> Post(HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<Configuration>(requestBody);

            if (data != null && data.Values != null)
            {
                var updatedValues = new Dictionary<string, string>();

                foreach (var kvp in data.Values)
                {
                    if (IsValidKey(kvp.Key))
                    {
                        await _secretClient.SetSecretAsync(new KeyVaultSecret(kvp.Key, kvp.Value));
                        updatedValues[kvp.Key] = $"<KeyVault reference for {kvp.Key}>";
                    }
                    else
                    {
                        updatedValues[kvp.Key] = kvp.Value;
                    }
                }

                data.Values = updatedValues;

                var updatedJson = JsonConvert.SerializeObject(data, Formatting.Indented);
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(_fileName);

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(updatedJson)))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }
                // Trigger GitHub Actions workflow
                await TriggerGitHubActionsWorkflow();

                return new OkObjectResult("Secrets added to Key Vault and JSON file updated in Azure Storage");
            }

            return new BadRequestObjectResult("Invalid input");
        }

        public class Configuration
        {
            public bool IsEncrypted { get; set; }
            public Dictionary<string, string> Values { get; set; }
        }

        public bool IsValidKey(string key)
        {
            Regex regex = new Regex("^[a-zA-Z0-9-]+$");
            return regex.IsMatch(key);
        }
        private async Task TriggerGitHubActionsWorkflow()
        {
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPO");
            var githubWorkflowId = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW_ID");

            if (string.IsNullOrEmpty(githubToken) || string.IsNullOrEmpty(githubRepo) || string.IsNullOrEmpty(githubWorkflowId))
            {
                _logger.LogError("GitHub environment variables are not set.");
                throw new InvalidOperationException("GitHub environment variables are not set.");
            }

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YourAppName/1.0");

            var requestBody = new
            {
                @ref = "main" // or the branch you want to trigger the workflow on
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"https://api.github.com/repos/{githubRepo}/actions/workflows/{githubWorkflowId}/dispatches", content);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to trigger GitHub Actions workflow: {responseContent}");
                throw new InvalidOperationException($"Failed to trigger GitHub Actions workflow: {responseContent}");
            }

            _logger.LogInformation("GitHub Actions workflow triggered successfully.");
        }
    }
}