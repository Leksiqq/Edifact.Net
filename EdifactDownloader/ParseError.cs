using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public enum ErrorTypes { INFO, WARNING, ERROR, FATAL };

public class ParseError
{
    class LocationComparer : IEqualityComparer<Location>
    {
        public bool Equals(Location x, Location y)
        {
            return (x.offset == y.offset) && (x.line == y.line) && (x.col == y.col);
        }

        public int GetHashCode(Location obj)
        {
            return obj.GetHashCode();
        }
    }

    LocationComparer lc = new LocationComparer();

    ErrorTypes type = ErrorTypes.INFO;
    ErrorKinds kind;
    string message = null;
    List<Location> locations = new List<Location>();
    List<string> data = new List<string>();
    List<string> extra_messages = new List<string>();
    static ResourceManager rm_errors = new ResourceManager("Net.Leksi.EDIFACT.Properties.errors", Assembly.GetExecutingAssembly());

    public ParseError(ErrorTypes type, ErrorKinds kind)
    {
        this.type = type;
        this.kind = kind;
    }

    public ParseError AddLocation(Location loc)
    {
        message = null;
        locations.Add(loc);
        return this;
    }

    public ParseError AddData(string data)
    {
        message = null;
        this.data.Add(data);
        return this;
    }

    public ParseError AddExteraMessage(string format, object[] args = null)
    {
        message = null;
        string frm = rm_errors.GetString(format);
        if (frm == null)
        {
            frm = format;
        }
        if (args != null)
        {
            this.extra_messages.Add(string.Format(frm, args));
        }
        else
        {
            this.extra_messages.Add(frm);
        }
        return this;
    }

    public bool IsAt(Location loc)
    {
        return locations.Count > 0 &&  lc.Equals(locations[0], loc);
    }

    public string Message
    {
        get
        {
            if (message == null)
            {
                StringBuilder format = new StringBuilder();
                try
                {
                    string format0 = rm_errors.GetString(kind.ToString());
                    if (format0 == null)
                    {
                        format0 = kind.ToString();
                    }
                    List<object> objects = new List<object>();
                    Dictionary<string, string> map = new Dictionary<string, string>();
                    Regex re = new Regex("(?:[^{]|^){([^},]+)[^}]*}(?:[^}]|$)");
                    int pos = 0;
                    while (true)
                    {
                        Match m = re.Match(format0.Substring(pos));
                        if (!m.Success)
                        {
                            format.Append(format0.Substring(pos));
                            break;
                        }
                        string s = format0.Substring(pos, m.Groups[1].Captures[0].Index);
                        format.Append(s);
                        string index = m.Groups[1].Captures[0].Value.Trim().ToUpper();
                        string new_index;
                        if (map.ContainsKey(index))
                        {
                            new_index = map[index];
                        }
                        else
                        {
                            new_index = objects.Count.ToString();
                            map.Add(index, new_index);
                            if (index.StartsWith("L") || index.StartsWith("C") || index.StartsWith("O"))
                            {
                                int ind = int.Parse(index.Substring(1));
                                Location loc = locations[ind];
                                if (index.StartsWith("L"))
                                {
                                    objects.Add(loc.line);
                                }
                                else if (index.StartsWith("C"))
                                {
                                    objects.Add(loc.col);
                                }
                                else if (index.StartsWith("O"))
                                {
                                    objects.Add(loc.offset);
                                }
                            }
                            else
                            {
                                int ind = int.Parse(index);
                                objects.Add(data[ind]);
                            }
                        }
                        format.Append(new_index);
                        s = format0.Substring(
                            pos + m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length,
                            m.Groups[0].Index + m.Groups[0].Length - (m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length)
                        );
                        format.Append(s);
                        pos += m.Groups[0].Index + m.Groups[0].Length;
                    }
                    object[] args = new object[objects.Count];
                    objects.CopyTo(args);
                    message = string.Format(format.ToString(), args);
                    foreach (string em in extra_messages)
                    {
                        message += " (" + em;
                    }
                    foreach (string em in extra_messages)
                    {
                        message += ")";
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("s_directoryNotExistsFormat: " + format.ToString(), ex);
                }
            }
            return message;
        }
    }

}
