using System;
using System.Collections.Generic;
using System.Text;

namespace OdysseyCards.Localization;

public static class YamlParser
{
    public static Dictionary<string, object> Parse(string yamlContent)
    {
        if (string.IsNullOrEmpty(yamlContent))
        {
            return new Dictionary<string, object>();
        }

        string[] lines = yamlContent.Split('\n');
        Dictionary<string, object> result = new();
        Stack<(int indent, Dictionary<string, object> dict)> stack = new();
        stack.Push((-1, result));

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            int indent = GetIndentLevel(line);
            string trimmedLine = line.TrimStart();

            int colonIndex = trimmedLine.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            string key = trimmedLine.Substring(0, colonIndex).Trim();
            string valuePart = colonIndex + 1 < trimmedLine.Length
                ? trimmedLine.Substring(colonIndex + 1).Trim()
                : null;

            while (stack.Count > 0 && stack.Peek().indent >= indent)
            {
                _ = stack.Pop();
            }

            Dictionary<string, object> currentDict = stack.Peek().dict;

            if (string.IsNullOrEmpty(valuePart))
            {
                Dictionary<string, object> nestedDict = new();
                currentDict[key] = nestedDict;
                stack.Push((indent, nestedDict));
            }
            else
            {
                object parsedValue = ParseValue(valuePart);
                currentDict[key] = parsedValue;
            }
        }

        return result;
    }

    public static Dictionary<string, string> Flatten(Dictionary<string, object> nested, string prefix = "")
    {
        Dictionary<string, string> result = new();

        foreach (KeyValuePair<string, object> kvp in nested)
        {
            string fullKey = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                Dictionary<string, string> flattened = Flatten(nestedDict, fullKey);
                foreach (KeyValuePair<string, string> inner in flattened)
                {
                    result[inner.Key] = inner.Value;
                }
            }
            else
            {
                result[fullKey] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        return result;
    }

    private static int GetIndentLevel(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
            {
                count++;
            }
            else if (c == '\t')
            {
                count += 2;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    private static object ParseValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.StartsWith('"') && value.EndsWith('"'))
        {
            return UnescapeString(value.Substring(1, value.Length - 2));
        }

        if (value.StartsWith('\'') && value.EndsWith('\''))
        {
            return value.Substring(1, value.Length - 2);
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (int.TryParse(value, out int intValue))
        {
            return intValue;
        }

        if (float.TryParse(value, out float floatValue))
        {
            return floatValue;
        }

        return value;
    }

    private static string UnescapeString(string value)
    {
        StringBuilder result = new();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                char next = value[i + 1];
                switch (next)
                {
                    case 'n':
                        _ = result.Append('\n');
                        i++;
                        break;
                    case 't':
                        _ = result.Append('\t');
                        i++;
                        break;
                    case 'r':
                        _ = result.Append('\r');
                        i++;
                        break;
                    case '\\':
                        _ = result.Append('\\');
                        i++;
                        break;
                    case '"':
                        _ = result.Append('"');
                        i++;
                        break;
                    default:
                        _ = result.Append(value[i]);
                        break;
                }
            }
            else
            {
                _ = result.Append(value[i]);
            }
        }
        return result.ToString();
    }
}
