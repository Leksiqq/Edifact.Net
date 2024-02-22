using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
namespace Net.Leksi.Edifact;

internal class CLParser : PartsParser
{
    internal delegate void SimpleType(string name);
    internal delegate void Item(string value, string change_indicator, string info, string description);
    internal event SimpleType? OnSimpleType;
    internal event Item? OnItem;
    private enum Stages { NONE, NAME, DESC, REPR, NOTE, VALUE };

    private const string s_reValueFormat = "^([\\*+#|X]|\\s)\\s{{1,{0}}}([^\\s]+)\\s+(.*)$";
    private const string s_reValueDescFormat = "^\\s{{{0},}}.*$";
    private const string s_space = " ";
    private const string s_descNotFound = "DESC_NOT_FOUND";
    private const string s_rmErrorsName = "Net.Leksi.Edifact.Properties.errors";
    private static readonly Regex s_reName = new("^[-\\*+#|X]*\\s*(\\d{4})(.*)$");
    private static readonly Regex s_reDesc = new("^[-\\*+#|X]*\\s*(Desc:.*)$");
    private static readonly Regex s_reRepr = new("^\\s+Repr:.*$");
    private static readonly Regex s_reNote = new("^[-\\*+#|X]*\\s*(Note:.*)$");
    private static readonly Regex s_reValue = new("^[\\*+#|X]?\\s+([^\\s]+)\\s+.*$");
    private static readonly ResourceManager s_rmErrors;

    private readonly StringBuilder _desc = new();
    private readonly StringBuilder _valueNote = new();

    private Regex _reValue = null!;
    private Regex _reValueDesc = null!;
    private Stages _stage = Stages.NONE;
    private string? _name;
    private string? _value;
    private string _info = null!;
    private string _changeIndicator = null!;

    static CLParser()
    {
        s_rmErrors = new ResourceManager(s_rmErrorsName, Assembly.GetExecutingAssembly());
    }
    internal CLParser()
    {
        OnPart += CLParser_OnPart;
        OnLine += CLParser_OnLine;
    }
    protected internal override void Run(string[] data)
    {
        base.Run(data);
        if (_value != null)
        {
            OnItem?.Invoke(_value, _changeIndicator, _info, _desc.ToString());
        }
    }

    private void CLParser_OnPart()
    {
        if (_value != null)
        {
            OnItem?.Invoke(_value, _changeIndicator, _info, _desc.ToString());
        }
        _value = null;
        _stage = Stages.NAME;
        _name = null;
    }

    private void CLParser_OnLine(string line)
    {
        Match m;
        while (true)
        {
            switch (_stage)
            {
                case Stages.NAME:
                    if (string.IsNullOrEmpty(line))
                    {
                        if (_name is { })
                        {
                            _stage = Stages.DESC;
                            OnSimpleType?.Invoke(_name);
                        }
                    }
                    else
                    {
                        m = s_reName.Match(line);
                        if (m.Success)
                        {
                            _name = m.Groups[1].Captures[0].Value.Trim();
                            _info = m.Groups[2].Captures[0].Value.Trim();
                        }
                        else
                        {
                            _info += s_space + line.Trim();
                        }
                    }
                    break;
                case Stages.DESC:
                    if (string.IsNullOrEmpty(line))
                    {
                        _stage = Stages.REPR;
                    }
                    else
                    {
                        m = s_reDesc.Match(line);
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
                                m = s_reRepr.Match(line);
                                if (m.Success)
                                {
                                    _stage = Stages.REPR;
                                    continue;
                                }
                                else
                                {
                                    _desc.Append(' ').Append(line.Trim());
                                }
                            }
                        }
                    }
                    break;
                case Stages.REPR:
                    if (string.IsNullOrEmpty(line))
                    {
                        _stage = Stages.NOTE;
                        _valueNote.Clear();
                    }
                    break;
                case Stages.NOTE:
                    if (string.IsNullOrEmpty(line))
                    {
                        _stage = Stages.VALUE;
                    }
                    else
                    {
                        m = s_reNote.Match(line);
                        if (m.Success)
                        {
                            _valueNote.Clear();
                            _valueNote.Append(m.Groups[1].Captures[0].Value.Trim());
                        }
                        else
                        {
                            if (_valueNote.Length == 0)
                            {
                                _stage = Stages.VALUE;
                                continue;
                            }
                            else
                            {
                                _valueNote.Append('\t').Append(line.Trim());
                            }
                        }
                    }
                    break;
                case Stages.VALUE:
                    if (_reValue == null)
                    {
                        m = s_reValue.Match(line);
                        if (m.Success)
                        {
                            int maxValuePos = m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length;
                            _reValue = new Regex(string.Format(s_reValueFormat, maxValuePos - 1));
                            _reValueDesc = new Regex(string.Format(s_reValueDescFormat, maxValuePos + 1));
                        }
                    }
                    m = _reValue!.Match(line);
                    if (m.Success)
                    {
                        if (_value != null)
                        {
                            OnItem?.Invoke(_value, _changeIndicator, _info, _desc.ToString());
                        }
                        _changeIndicator = m.Groups[1].Captures[0].Value.Trim();
                        _value = m.Groups[2].Captures[0].Value.Trim();
                        _info = m.Groups[3].Captures[0].Value.Trim();
                        _desc.Clear();
                    }
                    else
                    {
                        m = _reValueDesc.Match(line);
                        if (m.Success)
                        {
                            if (_desc.Length > 0)
                            {
                                _desc.Append(' ');
                            }
                            _desc.Append(line.Trim());
                        }
                    }
                    break;
            }
            break;
        }
    }
}
