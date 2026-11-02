using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using ExtendedNumerics;

using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Domain;

/// <summary>
/// Custom JSON converter for nullable <see langword="object"/> values stored in <see cref="ValueExpression.Value"/>.
/// Supports primitive scalars, tagged type-preserving scalars, BigDecimal, BigInteger,
/// ComplexNumber, Vector, JsonElement, dictionaries, and arrays.
/// Dictionaries are written as <c>{"$type": "Dictionary", "$value": {...}}</c> and arrays of a scalar element type as
/// <c>{"$type": "Int32[]", "$value": [...]}</c>, so that both are read back with their types; plain JSON objects and arrays are still read
/// (as a dictionary, unless they have the form of a BigDecimal or a tagged value, and as a list).
/// </summary>
public sealed class ObjectValueJsonConverter : JsonConverter<object?>
{
    private const string TypePropertyName = "$type";
    private const string ValuePropertyName = "$value";
    private const string DictionaryTypeName = "Dictionary";
    private const string ArrayTypeSuffix = "[]";

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // A malformed payload (e.g. {"$type": "Int32", "$value": "abc"}) is reported as invalid JSON, not with the exception of the parsing method
        try
        {
            return ReadValue(ref reader);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or InvalidOperationException or InvalidCastException)
        {
            throw new JsonException($"The value is not valid: {e.Message}", e);
        }
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
                return ReadObjectOrTypedScalar(ref reader, plainObject: false);

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

    // A plain object is the content of a tagged dictionary: it is not interpreted as a BigDecimal, a tagged value etc.
    private static object ReadObjectOrTypedScalar(ref Utf8JsonReader reader, bool plainObject)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (plainObject)
                    return dictionary;

                if (TryReadTaggedScalar(dictionary, out object? typedValue))
                    return typedValue!;

                if (TryReadBigDecimal(dictionary, out object? bigDecimal))
                    return bigDecimal!;

                if (TryReadComplexNumber(dictionary, out ComplexNumber complexNumber))
                    return complexNumber;

                if (TryReadVector(dictionary, out NCalcVector vector))
                    return vector;

                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            string propertyName = reader.GetString()!;
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON after property name.");

