using System.Text;

namespace TrainerDeck.Core.Steam;


public sealed class VdfNode
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();

    public IEnumerable<string> Keys => _order;

    public string? GetString(string key) => _values.GetValueOrDefault(key) as string;

    public VdfNode? GetNode(string key) => _values.GetValueOrDefault(key) as VdfNode;

    public object? this[string key] => _values.GetValueOrDefault(key);

    internal void Set(string key, object value)
    {
        if (!_values.ContainsKey(key))
        {
            _order.Add(key);
        }

        _values[key] = value;
    }
}

public static class Vdf
{
    public static VdfNode Parse(string text)
    {
        int pos = 0;
        var root = new VdfNode();
        ParseInto(root, text, ref pos, topLevel: true);
        return root;
    }

    private static void ParseInto(VdfNode node, string text, ref int pos, bool topLevel)
    {
        while (true)
        {
            string? key = NextToken(text, ref pos, out bool keyQuoted);
            if (key is null)
            {
                return;
            }

            if (key == "}" && !keyQuoted)
            {
                if (topLevel)
                {
                    continue;
                }

                return;
            }

            string? value = NextToken(text, ref pos, out bool valueQuoted);
            if (value is null)
            {
                return;
            }

            if (value == "{" && !valueQuoted)
            {
                var child = new VdfNode();
                ParseInto(child, text, ref pos, topLevel: false);
                node.Set(key, child);
            }
            else
            {
                node.Set(key, value);
            }
        }
    }

    private static string? NextToken(string text, ref int pos, out bool quoted)
    {
        quoted = false;
        while (pos < text.Length)
        {
            char c = text[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
            {
                while (pos < text.Length && text[pos] != '\n')
                {
                    pos++;
                }
            }
            else if (c == '[')
            {
                while (pos < text.Length && text[pos] != ']')
                {
                    pos++;
                }

                pos++;
            }
            else
            {
                break;
            }
        }

        if (pos >= text.Length)
        {
            return null;
        }

        char first = text[pos];
        if (first is '{' or '}')
        {
            pos++;
            return first.ToString();
        }

        var sb = new StringBuilder();
        if (first == '"')
        {
            quoted = true;
            pos++;
            while (pos < text.Length && text[pos] != '"')
            {
                char ch = text[pos++];
                if (ch == '\\' && pos < text.Length)
                {
                    char esc = text[pos++];
                    sb.Append(esc switch { 'n' => '\n', 't' => '\t', _ => esc });
                }
                else
                {
                    sb.Append(ch);
                }
            }

            pos++;
            return sb.ToString();
        }

        while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] is not ('{' or '}' or '"'))
        {
            sb.Append(text[pos++]);
        }

        return sb.ToString();
    }
}
