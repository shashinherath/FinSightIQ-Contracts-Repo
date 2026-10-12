using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FinSightIQ.Contracts.Events;
using Json.Schema;

namespace FinSightIQ.Contracts.Tests;

/// <summary>Checks that each C# event and its JSON Schema describe the same contract.</summary>
public sealed class SchemaAgreementTests
{
    public static TheoryData<Type, string> Contracts => new()
    {
        { typeof(PriceUpdated), "price-updated.v1.json" },
        { typeof(PredictionGenerated), "prediction-generated.v1.json" },
        { typeof(SentimentShifted), "sentiment-shifted.v1.json" },
    };

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_IsValidDraft202012(Type _, string schemaFile)
    {
        EvaluationResults result = MetaSchemas.Draft202012.Evaluate(
            ContractTestHelpers.LoadSchemaNode(schemaFile), new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid);
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", ContractTestHelpers.LoadSchemaNode(schemaFile)["$schema"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_HasVersionedIdentifierMatchingFileName(Type _, string schemaFile)
    {
        string id = ContractTestHelpers.LoadSchemaNode(schemaFile)["$id"]!.GetValue<string>();
        string stem = Path.GetFileNameWithoutExtension(schemaFile); // price-updated.v1
        int dot = stem.LastIndexOf('.');

        Assert.Equal($"urn:finsightiq:schema:{stem[..dot]}:{stem[(dot + 1)..]}", id);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_DeclaresExactlyTheSerializedProperties(Type eventType, string schemaFile)
    {
        JsonObject schema = ContractTestHelpers.LoadSchemaNode(schemaFile).AsObject();

        string[] schemaProps = schema["properties"]!.AsObject().Select(p => p.Key).Order().ToArray();
        string[] clrProps = SerializedProperties(eventType).Select(p => p.Name).Order().ToArray();

        Assert.Equal(clrProps, schemaProps);
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_RequiresExactlyTheRequiredMembers(Type eventType, string schemaFile)
    {
        string[] required = ContractTestHelpers.LoadSchemaNode(schemaFile)["required"]!.AsArray()
            .Select(n => n!.GetValue<string>()).Order().ToArray();
        string[] clrRequired = SerializedProperties(eventType).Where(p => p.Required).Select(p => p.Name).Order().ToArray();

        Assert.Equal(clrRequired, required);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_PropertyTypesMatchClrTypes(Type eventType, string schemaFile)
    {
        JsonObject props = ContractTestHelpers.LoadSchemaNode(schemaFile)["properties"]!.AsObject();

        foreach ((string name, Type clrType, _) in SerializedProperties(eventType))
        {
            string expected = ExpectedSchemaType(Nullable.GetUnderlyingType(clrType) ?? clrType);
            Assert.True(
                expected == props[name]!["type"]!.GetValue<string>(),
                $"{eventType.Name}.{name}: schema type '{props[name]!["type"]}' but CLR type {clrType.Name} maps to '{expected}'.");
        }
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Schema_FormatsMatchClrTypes(Type eventType, string schemaFile)
    {
        JsonObject props = ContractTestHelpers.LoadSchemaNode(schemaFile)["properties"]!.AsObject();

        foreach ((string name, Type clrType, _) in SerializedProperties(eventType))
        {
            Type t = Nullable.GetUnderlyingType(clrType) ?? clrType;
            string? expected = t == typeof(Guid) ? "uuid" : t == typeof(DateOnly) ? "date" : t == typeof(DateTimeOffset) ? "date-time" : null;
            string? actual = props[name]!["format"]?.GetValue<string>();

            Assert.True(expected == actual, $"{eventType.Name}.{name}: schema format '{actual}' but CLR type {t.Name} requires '{expected}'.");
        }
    }

    [Fact]
    public void EveryContract_IsCoveredByASchemaFile()
    {
        string[] events = typeof(PriceUpdated).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PriceUpdated).Namespace && t != typeof(FeatureContribution) && t.IsPublic)
            .Select(t => t.Name).Order().ToArray();

        Assert.Equal(Contracts.Select(c => ((Type)c[0]).Name).Order().ToArray(), events);
    }

    private static IEnumerable<(string Name, Type Type, bool Required)> SerializedProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition is null or not JsonIgnoreCondition.Always)
            .Select(p => (
                p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? throw new InvalidOperationException($"{type.Name}.{p.Name} lacks [JsonPropertyName]."),
                p.PropertyType,
                p.GetCustomAttribute<RequiredMemberAttribute>() is not null));

    private static string ExpectedSchemaType(Type t) =>
        t == typeof(string) || t == typeof(Guid) || t == typeof(DateOnly) || t == typeof(DateTimeOffset) ? "string"
        : t == typeof(decimal) || t == typeof(double) ? "number"
        : t == typeof(int) || t == typeof(long) ? "integer"
        : t == typeof(bool) ? "boolean"
        : t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ? "array"
        : throw new NotSupportedException($"No schema mapping for {t}.");
}
