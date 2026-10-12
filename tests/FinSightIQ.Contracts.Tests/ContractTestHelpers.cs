using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace FinSightIQ.Contracts.Tests;

/// <summary>Shared helpers: default serializer options (as a consumer would use) and schema loading/validation.</summary>
internal static class ContractTestHelpers
{
    // Deliberately default options: camelCase must come from the contract, not the caller's settings.
    public static readonly JsonSerializerOptions DefaultOptions = new();

    public static string SchemaPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "schemas", fileName);

    public static JsonNode LoadSchemaNode(string fileName) =>
        JsonNode.Parse(File.ReadAllText(SchemaPath(fileName)))!;

    public static JsonSchema LoadSchema(string fileName) =>
        JsonSchema.FromText(File.ReadAllText(SchemaPath(fileName)));

    public static EvaluationOptions StrictOptions() => new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    public static bool IsValid(string schemaFile, string json) =>
        LoadSchema(schemaFile).Evaluate(JsonNode.Parse(json), StrictOptions()).IsValid;

    public static bool IsValid(string schemaFile, object payload) =>
        IsValid(schemaFile, JsonSerializer.Serialize(payload, payload.GetType(), DefaultOptions));
}
