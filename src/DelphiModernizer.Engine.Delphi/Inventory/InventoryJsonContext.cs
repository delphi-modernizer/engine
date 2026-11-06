using System.Text.Json.Serialization;

namespace DelphiModernizer.Engine.Delphi.Inventory;

/// <summary>The source-generated serializer for <see cref="LegacyInventory"/>: camelCase, indented, no reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(LegacyInventory))]
public sealed partial class InventoryJsonContext : JsonSerializerContext;
