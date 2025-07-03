using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NCalc.Parser;

public static class BigIntegerParser
{
    /// <summary>
    /// Parses a string of the specified base into a BigInteger.
    /// Throws OverflowException if the string contains invalid characters.
    /// </summary>
    /// <param name="value">The input string to parse.</param>
    /// <param name="numberBase">The base: 2, 8, 10, or 16.</param>
    /// <param name="result">The output BigInteger value.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseBigInteger(string value, int numberBase, out BigInteger result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        bool isNegative = false;

        if (value.StartsWith("-", StringComparison.Ordinal))
        {
            isNegative = true;
            value = value.Substring(1);
        }
        else if (value.StartsWith("+", StringComparison.Ordinal))
        {
            value = value.Substring(1);
        }

        if (value.Length == 0)
            return false;

        try
        {
            switch (numberBase)
            {
                case 10:
                    // Let BigInteger handle decimal parsing.
                    result = BigInteger.Parse(value, CultureInfo.InvariantCulture);
                    if (isNegative) result = -result;
                    return true;
                case 16:
                case 8:
                case 2:
                    result = ParseFromBase(value, numberBase);
                    if (isNegative) result = -result;
                    return true;
                default:
                    throw new ArgumentException("Base must be 2, 8, 10, or 16.");
            }
        }
        catch (FormatException)
        {
            throw new OverflowException("The value contains invalid characters for the specified base.");
        }
        catch (ArgumentException)
        {
            throw new OverflowException("The value contains invalid characters for the specified base.");
        }
    }

    private static BigInteger ParseFromBase(string value, int numberBase)
    {
        BigInteger result = 0;
        foreach (char c in value)
        {
            int digit = CharToDigit(c);
            if (digit < 0 || digit >= numberBase)
                throw new OverflowException("Invalid character for base " + numberBase + ": " + c);
            result = result * numberBase + digit;
        }
        return result;
    }

    private static int CharToDigit(char c)
    {
        if (c >= '0' && c <= '9')
            return c - '0';
        if (c >= 'A' && c <= 'F')
            return c - 'A' + 10;
        if (c >= 'a' && c <= 'f')
            return c - 'a' + 10;
        return -1; // invalid character
    }
}