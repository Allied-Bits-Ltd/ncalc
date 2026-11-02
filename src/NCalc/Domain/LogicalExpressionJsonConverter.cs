#if NET
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NCalc.Domain;

/// <summary>
/// Writes and reads a <see cref="LogicalExpression"/> with the kind of the node in a "$type" property, as the System.Text.Json polymorphism
/// did (<c>{"$type": "binary", ...}</c>). Unlike that polymorphism, it also keeps the options of a list and of a statement sequence
/// (see <see cref="ExpressionListJsonConverter"/>), which the serializer would otherwise write as a plain array.
/// </summary>
internal sealed class LogicalExpressionJsonConverter : JsonConverter<LogicalExpression>
{
    internal const string TypePropertyName = "$type";

    private static readonly Dictionary<Type, string> TypeNames = new()
    {
        [typeof(BinaryExpression)] = "binary",
        [typeof(FunctionCall)] = "function",
        [typeof(Identifier)] = "identifier",
        [typeof(LogicalExpressionList)] = "list",
        [typeof(TernaryExpression)] = "ternary",
        [typeof(UnaryExpression)] = "unary",
        [typeof(PercentExpression)] = "percent",
        [typeof(ExpressionGroup)] = "expressionGroup",
        [typeof(ValueExpression)] = "value",
        [typeof(ImaginaryNumberExpression)] = "imaginary",
        [typeof(VectorExpression)] = "vector",
        [typeof(StatementSequence)] = "statements",
        [typeof(FunctionExpression)] = "functionDeclaration",
        [typeof(IfStatementExpression)] = "if",
    };

    private static readonly Dictionary<string, Type> TypesByName = TypeNames.ToDictionary(static pair => pair.Value, static pair => pair.Key, StringComparer.Ordinal);

    public override LogicalExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"An expression node must be a JSON object, but {reader.TokenType} was found.");

        string? typeName = FindTypeName(reader);
        if (typeName is null)
            throw new JsonException($"The expression node has no \"{TypePropertyName}\" property.");
        if (!TypesByName.TryGetValue(typeName, out var type))
            throw new JsonException($"The expression node has an unknown type \"{typeName}\".");

        if (type == typeof(LogicalExpressionList) || type == typeof(StatementSequence))
            return ExpressionListJsonConverter.ReadList(ref reader, type, options);

        // The node is read by the default contract of its type, which ignores the "$type" property
        return (LogicalExpression?)JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(type));
    }

    public override void Write(Utf8JsonWriter writer, LogicalExpression value, JsonSerializerOptions options)
    {
        if (!TypeNames.TryGetValue(value.GetType(), out var typeName))
            throw new NotSupportedException($"The expression node type '{value.GetType()}' cannot be serialized.");

        if (value is LogicalExpressionList or StatementSequence)
        {
            ExpressionListJsonConverter.WriteList(writer, value, typeName, options);
            return;
        }

        // The node is written by the default contract of its type, after the "$type" property
        JsonElement element = JsonSerializer.SerializeToElement(value, options.GetTypeInfo(value.GetType()));
        writer.WriteStartObject();
        writer.WriteString(TypePropertyName, typeName);
        foreach (var property in element.EnumerateObject())
            property.WriteTo(writer);
        writer.WriteEndObject();
    }

    // Finds the "$type" property of the object at the reader (usually the first property), without moving the reader
    private static string? FindTypeName(Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isTypeProperty = reader.ValueTextEquals(TypePropertyName);
            if (!reader.Read())
                return null;
            if (isTypeProperty)
                return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            if (!reader.TrySkip())
                return null;
        }
        return null;
    }
}

/// <summary>
/// Writes a <see cref="LogicalExpressionList"/> or a <see cref="StatementSequence"/> as an object with its items, its options and,
/// for a sequence, whether it ends with a separator, so that a deserialized list is written by ToString() in the dialect it was
/// parsed with (e.g. "==" instead of "=" with C-style assignments) and a sequence keeps its trailing ';'.
/// A list without options is written as before, as a plain array (or as <c>{"$type": "list", "$values": [...]}</c> in place of a node),
/// and both forms are read.
/// </summary>
internal sealed class ExpressionListJsonConverter : JsonConverterFactory
{
    private const string ValuesPropertyName = "$values";
    private const string OptionsPropertyName = "Options";
    private const string AdvancedOptionsPropertyName = "AdvancedOptions";
    private const string EndsWithSeparatorPropertyName = "EndsWithSeparator";
    private const string ItemsPropertyName = "Items";

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(LogicalExpressionList) || typeToConvert == typeof(StatementSequence);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => typeToConvert == typeof(StatementSequence) ? new Converter<StatementSequence>() : new Converter<LogicalExpressionList>();

