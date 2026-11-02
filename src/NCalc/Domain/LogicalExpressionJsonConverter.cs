#if NET
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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

    // The nodes of a tree are read and written by nested converter and serializer calls, several per level, and the serializer
    // catches and rethrows an exception in each of its calls; an exception thrown deep in a tree then overflows the stack while it
    // propagates (NCALC-301). So a failure below the outermost node is recorded here, the rest of the tree is skipped, and the
    // outermost call throws it
    [ThreadStatic]
    private static int t_nesting;

    [ThreadStatic]
    private static ExceptionDispatchInfo? t_error;

    public override LogicalExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (t_nesting > 0 && t_error is not null)
        {
            reader.Skip();
            return null;
        }

        LogicalExpression? result = null;
        t_nesting++;
        try
        {
            if (t_nesting == 1)
                t_error = null;
            result = ReadNode(ref reader, options);
        }
        catch (Exception e) when (t_nesting > 1 || t_error is not null)
        {
            t_error ??= ExceptionDispatchInfo.Capture(e);
        }
        finally
        {
            t_nesting--;
        }

        ThrowRecordedError();
        return result;
    }

    private static LogicalExpression? ReadNode(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"An expression node must be a JSON object, but {reader.TokenType} was found.");
        EnsureDepth(reader.CurrentDepth, options);

        string? typeName = FindTypeName(reader);
        if (typeName is null)
            throw new JsonException($"The expression node has no \"{TypePropertyName}\" property.");
        if (!TypesByName.TryGetValue(typeName, out var type))
            throw new JsonException($"The expression node has an unknown type \"{typeName}\".");

        if (type == typeof(LogicalExpressionList) || type == typeof(StatementSequence))
            return ExpressionListJsonConverter.ReadList(ref reader, type, options);

        // The node is read by the default contract of its type, which ignores the "$type" property
        var node = NodeReaders[type](ref reader, options);
        if (node is not null)
            Validate(node);
        return node;
    }

    private delegate LogicalExpression? NodeReader(ref Utf8JsonReader reader, JsonSerializerOptions options);

    private static readonly Dictionary<Type, NodeReader> NodeReaders = new()
    {
        [typeof(BinaryExpression)] = ReadByContract<BinaryExpression>,
        [typeof(FunctionCall)] = ReadByContract<FunctionCall>,
        [typeof(Identifier)] = ReadByContract<Identifier>,
        [typeof(TernaryExpression)] = ReadByContract<TernaryExpression>,
        [typeof(UnaryExpression)] = ReadByContract<UnaryExpression>,
        [typeof(PercentExpression)] = ReadByContract<PercentExpression>,
        [typeof(ExpressionGroup)] = ReadByContract<ExpressionGroup>,
        [typeof(ValueExpression)] = ReadByContract<ValueExpression>,
        [typeof(ImaginaryNumberExpression)] = ReadByContract<ImaginaryNumberExpression>,
        [typeof(VectorExpression)] = ReadByContract<VectorExpression>,
        [typeof(FunctionExpression)] = ReadByContract<FunctionExpression>,
        [typeof(IfStatementExpression)] = ReadByContract<IfStatementExpression>,
    };

    // Reads a node with the converter of its contract, from the reader of the parent. JsonSerializer.Deserialize(ref reader, ...)
    // would first skip over the whole node to find its end, at every level, which takes quadratic time for a deep tree
    private static LogicalExpression? ReadByContract<T>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        where T : LogicalExpression
        => ((JsonConverter<T>)options.GetTypeInfo(typeof(T)).Converter).Read(ref reader, typeof(T), options);

    // Reads an item of a list, which is an expression node, with the converter of LogicalExpression (normally this one)
    internal static LogicalExpression? ReadItem(ref Utf8JsonReader reader, JsonSerializerOptions options)
        => ((JsonConverter<LogicalExpression>)options.GetTypeInfo(typeof(LogicalExpression)).Converter).Read(ref reader, typeof(LogicalExpression), options);

    // A node read from JSON must have the members that the parser always sets, and known kinds, as ToString() and the evaluators
    // rely on them (instead of failing later with a NullReferenceException or writing an unknown operator)
    private static void Validate(LogicalExpression node)
    {
        switch (node)
        {
            case BinaryExpression binary:
                Require(binary.LeftExpression, "LeftExpression");
                Require(binary.RightExpression, "RightExpression");
                RequireDefined(binary.Type);
                break;
            case TernaryExpression ternary:
                Require(ternary.LeftExpression, "LeftExpression");
                Require(ternary.MiddleExpression, "MiddleExpression");
                Require(ternary.RightExpression, "RightExpression");
                break;
            case UnaryExpression unary:
                Require(unary.Expression, "Expression");
                RequireDefined(unary.Type);
                break;
            case PercentExpression percent:
                Require(percent.Expression, "Expression");
                break;
            case ExpressionGroup group:
                Require(group.Expression, "Expression");
                break;
            case ImaginaryNumberExpression imaginary:
                Require(imaginary.Expression, "Expression");
                break;
            case Identifier identifier:
                Require(identifier.Name, "Name");
                break;
            case FunctionCall call:
                Require(call.Identifier, "Identifier");
                Require(call.Identifier.Name, "Identifier.Name");
                Require(call.Parameters, "Parameters");
                break;
            case VectorExpression vector:
                Require(vector.Expressions, "Expressions");
                break;
            case FunctionExpression declaration:
                Require(declaration.Function, "Function");
                Require(declaration.Function.Name, "Function.Name");
                Require(declaration.Function.Body, "Function.Body");
                foreach (var parameter in declaration.Function.Parameters)
                {
                    Require(parameter, "Function.Parameters");
                    Require(parameter.Name, "Function.Parameters.Name");
                }
                break;
        }

        static void Require(object? member, string name)
        {
            if (member is null)
                throw new JsonException($"The expression node has no \"{name}\" member.");
        }

        static void RequireDefined<T>(T value)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
                throw new JsonException($"The expression node has an unknown {typeof(T).Name} {value}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, LogicalExpression value, JsonSerializerOptions options)
    {
        if (t_nesting > 0 && t_error is not null)
        {
            writer.WriteNullValue();
            return;
        }

        t_nesting++;
        try
        {
            if (t_nesting == 1)
                t_error = null;
            WriteNode(writer, value, options);
        }
        catch (Exception e) when (t_nesting > 1 || t_error is not null)
        {
            t_error ??= ExceptionDispatchInfo.Capture(e);
        }
        finally
        {
            t_nesting--;
        }

        ThrowRecordedError();
    }

    private static void WriteNode(Utf8JsonWriter writer, LogicalExpression value, JsonSerializerOptions options)
    {
        string typeName = GetTypeName(value.GetType());

        // Each node is written by a nested serializer call into the same writer, and each nested call has its own state, which does
        // not count the depth; so the MaxDepth of the options is enforced here
        EnsureDepth(writer.CurrentDepth, options);

        if (value is LogicalExpressionList or StatementSequence)
        {
            ExpressionListJsonConverter.WriteList(writer, value, typeName, options);
            return;
        }

        // The node is written by the default contract of its type, which writes the "$type" property first
        // (LogicalExpression.JsonTypeName), directly into the writer of the parent
        JsonSerializer.Serialize(writer, value, options.GetTypeInfo(value.GetType()));
    }

    private static void ThrowRecordedError()
    {
        if (t_nesting == 0 && t_error is { } error)
        {
            t_error = null;
            error.Throw();
        }
    }

    internal static string GetTypeName(Type type)
        => TypeNames.TryGetValue(type, out var typeName) ? typeName : throw new NotSupportedException($"The expression node type '{type}' cannot be serialized.");

    private static void EnsureDepth(int currentDepth, JsonSerializerOptions options)
    {
        int maxDepth = options.MaxDepth == 0 ? 64 : options.MaxDepth;
        if (currentDepth >= maxDepth)
            throw new JsonException($"The expression is nested deeper than the maximum allowed depth of {maxDepth}. Increase JsonSerializerOptions.MaxDepth to serialize it.");
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw new JsonException("The expression is nested too deeply to be serialized or deserialized on the stack of this thread.");
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

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            items.Add(LogicalExpressionJsonConverter.ReadItem(ref reader, options) ?? throw new JsonException("An item of a list of expressions is null."));
    }
}
#endif
