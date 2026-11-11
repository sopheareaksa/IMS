using System.Text.Json.Serialization;

namespace IMS.DTOs;

public class BakongCheckResponse
{
    [JsonPropertyName("responseCode")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("responseMessage")]
    public string? ResponseMessage { get; set; }

    [JsonPropertyName("data")]
    public BakongTransactionData? Data { get; set; }
}

public class BakongTransactionData
{
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("fromAccountId")]
    public string? FromAccountId { get; set; }

    [JsonPropertyName("toAccountId")]
    public string? ToAccountId { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("createdDateMs")]
    public long? CreatedDateMs { get; set; }
}

