using System.Text.Json;
using System.Text.Json.Nodes;
using FinSightIQ.Contracts.Events;

namespace FinSightIQ.Contracts.Tests.Events;

public sealed class PredictionGeneratedTests
{
    private const string Schema = "prediction-generated.v1.json";
    private static readonly Guid Id = Guid.Parse("c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44");

    private static PredictionGenerated Minimal() => new()
    {
        PredictionId = Id,
        Symbol = "AAPL",
        Confidence = 0.87m,
        Recommendation = "BUY",
    };

    private static PredictionGenerated Full() => Minimal() with
    {
        PredictedPrice = 194.82m,
        LowerBound = 187.40m,
        UpperBound = 202.24m,
        Trend = "UP",
        ForecastDays = 7,
        TargetDate = new DateOnly(2026, 4, 28),
        ModelUsed = "ensemble",
        ModelVersion = "2026-04-18T02:00:00Z",
        Degraded = false,
        GeneratedAt = new DateTimeOffset(2026, 4, 19, 8, 0, 0, TimeSpan.Zero),
        TopFeatures =
        [
            new FeatureContribution { Name = "sentiment_score", Contribution = 0.41 },
            new FeatureContribution { Name = "close_lag_7", Contribution = -0.15 },
        ],
    };

    [Fact]
    public void Serializes_MinimalEvent_WithOnlyDocumentedFields()
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(Minimal(), ContractTestHelpers.DefaultOptions))!.AsObject();

        Assert.Equal(["confidence", "predictionId", "recommendation", "symbol"], json.Select(p => p.Key).Order().ToArray());
        Assert.Equal("c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44", json["predictionId"]!.GetValue<string>());
    }

    [Fact]
    public void Serializes_FullEvent_WithApiDocumentFieldNames()
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(Full(), ContractTestHelpers.DefaultOptions))!.AsObject();

        Assert.Equal(
            [
                "confidence", "degraded", "forecastDays", "generatedAt", "lowerBound", "modelUsed", "modelVersion",
                "predictedPrice", "predictionId", "recommendation", "symbol", "targetDate", "topFeatures", "trend", "upperBound",
            ],
            json.Select(p => p.Key).Order().ToArray());
        Assert.Equal("2026-04-28", json["targetDate"]!.GetValue<string>());
        Assert.Equal("sentiment_score", json["topFeatures"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void RoundTrips_FullEvent_PreservingDecimalsAndFeatures()
    {
        string json = JsonSerializer.Serialize(Full(), ContractTestHelpers.DefaultOptions);

        PredictionGenerated? copy = JsonSerializer.Deserialize<PredictionGenerated>(json, ContractTestHelpers.DefaultOptions);

        Assert.NotNull(copy);
        Assert.Equal(187.40m, copy.LowerBound);
        Assert.Equal(new DateOnly(2026, 4, 28), copy.TargetDate);
        Assert.Equal(TimeSpan.Zero, copy.GeneratedAt!.Value.Offset);
        Assert.Equal(-0.15, copy.TopFeatures![1].Contribution);
    }

    [Fact]
    public void Deserializes_MinimalPayloadAndIgnoresUnknownFields()
    {
        const string json = """
            {"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5,"recommendation":"HOLD","later":true}
            """;

        PredictionGenerated? evt = JsonSerializer.Deserialize<PredictionGenerated>(json, ContractTestHelpers.DefaultOptions);

        Assert.NotNull(evt);
        Assert.Equal(Id, evt.PredictionId);
        Assert.Null(evt.TopFeatures);
        Assert.Null(evt.LowerBound);
    }

    [Fact]
    public void Deserialization_RejectsMissingRecommendation() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PredictionGenerated>(
            """{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5}""",
            ContractTestHelpers.DefaultOptions));

    [Fact]
    public void SerializedPayloads_ConformToSchema()
    {
        Assert.True(ContractTestHelpers.IsValid(Schema, Minimal()));
        Assert.True(ContractTestHelpers.IsValid(Schema, Full()));
    }

    [Theory]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":1.2,"recommendation":"BUY"}""")]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":-0.1,"recommendation":"BUY"}""")]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5,"recommendation":"STRONG_BUY"}""")]
    [InlineData("""{"predictionId":"nope","symbol":"AAPL","confidence":0.5,"recommendation":"BUY"}""")]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5,"recommendation":"BUY","forecastDays":31}""")]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5,"recommendation":"BUY","trend":"SIDEWAYS"}""")]
    [InlineData("""{"predictionId":"c9f0f895-fb98-4b41-a3f9-3f2e7c8d1a44","symbol":"AAPL","confidence":0.5,"recommendation":"BUY","topFeatures":[{"name":"x"}]}""")]
    [InlineData("""{"symbol":"AAPL","confidence":0.5,"recommendation":"BUY"}""")]
    public void Schema_RejectsInvalidPayloads(string json) =>
        Assert.False(ContractTestHelpers.IsValid(Schema, json));
}
