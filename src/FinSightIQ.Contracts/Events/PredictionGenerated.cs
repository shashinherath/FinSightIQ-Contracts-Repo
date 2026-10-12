using System.Text.Json.Serialization;

namespace FinSightIQ.Contracts.Events;

/// <summary>
/// A new forecast was produced and persisted by the Prediction Service (FR-PRED-12).
/// </summary>
/// <remarks>
/// <para>Routing key: <c>prediction.generated</c>. Publisher: Prediction Service.
/// Consumers: Notification, analytics.</para>
/// <para>Schema: <c>schemas/prediction-generated.v1.json</c>. Delivery is at-least-once, so consumers must be idempotent.</para>
/// <para>The architecture document specifies <c>predictionId, symbol, confidence, recommendation</c>; those are the
/// required fields. Forecast output, interval, model metadata and explainability are optional additive fields named as in
/// the <c>POST /predictions</c> response. <see cref="Confidence"/> is interval-derived and is not the probability that
/// the price will be reached. <see cref="Recommendation"/> is a mechanical mapping, not financial advice.</para>
/// </remarks>
public sealed record PredictionGenerated
{
    /// <summary>Gets the identifier of the stored prediction (<c>predictions.id</c>).</summary>
    [JsonPropertyName("predictionId")]
    public required Guid PredictionId { get; init; }

    /// <summary>Gets the uppercase ticker symbol the forecast is for.</summary>
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    /// <summary>Gets the model's interval-derived confidence, between 0 and 1 inclusive.</summary>
    [JsonPropertyName("confidence")]
    public required decimal Confidence { get; init; }

    /// <summary>Gets the mechanical recommendation: <c>BUY</c>, <c>HOLD</c> or <c>SELL</c>.</summary>
    [JsonPropertyName("recommendation")]
    public required string Recommendation { get; init; }

    // ---- Forecast output and confidence interval (optional) ----

    /// <summary>Gets the point estimate of the forecast price.</summary>
    [JsonPropertyName("predictedPrice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PredictedPrice { get; init; }

    /// <summary>Gets the lower bound of the confidence interval.</summary>
    [JsonPropertyName("lowerBound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LowerBound { get; init; }

    /// <summary>Gets the upper bound of the confidence interval.</summary>
    [JsonPropertyName("upperBound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UpperBound { get; init; }

    /// <summary>Gets the forecast direction: <c>UP</c>, <c>DOWN</c> or <c>FLAT</c>.</summary>
    [JsonPropertyName("trend")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Trend { get; init; }

    /// <summary>Gets the forecast horizon in days (1 to 30).</summary>
    [JsonPropertyName("forecastDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ForecastDays { get; init; }

    /// <summary>Gets the calendar date being forecast.</summary>
    [JsonPropertyName("targetDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? TargetDate { get; init; }

    // ---- Model metadata (optional) ----

    /// <summary>Gets the model that produced the forecast: <c>prophet</c>, <c>arima</c>, <c>lstm</c> or <c>ensemble</c>.</summary>
    [JsonPropertyName("modelUsed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelUsed { get; init; }

    /// <summary>Gets the registry artefact version of the model.</summary>
    [JsonPropertyName("modelVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelVersion { get; init; }

    /// <summary>Gets a value indicating whether a non-fatal dependency was unavailable (for example sentiment defaulted to neutral).</summary>
    [JsonPropertyName("degraded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Degraded { get; init; }

    /// <summary>Gets the instant the forecast was generated, expressed in UTC.</summary>
    [JsonPropertyName("generatedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? GeneratedAt { get; init; }

    // ---- Explainability (optional) ----

    /// <summary>Gets the most influential features with signed SHAP contributions.</summary>
    [JsonPropertyName("topFeatures")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FeatureContribution>? TopFeatures { get; init; }
}

/// <summary>
/// A feature's signed SHAP contribution to a forecast. Positive pushed the forecast up, negative pushed it down.
/// Magnitude is comparable within one prediction, not across predictions.
/// </summary>
public sealed record FeatureContribution
{
    /// <summary>Gets the feature name, for example <c>rsi_14</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the signed SHAP value.</summary>
    [JsonPropertyName("contribution")]
    public required double Contribution { get; init; }
}
