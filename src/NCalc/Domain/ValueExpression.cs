using System.Buffers;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using ExtendedNumerics;

using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain;

internal enum StringKind
{
    Undefined,
    SingleQuote,
    DoubleQuote,
    BackQuote,
    RawDoubleQuote,
}

public sealed class ValueExpression : LogicalExpression
{
    internal StringKind StringKind { get; private set; }
    internal string? OriginalString { get; private set; }

    [JsonConverter(typeof(ObjectValueJsonConverter))]
    public object? Value { get; set; }
    public ValueType Type { get; set; }

    public ValueExpression()
    {
        Type = ValueType.NoValue;
    }

    public ValueExpression(object value)
    {
        Type = value switch
        {
            bool => ValueType.Boolean,
            DateTime => ValueType.DateTime,
            TimeSpan => ValueType.TimeSpan,
            Guid => ValueType.Guid,
            char => ValueType.Char,
            decimal or double or float or BigDecimal => ValueType.Float,
            byte or sbyte or short or int or long or ushort or uint or ulong or BigInteger => ValueType.Integer,
            string or Parlot.TextSpan => ValueType.String,
            _ => throw new NCalcException("This value could not be handled: " + value)
        };

        Value = value;
    }

    public ValueExpression(string? value)
    {
        Value = value;
        Type = ValueType.String;
    }

    internal ValueExpression(string? value, StringKind kind)
    {
        Value = value;
        Type = ValueType.String;
        StringKind = kind;
    }

    internal ValueExpression(string? value, string originalString, StringKind kind)
    {
        Value = value;
        Type = ValueType.String;
        OriginalString = originalString;
        StringKind = kind;
    }

    public ValueExpression(char value)
    {
        Value = value;
        Type = ValueType.Char;
    }

    public ValueExpression(int value)
    {
        Value = value;
        Type = ValueType.Integer;
    }

    public ValueExpression(long value)
    {
        Value = value;
        Type = ValueType.Integer;
    }

    public ValueExpression(double value)
    {
        Value = value;
        Type = ValueType.Float;
    }

    public ValueExpression(decimal value)
    {
        Value = value;
        Type = ValueType.Float;
    }

    public ValueExpression(DateTime value)
    {
        Value = value;
        Type = ValueType.DateTime;
    }

    public ValueExpression(TimeSpan value)
    {
        Value = value;
        Type = ValueType.TimeSpan;
    }

    public ValueExpression(bool value)
    {
        Value = value;
        Type = ValueType.Boolean;
    }

    public ValueExpression(Guid value)
    {
        Value = value;
        Type = ValueType.Guid;
    }

