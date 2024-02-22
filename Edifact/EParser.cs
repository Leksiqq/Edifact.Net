using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
namespace Net.Leksi.Edifact;

internal class EParser : PartsParser
{
    private const string s_rmErrorsName = "Net.Leksi.Edifact.Properties.errors";
    private const string s_descNotFound = "DESC_NOT_FOUND";
    private enum Stages { NONE, NAME, DESC, REPR, NOTE };
    internal delegate void SimpleType(string name, string change_indicator, string info, string description, string repr, string note);
    internal event SimpleType? OnSimpleType;

    private static readonly Regex reName = new("^([-\\*+#|X]*)\\s*(\\d{4})\\s+(.*?)(?:\\s+\\[.\\])?\\s*$");
    private static readonly Regex reDesr = new("^[-\\*+#|X]*\\s*(Desc:.*)$");
    private static readonly Regex reRepr = new("^\\s+Repr:(.*)$");
    private static readonly ResourceManager s_rmErrors;

    private readonly StringBuilder _info = new();
    private readonly StringBuilder _desc = new();
    private readonly StringBuilder _note = new();

    private string? _name;
    private string _repr = null!;
    private string _changeIndicator = null!;

    Stages stage = Stages.NONE;


    static EParser()
    {
        s_rmErrors = new ResourceManager(s_rmErrorsName, Assembly.GetExecutingAssembly());
    }
    internal EParser()
    {
        OnPart += EParser_OnPart;
        OnLine += EParser_OnLine;
        OnSimpleType += new SimpleType(delegate(string name, string change_indicator, string info, string description, string repr, string note) { });
    }

    protected internal override void Run(string[] data)
    {
        base.Run(data);
        if (_name != null)
        {
            OnSimpleType?.Invoke(_name, _changeIndicator, _info.ToString().Trim(), _desc.ToString().Trim(), _repr.Trim(), _note.ToString().Trim());
        }
    }

    private void EParser_OnPart()
    {
        if (_name != null)
        {
            OnSimpleType?.Invoke(_name, _changeIndicator, _info.ToString().Trim(), _desc.ToString().Trim(), _repr.Trim(), _note.ToString().Trim());
        }
        _name = null;
        stage = Stages.NAME;
        _changeIndicator = string.Empty;
        _note.Clear();
    }

    private void EParser_OnLine(string line)
    {
        Match m;
        while (true)
        {
            switch (stage)
            {
                case Stages.NAME:
                    if (string.IsNullOrEmpty(line))
                    {
                        if (_name != null)
                        {
                            stage = Stages.DESC;
                        }
                    }
                    else
                    {
                        m = reName.Match(line);
                        if (m.Success)
                        {
                            _changeIndicator = m.Groups[1].Captures[0].Value.Trim();
                            _name = m.Groups[2].Captures[0].Value.Trim();
                            _info.Clear();
                            _info.Append(m.Groups[3].Captures[0].Value.Trim());
                        }
                        else
                        {
                            _info.Append(' ').Append(line.Trim());
                        }
                    }
                    break;
                case Stages.DESC:
                    if (string.IsNullOrEmpty(line))
                    {
                        stage = Stages.REPR;
                    }
                    else
                    {
                        m = reDesr.Match(line);
                        if (m.Success)
                        {
                            _desc.Clear();
                            _desc.Append(m.Groups[1].Captures[0].Value.Trim());
                        }
                        else
                        {
                            if (_desc.Length == 0)
                            {
                                throw new Exception(string.Format(s_rmErrors.GetString(s_descNotFound)!, _name));
                            }
                            else
                            {
                                _desc.Append(' ').Append(line.Trim());
                            }
                        }
                    }
                    break;
                case Stages.REPR:
                    if (string.IsNullOrEmpty(line))
                    {
                        stage = Stages.NOTE;
                    }
                    else
                    {
                        m = reRepr.Match(line);
                        if (m.Success)
                        {
                            _repr = m.Groups[1].Captures[0].Value.Trim();
                        }
                    }
                    break;
                case Stages.NOTE:
                    if (_note.Length > 0)
                    {
                        _note.Append('\t');
                    }
                    _note.AppendLine(line.Trim());
                    break;
            }
            break;
        }
    }
}
