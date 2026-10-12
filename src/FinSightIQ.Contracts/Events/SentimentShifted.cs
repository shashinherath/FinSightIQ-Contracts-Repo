using System.Text.Json.Serialization;

namespace FinSightIQ.Contracts.Events;

/// <summary>
/// A symbol's daily sentiment score crossed the +0.3 / -0.3 threshold (FR-SENT-06).
/// </summary>
/// <remarks>
/// <para>Routing key: <c>sentiment.score.shifted</c>. Publisher: Sentiment Service.
/// Consumers: Alerting, retraining trigger.</para>
/// <para>Schema: <c>schemas/sentiment-shifted.v1.json</c>. Delivery is at-least-once, so consumers must be idempotent.</para>
/// <para>Fields are exactly those in the architecture document. The threshold rule belongs to the publisher and is
/// deliberately not encoded here.</para>
/// </remarks>
public sealed record SentimentShifted
{
    /// <summary>Gets the uppercase ticker symbol.</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>Gets the new daily score, between -1 and 1 inclusive.</summary>
    [JsonPropertyName("score")]
    public required decimal Score { get; init; }

    /// <summary>Gets the previous daily score, between -1 and 1 inclusive.</summary>
    [JsonPropertyName("previousScore")]
    public required decimal PreviousScore { get; init; }

    /// <summary>Gets the calendar date the new score applies to (<c>sentiment_scores.score_date</c>).</summary>
    [JsonPropertyName("date")]
    public required DateOnly Date { get; init; }
}