    private sealed class Converter<T> : JsonConverter<T>
        where T : LogicalExpression
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : (T)ReadList(ref reader, typeof(T), options);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => WriteList(writer, value, null, options);
    }

    private static IList<LogicalExpression> GetItems(LogicalExpression list)
        => list is StatementSequence sequence ? sequence : (LogicalExpressionList)list;

    private static JsonTypeInfo<LogicalExpression> GetItemTypeInfo(JsonSerializerOptions options)
        => (JsonTypeInfo<LogicalExpression>)options.GetTypeInfo(typeof(LogicalExpression));

    private static string ConvertName(string name, JsonSerializerOptions options)
        => options.PropertyNamingPolicy?.ConvertName(name) ?? name;

    internal static void WriteList(Utf8JsonWriter writer, LogicalExpression list, string? typeName, JsonSerializerOptions options)
    {
        var items = GetItems(list);
        bool endsWithSeparator = list is StatementSequence { EndsWithSeparator: true };
        var advancedOptions = list.SerializedAdvancedOptionsForJson;
        bool hasDetails = list.Options != ExpressionOptions.None || advancedOptions is not null || endsWithSeparator;

        if (!hasDetails && typeName is null)
        {
            WriteItems(writer, items, options);
            return;
        }

        writer.WriteStartObject();
        if (typeName is not null)
            writer.WriteString(LogicalExpressionJsonConverter.TypePropertyName, typeName);

        if (!hasDetails)
        {
            writer.WritePropertyName(ValuesPropertyName);
            WriteItems(writer, items, options);
            writer.WriteEndObject();
            return;
        }

        writer.WritePropertyName(ConvertName(OptionsPropertyName, options));
        JsonSerializer.Serialize(writer, list.Options, (JsonTypeInfo<ExpressionOptions>)options.GetTypeInfo(typeof(ExpressionOptions)));
        if (advancedOptions is not null)
        {
            writer.WritePropertyName(AdvancedOptionsPropertyName);
            JsonSerializer.Serialize(writer, advancedOptions, (JsonTypeInfo<LogicalExpression.SerializedAdvancedOptions>)options.GetTypeInfo(typeof(LogicalExpression.SerializedAdvancedOptions)));
        }
        if (endsWithSeparator)
            writer.WriteBoolean(ConvertName(EndsWithSeparatorPropertyName, options), true);
        writer.WritePropertyName(ConvertName(ItemsPropertyName, options));
        WriteItems(writer, items, options);
        writer.WriteEndObject();
    }

    private static void WriteItems(Utf8JsonWriter writer, IList<LogicalExpression> items, JsonSerializerOptions options)
    {
        var itemTypeInfo = GetItemTypeInfo(options);
        writer.WriteStartArray();
        foreach (var item in items)
            JsonSerializer.Serialize(writer, item, itemTypeInfo);
        writer.WriteEndArray();
    }

    internal static LogicalExpression ReadList(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        List<LogicalExpression> items = [];
        var expressionOptions = ExpressionOptions.None;
        LogicalExpression.SerializedAdvancedOptions? advancedOptions = null;
        bool endsWithSeparator = false;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            ReadItems(ref reader, items, options);
        }
        else
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString()!;
                reader.Read();
                if (string.Equals(name, ItemsPropertyName, StringComparison.OrdinalIgnoreCase) || name == ValuesPropertyName)
                    ReadItems(ref reader, items, options);
                else
                if (string.Equals(name, OptionsPropertyName, StringComparison.OrdinalIgnoreCase))
                    expressionOptions = JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<ExpressionOptions>)options.GetTypeInfo(typeof(ExpressionOptions)));
                else
                if (string.Equals(name, AdvancedOptionsPropertyName, StringComparison.OrdinalIgnoreCase))
                    advancedOptions = JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<LogicalExpression.SerializedAdvancedOptions?>)options.GetTypeInfo(typeof(LogicalExpression.SerializedAdvancedOptions)));
                else
                if (string.Equals(name, EndsWithSeparatorPropertyName, StringComparison.OrdinalIgnoreCase))
                    endsWithSeparator = reader.TokenType == JsonTokenType.True;
                else
                    reader.Skip();
            }
        }
        else
        {
            throw new JsonException($"A list of expressions must be a JSON array or object, but {reader.TokenType} was found.");
        }

        LogicalExpression result = type == typeof(StatementSequence)
            ? new StatementSequence(items, endsWithSeparator)
            : new LogicalExpressionList(items);
        result.SetOptions(expressionOptions, null, null);
        result.SerializedAdvancedOptionsForJson = advancedOptions;
        return result;
    }

    private static void ReadItems(ref Utf8JsonReader reader, List<LogicalExpression> items, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"The items of a list of expressions must be a JSON array, but {reader.TokenType} was found.");

        var itemTypeInfo = GetItemTypeInfo(options);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            items.Add(JsonSerializer.Deserialize(ref reader, itemTypeInfo)!);
    }
}
#endif