            // The value of a tagged dictionary is read as it is
            if (!plainObject && propertyName == ValuePropertyName && reader.TokenType == JsonTokenType.StartObject
                && dictionary.TryGetValue(TypePropertyName, out object? typeName) && typeName is DictionaryTypeName)
                dictionary[propertyName] = ReadObjectOrTypedScalar(ref reader, plainObject: true);
            else
                dictionary[propertyName] = ReadValue(ref reader);
        }

        throw new JsonException("Unexpected end of JSON while reading object.");
    }

    private static bool TryReadBigDecimal(Dictionary<string, object?> dictionary, out object? value)
    {
        value = null;

        // Expect exactly two properties: Mantissa and Exponent
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

    private static bool TryReadComplexNumber(Dictionary<string, object?> dictionary, out ComplexNumber result)
    {
        result = default;

        // Expect exactly three properties: $type, Real, Imaginary
        if (dictionary.Count != 3)
            return false;

        if (!dictionary.TryGetValue(TypePropertyName, out object? typeNameObj) ||
            typeNameObj is not string typeName || typeName != "ComplexNumber")
            return false;

        if (!dictionary.TryGetValue("Real", out object? realObj) || realObj is not BigDecimal realBD)
            return false;

        if (!dictionary.TryGetValue("Imaginary", out object? imagObj) || imagObj is not BigDecimal imagBD)
            return false;

        result = new ComplexNumber(realBD, imagBD);
        return true;
    }

    private static bool TryReadVector(Dictionary<string, object?> dictionary, out NCalcVector result)
    {
        result = default;

        // Expect exactly two properties: $type and Components
        if (dictionary.Count != 2)
            return false;

        if (!dictionary.TryGetValue(TypePropertyName, out object? typeNameObj) ||
            typeNameObj is not string typeName || typeName != "Vector")
            return false;

        if (!dictionary.TryGetValue("Components", out object? compsObj) ||
            compsObj is not List<object?> compList)
            return false;

        var components = new BigDecimal[compList.Count];
        for (int i = 0; i < compList.Count; i++)
        {
            if (compList[i] is not BigDecimal bd)
                return false;
            components[i] = bd;
        }

        result = new NCalcVector(components);
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
                return BigDecimal.Parse(RequireString(typeName, text), CultureInfo.InvariantCulture);

            case nameof(Guid):
                return Guid.Parse(RequireString(typeName, text));

            case nameof(DateTimeOffset):
                return DateTimeOffset.ParseExact(RequireString(typeName, text), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            case DictionaryTypeName:
                return rawValue as Dictionary<string, object?> ?? throw new JsonException("Tagged value for 'Dictionary' must be an object.");

            case string arrayTypeName when arrayTypeName.EndsWith(ArrayTypeSuffix, StringComparison.Ordinal):
                if (rawValue is not List<object?> items)
                    throw new JsonException($"Tagged value for '{arrayTypeName}' must be an array.");
                return CreateArray(arrayTypeName.Substring(0, arrayTypeName.Length - ArrayTypeSuffix.Length), items);

            default:
                throw new JsonException($"Unsupported tagged scalar type '{typeName}'.");
        }
    }

    // The element types whose arrays keep their type in JSON (the names are those of the tagged values)
    private static string? GetArrayElementTypeName(Type? elementType)
    {
        if (elementType is null)
            return null;
        if (elementType == typeof(object))
            return nameof(Object);
        if (elementType == typeof(string))
            return nameof(String);
        if (elementType == typeof(BigInteger))
            return nameof(BigInteger);
        if (elementType == typeof(BigDecimal))
            return nameof(BigDecimal);
        if (elementType == typeof(Guid))
            return nameof(Guid);
        if (elementType == typeof(TimeSpan))
            return nameof(TimeSpan);
        if (elementType == typeof(DateTimeOffset))
            return nameof(DateTimeOffset);

        return Type.GetTypeCode(elementType) switch
        {
            TypeCode.Boolean or TypeCode.Char or TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32
                or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal
                or TypeCode.DateTime when !elementType.IsEnum => elementType.Name,
            _ => null
        };
    }

    private static Array CreateArray(string elementTypeName, List<object?> items) => elementTypeName switch
    {
        nameof(Object) => items.ToArray(),
        nameof(String) => ToArray<string?>(items),
        nameof(Boolean) => ToArray<bool>(items),
        nameof(Char) => ToArray<char>(items),
        nameof(SByte) => ToArray<sbyte>(items),
        nameof(Byte) => ToArray<byte>(items),
        nameof(Int16) => ToArray<short>(items),
        nameof(UInt16) => ToArray<ushort>(items),
        nameof(Int32) => ToArray<int>(items),
        nameof(UInt32) => ToArray<uint>(items),
        nameof(Int64) => ToArray<long>(items),
        nameof(UInt64) => ToArray<ulong>(items),
        nameof(Single) => ToArray<float>(items),
        nameof(Double) => ToArray<double>(items),
        nameof(Decimal) => ToArray<decimal>(items),
        nameof(DateTime) => ToArray<DateTime>(items),
        nameof(DateTimeOffset) => ToArray<DateTimeOffset>(items),
        nameof(TimeSpan) => ToArray<TimeSpan>(items),
        nameof(BigInteger) => ToArray<BigInteger>(items),
        nameof(BigDecimal) => ToArray<BigDecimal>(items),
        nameof(Guid) => ToArray<Guid>(items),
        _ => throw new JsonException($"Unsupported array element type '{elementTypeName}'.")
    };

    private static T[] ToArray<T>(List<object?> items)
    {
        var result = new T[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is T item)
                result[i] = item;
            else
            if (items[i] is not null || default(T) is not null)
                throw new JsonException($"Invalid element of an array of {typeof(T).Name}.");
        }
        return result;
    }

    private static string RequireString(string typeName, string? text)
    {
        if (text is null)
            throw new JsonException($"Tagged value for '{typeName}' must be a string.");
        return text;
    }

    /// <summary>
    /// Writes a <see cref="BigDecimal"/> as a JSON object: <c>{"Mantissa": ..., "Exponent": ...}</c>.
    /// </summary>
    private static void WriteBigDecimal(Utf8JsonWriter writer, BigDecimal bd)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(BigDecimal.Mantissa));
        WriteValue(writer, bd.Mantissa);
        writer.WritePropertyName(nameof(BigDecimal.Exponent));
        WriteValue(writer, bd.Exponent);
        writer.WriteEndObject();
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

            case Guid guid:
                WriteTaggedString(writer, nameof(Guid), guid.ToString("D", CultureInfo.InvariantCulture));
                return;

            case BigDecimal bd:
                WriteBigDecimal(writer, bd);
                return;

            case ComplexNumber cn:
                writer.WriteStartObject();
                writer.WriteString(TypePropertyName, "ComplexNumber");
                writer.WritePropertyName("Real");
                WriteBigDecimal(writer, cn.Real);
                writer.WritePropertyName("Imaginary");
                WriteBigDecimal(writer, cn.Imaginary);
                writer.WriteEndObject();
                return;

            case NCalcVector v:
                writer.WriteStartObject();
                writer.WriteString(TypePropertyName, "Vector");
                writer.WriteStartArray("Components");
                foreach (BigDecimal component in v.Components)
                    WriteBigDecimal(writer, component);
                writer.WriteEndArray();
                writer.WriteEndObject();
                return;

            case DateTimeOffset dto:
                WriteTaggedString(writer, nameof(DateTimeOffset), dto.ToString("O", CultureInfo.InvariantCulture));
                return;

            case JsonElement element:
                element.WriteTo(writer);
                return;

            case IDictionary<string, object?> dict:
                // The entries are wrapped in a tagged object, so that a dictionary whose keys look like a BigDecimal ("Mantissa", "Exponent")
                // or a tagged value ("$type", "$value") is read back as a dictionary
                writer.WriteStartObject();
                writer.WriteString(TypePropertyName, DictionaryTypeName);
                writer.WritePropertyName(ValuePropertyName);
                writer.WriteStartObject();
                foreach (KeyValuePair<string, object?> pair in dict)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                return;

            case Array array when array.Rank == 1 && GetArrayElementTypeName(array.GetType().GetElementType()) is string elementTypeName:
                // An array of a supported element type keeps its type: {"$type": "Int32[]", "$value": [...]}
                writer.WriteStartObject();
                writer.WriteString(TypePropertyName, elementTypeName + ArrayTypeSuffix);
                writer.WriteStartArray(ValuePropertyName);
                foreach (object? item in array)
                    WriteValue(writer, item);
                writer.WriteEndArray();
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
                    "This converter supports primitive scalar types, string, DateTime, DateTimeOffset, TimeSpan, BigInteger, " +
                    "BigDecimal, ComplexNumber, Vector, JsonElement, dictionaries, and arrays/lists.");
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
