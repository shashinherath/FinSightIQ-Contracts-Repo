using System.Text.Json.Serialization;

namespace FinSightIQ.Contracts.Events;

/// <summary>
/// A price bar was ingested and persisted by the Market Data Service (FR-MKT-10).
/// </summary>
/// <remarks>
/// <para>Routing key: <c>market.price.updated</c>. Publisher: Market Data Service.
/// Consumers: Alerting, cache invalidation (Prediction Service), portfolio valuation.</para>
/// <para>Schema: <c>schemas/price-updated.v1.json</c>. Delivery is at-least-once, so consumers must be idempotent.</para>
/// <para>The architecture document specifies <c>symbol, price, volume, timestamp</c>; those are the required fields.
/// <see cref="Open"/>, <see cref="High"/> and <see cref="Low"/> are optional additive fields taken from the
/// <c>stock_prices</c> table. <see cref="Price"/> is the close of the bar.</para>
/// </remarks>
public sealed record PriceUpdated
{
    /// <summary>Gets the uppercase ticker symbol (at most 10 characters, matching <c>symbols.symbol</c>).</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>Gets the price of the bar, i.e. its close. Always positive.</summary>
    [JsonPropertyName("price")]
    public required decimal Price { get; init; }

    /// <summary>Gets the traded volume of the bar. Never negative.</summary>
    [JsonPropertyName("volume")]
    public required long Volume { get; init; }

    /// <summary>Gets the instant the bar was recorded (<c>stock_prices.recorded_at</c>), expressed in UTC.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the opening price of the bar, when supplied.</summary>
    [JsonPropertyName("open")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Open { get; init; }

    /// <summary>Gets the highest price of the bar, when supplied.</summary>
    [JsonPropertyName("high")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? High { get; init; }

    /// <summary>Gets the lowest price of the bar, when supplied.</summary>
    [JsonPropertyName("low")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Low { get; init; }
}
