using System.Text.Json;
using System.Text.Json.Nodes;
using FinSightIQ.Contracts.Events;

namespace FinSightIQ.Contracts.Tests.Events;

public sealed class PriceUpdatedTests
{
    private const string Schema = "price-updated.v1.json";

    private static PriceUpdated Sample() => new()
    {
        Symbol = "AAPL",
        Price = 194.8200m,
        Volume = 51_234_567_890L,
        Timestamp = new DateTimeOffset(2026, 4, 19, 13, 30, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Serializes_RequiredFields_AsCamelCase()
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(Sample(), ContractTestHelpers.DefaultOptions))!.AsObject();

        Assert.Equal(["price", "symbol", "timestamp", "volume"], json.Select(p => p.Key).Order().ToArray());
    }

    [Fact]
    public void Serializes_OptionalFields_OnlyWhenSet()
    {
        PriceUpdated full = Sample() with { Open = 190.1m, High = 195m, Low = 189.9m };
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(full, ContractTestHelpers.DefaultOptions))!.AsObject();

        Assert.Equal(["high", "low", "open", "price", "symbol", "timestamp", "volume"], json.Select(p => p.Key).Order().ToArray());
    }

    [Fact]
    public void Serializes_Prices_AsJsonNumbers_AndVolume_AsIntegerBeyondInt32()
    {
        string text = JsonSerializer.Serialize(Sample(), ContractTestHelpers.DefaultOptions);

        Assert.Contains("\"price\":194.8200", text);
        Assert.Contains("\"volume\":51234567890", text);
    }

    [Fact]
    public void Publisher_ConvertingToUtc_ProducesSchemaValidTimestampOfTheSameInstant()
    {
        var local = new DateTimeOffset(2026, 4, 19, 19, 0, 0, TimeSpan.FromHours(5.5));
        PriceUpdated evt = Sample() with { Timestamp = local.ToUniversalTime() };

        string timestamp = JsonNode.Parse(JsonSerializer.Serialize(evt, ContractTestHelpers.DefaultOptions))!["timestamp"]!.GetValue<string>();

        Assert.Matches("(Z|\\+00:00)$", timestamp);
        Assert.Equal(local, DateTimeOffset.Parse(timestamp));
        Assert.True(ContractTestHelpers.IsValid(Schema, evt));
    }

    [Fact]
    public void NonUtcTimestamp_IsNotNormalised_AndFailsSchema()
    {
        // The type does not coerce to UTC; publishers must. The schema is the guard.
        PriceUpdated evt = Sample() with { Timestamp = new DateTimeOffset(2026, 4, 19, 19, 0, 0, TimeSpan.FromHours(5.5)) };

        Assert.False(ContractTestHelpers.IsValid(Schema, evt));
    }

    [Fact]
    public void RoundTrips_WithoutLosingDecimalPrecision()
    {
        PriceUpdated original = Sample() with { Open = 0.1m + 0.2m, High = 195.0001m, Low = 189.9999m };

        PriceUpdated? copy = JsonSerializer.Deserialize<PriceUpdated>(
            JsonSerializer.Serialize(original, ContractTestHelpers.DefaultOptions), ContractTestHelpers.DefaultOptions);

        Assert.Equal(original, copy);
    }

    [Fact]
    public void Deserializes_PayloadWithUnknownFields_ForForwardCompatibility()
    {
        const string json = """
            {"symbol":"AAPL","price":10.5,"volume":3,"timestamp":"2026-04-19T13:30:00Z","futureField":{"x":1}}
            """;

        PriceUpdated? evt = JsonSerializer.Deserialize<PriceUpdated>(json, ContractTestHelpers.DefaultOptions);

        Assert.NotNull(evt);
        Assert.Equal(10.5m, evt.Price);
        Assert.Equal(TimeSpan.Zero, evt.Timestamp.Offset);
        Assert.Null(evt.Open);
    }

    [Theory]
    [InlineData("""{"price":10.5,"volume":3,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","volume":3,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":10.5,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":10.5,"volume":3}""")]
    public void Deserialization_RejectsMissingRequiredFields(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PriceUpdated>(json, ContractTestHelpers.DefaultOptions));

    [Fact]
    public void SerializedPayload_ConformsToSchema()
    {
        Assert.True(ContractTestHelpers.IsValid(Schema, Sample()));
        Assert.True(ContractTestHelpers.IsValid(Schema, Sample() with { Open = 1m, High = 2m, Low = 0.5m }));
    }

    [Theory]
    [InlineData("""{"symbol":"AAPL","price":0,"volume":3,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":-1,"volume":3,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":-1,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":1.5,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"","price":1,"volume":1,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"TOOLONGSYMBOL","price":1,"volume":1,"timestamp":"2026-04-19T13:30:00Z"}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":1,"timestamp":"not-a-date"}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":1,"timestamp":"2026-04-19T13:30:00+05:30"}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":1,"timestamp":"2026-04-19T13:30:00Z","unexpected":1}""")]
    [InlineData("""{"symbol":"AAPL","price":1,"volume":1}""")]
    public void Schema_RejectsInvalidPayloads(string json) =>
        Assert.False(ContractTestHelpers.IsValid(Schema, json));
}
