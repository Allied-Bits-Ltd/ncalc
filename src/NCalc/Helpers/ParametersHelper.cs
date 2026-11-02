using NCalc.Exceptions;

namespace NCalc.Helpers;

/// <summary>
/// Provides helper methods for handling parameters.
/// </summary>
public static class ParametersHelper
{
    /// <summary>
    /// Gets enumerators for the IEnumerable parameters and checks that they all have the same number of items.
    /// Strings and dictionaries are single values and are not iterated.
    /// </summary>
    /// <param name="parameters">The dictionary of parameters.</param>
    /// <param name="size">The size of the enumerable, if any. Set to null if there are no IEnumerable parameters.</param>
    /// <returns>A dictionary of parameter names and their corresponding enumerators.</returns>
    /// <exception cref="NCalcException">Thrown when the IEnumerable parameters do not have the same number of items.</exception>
    public static Dictionary<string, IEnumerator> GetEnumerators(IDictionary<string, object?> parameters, out int? size)
    {
        var parameterEnumerators = new Dictionary<string, IEnumerator>();
        size = null;

        foreach (var parameter in parameters)
        {
            // A string is one value, not a list of characters, and a dictionary is one value, not a list of key/value pairs
            if (parameter.Value is string || parameter.Value is IDictionary || parameter.Value is not IEnumerable enumerable)
                continue;

            var list = enumerable as List<object> ?? enumerable.Cast<object>().ToList();
            parameterEnumerators.Add(parameter.Key, list.GetEnumerator());

            var localSize = list.Count;

            if (size is null)
            {
                size = localSize;
            }
            else if (localSize != size)
            {
                throw new NCalcException(
                    "When IterateParameters option is used, IEnumerable parameters must have the same number of items");
            }
        }

        return parameterEnumerators;
    }
}

/// <summary>
/// The parameters of an expression during one evaluation with <see cref="ExpressionOptions.IterateParameters"/>: the iterated
/// parameters have the current elements, the other ones come from the parameters of the expression. The current elements are kept
/// here, so the parameters of the expression are not changed by the iteration (and may be read-only); values assigned to other
/// parameters are written into the parameters of the expression, as without the option.
/// </summary>
internal sealed class IteratedParameters(IDictionary<string, object?> parameters, IEnumerable<string> iteratedKeys) : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _current = CreateCurrent(parameters, iteratedKeys);

    private static Dictionary<string, object?> CreateCurrent(IDictionary<string, object?> parameters, IEnumerable<string> iteratedKeys)
    {
        var current = new Dictionary<string, object?>((parameters as Dictionary<string, object?>)?.Comparer ?? StringComparer.Ordinal);
        foreach (var key in iteratedKeys)
            current[key] = null;
        return current;
    }

    public void SetCurrent(string key, object? value) => _current[key] = value;

    public object? this[string key]
    {
        get => _current.TryGetValue(key, out var value) ? value : parameters[key];
        set
        {
            if (_current.ContainsKey(key))
                _current[key] = value;
            else
                parameters[key] = value;
        }
    }

    public ICollection<string> Keys => parameters.Keys;

    public ICollection<object?> Values
    {
        get
        {
            var values = new List<object?>();
            foreach (var pair in this)
                values.Add(pair.Value);
            return values;
        }
    }

    public int Count => parameters.Count;

    public bool IsReadOnly => parameters.IsReadOnly;

    public void Add(string key, object? value) => parameters.Add(key, value);

    public void Add(KeyValuePair<string, object?> item) => parameters.Add(item);

    public void Clear() => parameters.Clear();

    public bool Contains(KeyValuePair<string, object?> item)
        => TryGetValue(item.Key, out var value) && Equals(value, item.Value);

    public bool ContainsKey(string key) => parameters.ContainsKey(key);

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        foreach (var pair in this)
            array[arrayIndex++] = pair;
    }

    public bool Remove(string key)
    {
        _current.Remove(key);
        return parameters.Remove(key);
    }

    public bool Remove(KeyValuePair<string, object?> item) => Contains(item) && Remove(item.Key);

    public bool TryGetValue(string key, out object? value)
    {
        return _current.TryGetValue(key, out value) || parameters.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach (var pair in parameters)
            yield return _current.TryGetValue(pair.Key, out var value) ? new KeyValuePair<string, object?>(pair.Key, value) : pair;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