    public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, cancellationToken);
    }

    internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, task, cancellationToken);
    }

    public sealed class ObjectValueJsonConverter : JsonConverter<object?>
    {
        private const string TypePropertyName = "$type";
        private const string ValuePropertyName = "$value";

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return ReadValue(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            WriteValue(writer, value);
        }

        private static object? ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.True:
                    return true;

                case JsonTokenType.False:
                    return false;

                case JsonTokenType.String:
                    return reader.GetString();

                case JsonTokenType.Number:
                    return ReadUntypedNumber(ref reader);

                case JsonTokenType.StartArray:
                    return ReadArray(ref reader);

                case JsonTokenType.StartObject:
                    return ReadObjectOrTypedScalar(ref reader);

                default:
                    throw new JsonException($"Unsupported token type: {reader.TokenType}");
            }
        }

        private static object ReadUntypedNumber(ref Utf8JsonReader reader)
        {
            // Best-effort inference for untagged incoming JSON.
            // Exact round-trip of CLR numeric types is guaranteed only for tagged values.
            if (reader.TryGetInt32(out int i32))
                return i32;

            if (reader.TryGetInt64(out long i64))
                return i64;

            string raw = reader.GetRawString();

            if (BigInteger.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger bigInt))
                return bigInt;

            if (reader.TryGetDecimal(out decimal dec))
                return dec;

            if (reader.TryGetDouble(out double dbl))
                return dbl;

            throw new JsonException($"Cannot parse JSON number '{raw}'.");
        }

        private static List<object?> ReadArray(ref Utf8JsonReader reader)
        {
            var list = new List<object?>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;

                object? item = ReadValue(ref reader);
                list.Add(item);
            }

            throw new JsonException("Unexpected end of JSON while reading array.");
        }

        private static object ReadObjectOrTypedScalar(ref Utf8JsonReader reader)
        {
            var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (TryReadTaggedScalar(dictionary, out object? typedValue))
                        return typedValue!;

                    if (TryReadBigDecimal(dictionary, out object? bigDecimal))
                        return bigDecimal!;

                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name.");

                string propertyName = reader.GetString()!;
                if (!reader.Read())
                    throw new JsonException("Unexpected end of JSON after property name.");

                dictionary[propertyName] = ReadValue(ref reader);
            }

            throw new JsonException("Unexpected end of JSON while reading object.");
        }

        private static bool TryReadBigDecimal(Dictionary<string, object?> dictionary, out object? value)
        {
            value = null;

            // Expect exactly two properties
            if (dictionary.Count != 2)
                return false;

            if (!dictionary.TryGetValue(nameof(BigDecimal.Mantissa), out object? mantissaObj))
                return false;

            if (!dictionary.TryGetValue(nameof(BigDecimal.Exponent), out object? exponentObj))
                return false;

            if (!TryConvertToBigInteger(mantissaObj, out BigInteger mantissa))
                throw new JsonException("Invalid BigDecimal.Mantissa.");

            if (!TryConvertToInt32(exponentObj, out int exponent))
                throw new JsonException("Invalid BigDecimal.Exponent.");

            value = new BigDecimal(mantissa, exponent);
            return true;
        }

        private static bool TryConvertToBigInteger(object? input, out BigInteger result)
        {
            switch (input)
            {
                case BigInteger bi:
                    result = bi;
                    return true;

                case int i:
                    result = new BigInteger(i);
                    return true;

                case long l:
                    result = new BigInteger(l);
                    return true;

                case string s when BigInteger.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryConvertToInt32(object? input, out int result)
        {
            switch (input)
            {
                case int i:
                    result = i;
                    return true;

                case long l when l >= int.MinValue && l <= int.MaxValue:
                    result = (int)l;
                    return true;

                case BigInteger bi when bi >= int.MinValue && bi <= int.MaxValue:
                    result = (int)bi;
                    return true;

                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryReadTaggedScalar(Dictionary<string, object?> dictionary, out object? value)
        {
            value = null;

            if (dictionary.Count != 2)
                return false;

            if (!dictionary.TryGetValue(TypePropertyName, out object? typeNameObj) || typeNameObj is not string typeName)
                return false;

            if (!dictionary.TryGetValue(ValuePropertyName, out object? rawValue))
                return false;

            value = ParseTaggedScalar(typeName, rawValue);
            return true;
        }

        private static object? ParseTaggedScalar(string typeName, object? rawValue)
        {
            string? text = rawValue as string;

            switch (typeName)
            {
                case nameof(Boolean):
                    return bool.Parse(RequireString(typeName, text));

                case nameof(Byte):
                    return byte.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(SByte):
                    return sbyte.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(Int16):
                    return short.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(UInt16):
                    return ushort.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(Int32):
                    return int.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(UInt32):
                    return uint.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(Int64):
                    return long.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(UInt64):
                    return ulong.Parse(RequireString(typeName, text), NumberStyles.Integer, CultureInfo.InvariantCulture);

                case nameof(Single):
                    return float.Parse(RequireString(typeName, text), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

                case nameof(Double):
                    return double.Parse(RequireString(typeName, text), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

                case nameof(Decimal):
                    return decimal.Parse(RequireString(typeName, text), NumberStyles.Number, CultureInfo.InvariantCulture);

                case nameof(Char):
                {
                    string s = RequireString(typeName, text);
                    if (s.Length != 1)
                        throw new JsonException("Invalid Char payload.");
                    return s[0];
                }

                case nameof(String):
                    return text ?? string.Empty;

                case nameof(DateTime):
                    return DateTime.ParseExact(
                        RequireString(typeName, text),
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind);

                case nameof(TimeSpan):
                    return TimeSpan.ParseExact(
                        RequireString(typeName, text),
                        "c",
                        CultureInfo.InvariantCulture);

                case nameof(BigInteger):
                    return BigInteger.Parse(
                        RequireString(typeName, text),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture);

                case nameof(BigDecimal):
                    return BigInteger.Parse(
                        RequireString(typeName, text),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture);

                default:
                    throw new JsonException($"Unsupported tagged scalar type '{typeName}'.");
            }
        }

        private static string RequireString(string typeName, string? text)
        {
            if (text is null)
                throw new JsonException($"Tagged value for '{typeName}' must be a string.");
            return text;
        }

        private static void WriteValue(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    return;

                case bool b:
                    writer.WriteBooleanValue(b);
                    return;

                case string s:
                    writer.WriteStringValue(s);
                    return;

                case char ch:
                    WriteTaggedString(writer, nameof(Char), ch.ToString());
                    return;

                case byte v:
                    WriteTaggedString(writer, nameof(Byte), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case sbyte v:
                    WriteTaggedString(writer, nameof(SByte), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case short v:
                    WriteTaggedString(writer, nameof(Int16), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case ushort v:
                    WriteTaggedString(writer, nameof(UInt16), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case int v:
                    WriteTaggedString(writer, nameof(Int32), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case uint v:
                    WriteTaggedString(writer, nameof(UInt32), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case long v:
                    WriteTaggedString(writer, nameof(Int64), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case ulong v:
                    WriteTaggedString(writer, nameof(UInt64), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case float v:
                    WriteTaggedString(writer, nameof(Single), v.ToString("R", CultureInfo.InvariantCulture));
                    return;

                case double v:
                    WriteTaggedString(writer, nameof(Double), v.ToString("R", CultureInfo.InvariantCulture));
                    return;

                case decimal v:
                    WriteTaggedString(writer, nameof(Decimal), v.ToString(CultureInfo.InvariantCulture));
                    return;

                case DateTime dt:
                    // ISO 8601 round-trip text
                    WriteTaggedString(writer, nameof(DateTime), dt.ToString("O", CultureInfo.InvariantCulture));
                    return;

                case TimeSpan ts:
                    WriteTaggedString(writer, nameof(TimeSpan), ts.ToString("c", CultureInfo.InvariantCulture));
                    return;

                case BigInteger bi:
                    WriteTaggedString(writer, nameof(BigInteger), bi.ToString(CultureInfo.InvariantCulture));
                    return;

                case BigDecimal bd:
                    writer.WriteStartObject();
                    writer.WritePropertyName(nameof(bd.Mantissa));
                    WriteValue(writer, bd.Mantissa);
                    writer.WritePropertyName(nameof(bd.Exponent));
                    WriteValue(writer, bd.Exponent);
                    writer.WriteEndObject();
                    return;

                case JsonElement element:
                    element.WriteTo(writer);
                    return;

                case IDictionary<string, object?> dict:
                    writer.WriteStartObject();
                    foreach (KeyValuePair<string, object?> pair in dict)
                    {
                        writer.WritePropertyName(pair.Key);
                        WriteValue(writer, pair.Value);
                    }
                    writer.WriteEndObject();
                    return;

                case IEnumerable<object?> list:
                    writer.WriteStartArray();
                    foreach (object? item in list)
                    {
                        WriteValue(writer, item);
                    }
                    writer.WriteEndArray();
                    return;

                case IEnumerable enumerable when value is not string:
                    writer.WriteStartArray();
                    foreach (object? item in enumerable)
                    {
                        WriteValue(writer, item);
                    }
                    writer.WriteEndArray();
                    return;

                default:
                    throw new NotSupportedException(
                        $"Unsupported runtime type '{value.GetType().FullName}'. " +
                        "This converter supports primitive scalar types, string, DateTime, TimeSpan, BigInteger, " +
                        "JsonElement, dictionaries, and arrays/lists.");
            }
        }

        private static void WriteTaggedString(Utf8JsonWriter writer, string typeName, string valueText)
        {
            writer.WriteStartObject();
            writer.WriteString(TypePropertyName, typeName);
            writer.WriteString(ValuePropertyName, valueText);
            writer.WriteEndObject();
        }
    }
}

internal static class Utf8JsonReaderExtensions
{
    public static string GetRawString(this ref Utf8JsonReader reader)
    {
        if (reader.HasValueSequence)
        {
            ReadOnlySequence<byte> sequence = reader.ValueSequence;
            byte[] bytes = sequence.ToArray();
            return Encoding.UTF8.GetString(bytes);
        }

        ReadOnlySpan<byte> span = reader.ValueSpan;
        byte[] bytesFromSpan = span.ToArray();
        return Encoding.UTF8.GetString(bytesFromSpan);
    }
}
