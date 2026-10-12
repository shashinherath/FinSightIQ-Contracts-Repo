using System.Text.Json;
using System.Text.Json.Nodes;
using FinSightIQ.Contracts.Events;

namespace FinSightIQ.Contracts.Tests.Events;

public sealed class SentimentShiftedTests
{
    private const string Schema = "sentiment-shifted.v1.json";

    private static SentimentShifted Sample() => new()
    {
        Symbol = "AAPL",
        Score = 0.3412m,
        PreviousScore = 0.2800m,
        Date = new DateOnly(2026, 4, 19),
    };

    [Fact]
    public void Serializes_WithDocumentedCamelCaseFields()
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(Sample(), ContractTestHelpers.DefaultOptions))!.AsObject();

        Assert.Equal(["date", "previousScore", "score", "symbol"], json.Select(p => p.Key).Order().ToArray());
        Assert.Equal("2026-04-19", json["date"]!.GetValue<string>());
    }

    [Fact]
    public void RoundTrips_PreservingScoresExactly()
    {
        SentimentShifted? copy = JsonSerializer.Deserialize<SentimentShifted>(
            JsonSerializer.Serialize(Sample(), ContractTestHelpers.DefaultOptions), ContractTestHelpers.DefaultOptions);

        Assert.Equal(Sample(), copy);
    }

    [Fact]
    public void Deserializes_PayloadWithUnknownFields() =>
        Assert.NotNull(JsonSerializer.Deserialize<SentimentShifted>(
            """{"symbol":"AAPL","score":-0.4,"previousScore":-0.1,"date":"2026-04-19","confidence":0.9}""",
            ContractTestHelpers.DefaultOptions));

    [Fact]
    public void Deserialization_RejectsMissingPreviousScore() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SentimentShifted>(
            """{"symbol":"AAPL","score":-0.4,"date":"2026-04-19"}""", ContractTestHelpers.DefaultOptions));

    [Fact]
    public void SerializedPayload_ConformsToSchema() =>
        Assert.True(ContractTestHelpers.IsValid(Schema, Sample()));

    [Theory]
    [InlineData("""{"symbol":"AAPL","score":1.1,"previousScore":0,"date":"2026-04-19"}""")]
    [InlineData("""{"symbol":"AAPL","score":0.5,"previousScore":-1.1,"date":"2026-04-19"}""")]
    [InlineData("""{"symbol":"AAPL","score":0.5,"previousScore":0,"date":"19/04/2026"}""")]
    [InlineData("""{"symbol":"AAPL","score":0.5,"previousScore":0,"date":"2026-04-19T00:00:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","score":0.5,"date":"2026-04-19"}""")]
    [InlineData("""{"symbol":"AAPL","score":"0.5","previousScore":0,"date":"2026-04-19"}""")]
    public void Schema_RejectsInvalidPayloads(string json) =>
        Assert.False(ContractTestHelpers.IsValid(Schema, json));
}
