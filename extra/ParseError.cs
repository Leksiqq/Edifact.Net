using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal enum ErrorTypes { INFO, WARNING, ERROR, FATAL };
internal class ParseError
{
    private class LocationComparer : IEqualityComparer<Location>
    {
        public bool Equals(Location? x, Location? y)
        {
            return (x!.Offset == y!.Offset) && (x.Line == y.Line) && (x.Col == y.Col);
        }

        public int GetHashCode(Location obj)
        {
            return HashCode.Combine(obj.Offset, obj.Line, obj.Col);
        }
    }

    private static readonly ResourceManager s_rmErrors = new("Net.Leksi.EDIFACT.Properties.errors", Assembly.GetExecutingAssembly());
    private static readonly Regex s_re = new("(?:[^{]|^){([^},]+)[^}]*}(?:[^}]|$)");

    private readonly LocationComparer _lc = new();
    private readonly List<Location> _locations = [];
    private readonly List<string> _data = [];
    private readonly List<string> _extraMessages = [];
    private readonly StringBuilder _message = new();

    internal ErrorTypes Type { get; private init; }
    internal ErrorKinds Kind { get; private init; }

    internal ParseError(ErrorTypes type, ErrorKinds kind)
    {
        Type = type;
        Kind = kind;
    }

    internal ParseError AddLocation(Location loc)
    {
        _message.Clear();
        _locations.Add(loc);
        return this;
    }

    internal ParseError AddData(string data)
    {
        _message.Clear();
        _data.Add(data);
        return this;
    }

    internal ParseError AddExteraMessage(string format, object[]? args = null)
    {
        _message.Clear();
        string? frm = s_rmErrors.GetString(format);
        frm ??= format;
        if (args is { })
        {
            _extraMessages.Add(string.Format(frm, args));
        }
        else
        {
            _extraMessages.Add(frm);
        }
        return this;
    }

    internal bool IsAt(Location loc)
    {
        return _locations.Count > 0 && _lc.Equals(_locations[0], loc);
    }

    internal string Message
    {
        get
        {
            if (_message.Length == 0)
            {
                StringBuilder format = new();
                string? format0 = s_rmErrors.GetString(Kind.ToString());
                format0 ??= Kind.ToString();
                List<object> objects = [];
                Dictionary<string, string> map = [];
                int pos = 0;
                while (true)
                {
                    Match m = s_re.Match(format0[pos..]);
                    if (!m.Success)
                    {
                        format.Append(format0[pos..]);
                        break;
                    }
                    string s = format0.Substring(pos, m.Groups[1].Captures[0].Index);
                    format.Append(s);
                    string index = m.Groups[1].Captures[0].Value.Trim().ToUpper();
                    if (!map.TryGetValue(index, out string? newIndex))
                    {
                        newIndex = objects.Count.ToString();
                        map.Add(index, newIndex);
                        if (index.StartsWith('L') || index.StartsWith('C') || index.StartsWith('O'))
                        {
                            int ind = int.Parse(index[1..]);
                            Location loc = _locations[ind];
                            if (index.StartsWith('L'))
                            {
                                objects.Add(loc.Line);
                            }
                            else if (index.StartsWith('C'))
                            {
                                objects.Add(loc.Col);
                            }
                            else if (index.StartsWith('O'))
                            {
                                objects.Add(loc.Offset);
                            }
                        }
                        else
                        {
                            int ind = int.Parse(index);
                            objects.Add(_data[ind]);
                        }
                    }
                    format.Append(newIndex);
                    s = format0.Substring(
                        pos + m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length,
                        m.Groups[0].Index + m.Groups[0].Length - (m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length)
                    );
                    format.Append(s);
                    pos += m.Groups[0].Index + m.Groups[0].Length;
                }
                object[] args = new object[objects.Count];
                objects.CopyTo(args);
                _message.Append(string.Format(format.ToString(), args));
                foreach (string em in _extraMessages)
                {
                    _message.Append(' ').Append('(').Append(em);
                }
                for (int i = 0; i < _extraMessages.Count; i++)
                {
                    _message.Append(')');
                }
            }
            return _message.ToString();
        }
    }

}
