using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class Preparser2
{
    private enum Stages { NONE, SEGORSG, SG_REST, OCCURS, DONE };

    private const string s_fileNameFormat = "{0}.1";
    private static readonly Regex s_reSeg = new("^\\s*([A-Z]{3})[^\\w].*$");
    private static readonly Regex s_reSG = new("^\\s*(?:Segment\\s+)?[gG]roup\\s+\\d+\\s*[:.,]?.*$");
    private static readonly Regex s_reHeaders = new("^\\s*(TAG)\\s+(NAME)\\s+(S|status)(?:\\s+|/)(REPT|repeats).*$", RegexOptions.IgnoreCase);
    private static readonly Regex s_reUNH = new("^\\s*UNH\\s*,?\\s*s_logMessage header.*$");
    private static readonly Regex s_reBGM = new("^\\s*BGM\\s*,?\\s*Beginning of message.*$");
    private static readonly Regex s_reUNT = new("^\\s*UNT\\s+s_logMessage trailer.*$");
    private static readonly Regex s_reOccSeg = new("^[-\\s\\*+#|X]*([A-Z]{3})\\s+(.*?)\\s*([CM])\\s+(\\d+).*$");
    private static readonly Regex s_reOccSG = new("^.*?Segment\\s+[gG]roup\\s+(\\d+).*?([CM])\\s+(\\d+).*$");

    private TextWriter _tw;
    private Stages _stage = Stages.NONE;
    private bool _unhPassed = false;
    private bool _bgmPassed = false;
    private bool _untPassed = false;
    private bool _headersPassed = false;
    private int _cnt = 0;
    private int _tagPos = 0;
    private int _namePos = 0;
    private int _sPos = 0;
    private int _rPos = 0;
    private string _savefile;
    private int _lineNum = 0;

    public Preparser2(string filename)
    {
        _tw = new StreamWriter(filename, false);
        _savefile = Path.Combine(Path.GetDirectoryName(filename)!, string.Format(s_fileNameFormat, Path.GetFileName(filename)));
        if (File.Exists(_savefile))
        {
            File.Delete(_savefile);
        }
    }

    public void Run(string[] data)
    {
        File.AppendAllLines(_savefile, data);
        _stage = Stages.NONE;
        _unhPassed = false;
        _bgmPassed = false;
        _untPassed = false;
        _headersPassed = false;
        _cnt = 0;
        _tagPos = 0;
        _namePos = 0;
        _sPos = 0;
        _rPos = 0;
        _lineNum = 0;
        try
        {
            foreach (string line in data)
            {
                OnLine(line);
            }
        }
        finally
        {
            _tw.Close();
        }
    }

    private void OnLine(string line)
    {
        ++_lineNum;
        bool canWrite = true;
        while (true)
        {
            Match m;
            switch (_stage)
            {
                case Stages.NONE:
                    if (s_reUNH.IsMatch(line))
                    {
                        _stage = Stages.SEGORSG;
                        continue;
                    }
                    break;
                case Stages.SEGORSG:
                    m = s_reSG.Match(line);
                    if (m.Success)
                    {
                        //Console.WriteLine(line);
                        _cnt++;
                        line = string.Format("{0:0000}   ", _cnt * 10) + line.Replace("Group", "group");
                        if (!line.Contains("egment"))
                        {
                            line = line.Replace("group", "Segment group");
                        }
                        if (line.Contains("."))
                        {
                            line = line.Replace(".", "");
                        }
                        if (line.Contains(","))
                        {
                            line = line.Replace(",", "");
                        }
                        if (!line.Contains(":"))
                        {
                            line = Regex.Replace(line, "Segment\\s+group\\s+\\d+([^\\d])", delegate(Match m1)
                            {
                                //Console.WriteLine("+" + m1.Value + "+" + m1.Groups[1].Captures[0].Index);
                                return m1.Groups[0].Value.Substring(0, m1.Groups[1].Captures[0].Index - m1.Groups[0].Index) + ":" + m1.Groups[1].Captures[0].Value;
                            });
                        }
                        _stage = Stages.SG_REST;
                    }
                    else
                    {
                        m = s_reSeg.Match(line);
                        if (m.Success)
                        {
                            //Console.WriteLine(line);
                            _cnt++;
                            line = string.Format("{0:0000}   ", _cnt * 10) + line;
                            _stage = Stages.SG_REST;
                            if ("UNH".Equals(m.Groups[1].Captures[0].Value))
                            {
                                _unhPassed = true;
                            }
                            else if ("BGM".Equals(m.Groups[1].Captures[0].Value))
                            {
                                _bgmPassed = true;
                            }
                            else if ("UNT".Equals(m.Groups[1].Captures[0].Value))
                            {
                                _untPassed = true;
                            }
                        }
                        else
                        {
                            line = "       " + line;
                        }
                    }
                    break;
                case Stages.SG_REST:
                    //if (bgm_passed && "".Equals(line.Trim()))
                    if (_unhPassed && "".Equals(line.Trim()))
                    {
                        if (_untPassed)
                        {
                            _stage = Stages.OCCURS;
                            _cnt = 0;
                        }
                        else
                        {
                            _stage = Stages.SEGORSG;
                        }
                    }
                    else
                    {
                        if (s_reBGM.IsMatch(line))
                        {
                            _stage = Stages.SEGORSG;
                            continue;
                        }
                        line = "       " + line;
                    }
                    break;
                case Stages.OCCURS:
                    m = s_reHeaders.Match(line);
                    if (m.Success)
                    {
                        line = "";
                        if (!_headersPassed)
                        {
                            _tagPos = m.Groups[1].Captures[0].Index + 7;
                            _namePos = m.Groups[2].Captures[0].Index + 7;
                            _sPos = m.Groups[3].Captures[0].Index + 7;
                            _rPos = m.Groups[4].Captures[0].Index + 7;
                            _headersPassed = true;
                            int pos = 0;
                            for (int i = 1; i <= 4; i++)
                            {
                                if (i == 1)
                                {
                                    line += "POS    ";
                                }
                                else
                                {
                                    for (int j = pos; j < m.Groups[i].Captures[0].Index; j++)
                                    {
                                        line += " ";
                                    }
                                }
                                string repl = m.Groups[i].Captures[0].Value;
                                if (i == 3)
                                {
                                    repl = "S";
                                }
                                if (i == 4)
                                {
                                    repl = "R";
                                }
                                line += repl;
                                pos = m.Groups[i].Captures[0].Index + repl.Length;
                            }
                        }
                    }
                    else
                    {
                        if (_headersPassed && !"".Equals(line.Trim()))
                        {
                            if (line.Length > _tagPos - 5 && line[_tagPos - 7] != ' ' && line[_tagPos - 6] != ' ' && line[_tagPos - 5] != ' ')
                            {
                                if (s_reUNT.IsMatch(line))
                                {
                                    _stage = Stages.DONE;
                                }
                                m = s_reOccSeg.Match(line);
                                if (m.Success)
                                {
                                    _cnt++;
                                    line = string.Format(
                                        "{0:0000}   {1}{2," +
                                        (_namePos - _tagPos - m.Groups[1].Captures[0].Length + m.Groups[2].Captures[0].Length) +
                                        "}{3," +
                                        (_sPos - _namePos - m.Groups[2].Captures[0].Length + m.Groups[3].Captures[0].Length) +
                                        "}{4," +
                                        (_rPos - _sPos - m.Groups[3].Captures[0].Length + m.Groups[4].Captures[0].Length) +
                                        "}",
                                        _cnt * 10,
                                        m.Groups[1].Captures[0].Value,
                                        m.Groups[2].Captures[0].Value,
                                        m.Groups[3].Captures[0].Value,
                                        m.Groups[4].Captures[0].Value
                                    );
                                }
                                else
                                {
                                    m = s_reOccSG.Match(line);
                                    if (m.Success)
                                    {
                                        _cnt++;
                                        string sg = "--- Segment group " + m.Groups[1].Captures[0].Value + " ---";
                                        line = string.Format(
                                            "{0:0000}   {1," +
                                            (_namePos - _tagPos + sg.Length) +
                                            "}{2," +
                                            (_sPos - _namePos - sg.Length + m.Groups[2].Captures[0].Length) +
                                            "}{3," +
                                            (_rPos - _sPos - m.Groups[2].Captures[0].Length + m.Groups[3].Captures[0].Length) +
                                            "}",
                                            _cnt * 10,
                                            sg,
                                            m.Groups[2].Captures[0].Value,
                                            m.Groups[3].Captures[0].Value
                                        );
                                    }
                                }
                            }
                            else
                            {
                                canWrite = false;
                            }
                        }
                    }
                    if (_headersPassed && "".Equals(line.Trim()))
                    {
                        canWrite = false;
                    }
                    break;
            }
            break;
        }
        if (canWrite)
        {
            _tw.WriteLine(line);
        }
    }

}
