using Google.Cloud.BigQuery.V2;
using Google.Cloud.SecretManager.V1;
using BigQueryRazorDemo.Models;

namespace BigQueryRazorDemo.Services;

public class BigQueryService : IBigQueryService
{
    private readonly BigQueryClient _client;
    private readonly string _datasetId;
    private readonly ILogger<BigQueryService> _logger;

    public BigQueryService(IConfiguration config, ILogger<BigQueryService> logger)
    {
        _logger = logger;
        var projectId = config["BigQuery:ProjectId"];
        _datasetId = config["BigQuery:DatasetId"] ?? "cdc_analytics";
        var secretName = config["BigQuery:CredentialsSecretName"];
        
        if (string.IsNullOrEmpty(secretName))
        {
            throw new InvalidOperationException("BigQuery:CredentialsSecretName is not configured.");
        }

        // 1. 从 Secret Manager 获取服务账号 JSON 凭据
        var secretJson = GetSecretFromManager(secretName);
        
        // 2. 使用 JSON 字符串创建 BigQueryClient
        var clientBuilder = new BigQueryClientBuilder
        {
            ProjectId = projectId,
            JsonCredentials = secretJson
        };
        _client = clientBuilder.Build();
        _logger.LogInformation("BigQueryClient initialized with project {ProjectId}", projectId);
    }

    private string GetSecretFromManager(string secretName)
    {
        // 解析 secret 名称格式：projects/{project_id}/secrets/{secret_name}/versions/latest
        // 这里简化：如果 secretName 不包含完整路径，则默认使用当前项目 ID 和环境变量 PROJECT_ID
        var projectId = Environment.GetEnvironmentVariable("PROJECT_ID") ?? 
                        throw new InvalidOperationException("PROJECT_ID environment variable not set.");
        
        var fullSecretName = secretName.Contains("/secrets/") 
            ? secretName 
            : $"projects/{projectId}/secrets/{secretName}/versions/latest";
        
        var client = SecretManagerServiceClient.Create();
        var response = client.AccessSecretVersion(fullSecretName);
        var secretPayload = response.Payload.Data.ToStringUtf8();
        return secretPayload;
    }

    public async Task<List<InteractionSummary>> GetDailySummariesAsync(DateTime from, DateTime to)
    {
        // 假设 BigQuery 表为 user_interactions，包含字段：source_table, operation_type, event_timestamp, processed_flag
        // 这里查询已处理（processed_flag = true）的数据，按日期聚合
        string sql = @"
            SELECT 
                source_table,
                operation_type,
                DATE(event_timestamp) as event_date,
                COUNT(*) as count
            FROM `" + _client.ProjectId + "." + _datasetId + @".user_interactions`
            WHERE event_timestamp >= @from
              AND event_timestamp < @to
              AND processed_flag = true
            GROUP BY source_table, operation_type, event_date
            ORDER BY event_date DESC, count DESC
        ";

        var parameters = new[]
        {
            new BigQueryParameter("from", BigQueryDbType.Timestamp, from),
            new BigQueryParameter("to", BigQueryDbType.Timestamp, to)
        };

        var results = await _client.ExecuteQueryAsync(sql, parameters);
        var list = new List<InteractionSummary>();
        
        foreach (var row in results)
        {
            list.Add(new InteractionSummary
            {
                SourceTable = (string)row["source_table"],
                OperationType = (string)row["operation_type"],
                EventDate = (DateTime)row["event_date"],
                Count = (long)row["count"]
            });
        }
        
        return list;
    }
}