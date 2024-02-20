
using System.Text.RegularExpressions;
namespace Net.Leksi.Edifact;

internal class CLParser : PartsParser
{
    string name;
    string value = null;
    string info;
    string desc;
    string value_note;
    string change_indicator;

    Regex reName = new Regex("^[-\\*+#|X]*\\s*(\\d{4})(.*)$");
    Regex reDesc = new Regex("^[-\\*+#|X]*\\s*(Desc:.*)$");
    Regex reRepr = new Regex("^\\s+Repr:.*$");
    Regex reNote = new Regex("^[-\\*+#|X]*\\s*(Note:.*)$");
    Regex reValue = null;
    Regex reValueDesc = null;

    Stages stage = Stages.NONE;

    enum Stages { NONE, NAME, DESC, REPR, NOTE, VALUE };

    public delegate void SimpleType(string name);
    public delegate void Item(string value, string change_indicator, string info, string description);

    public event SimpleType OnSimpleType;
    public event Item OnItem;

    public CLParser()
    {
        OnPart += new Part(on_part);
        OnLine += new Line(on_line);
        OnSimpleType += new SimpleType(delegate(string name) { });
        OnItem += new Item(delegate(string value, string change_indicator, string info, string description) { });
    }

    protected internal override void Run(string[] data)
    {
        base.Run(data);
        if (value != null)
        {
            OnItem(value, change_indicator, info, desc);
        }
    }

    void on_part()
    {
        if (value != null)
        {
            OnItem(value, change_indicator, info, desc);
        }
        value = null;
        stage = Stages.NAME;
        name = null;
    }

    void on_line(string line)
    {
        Match m;
        while (true)
        {
            switch (stage)
            {
                case Stages.NAME:
                    if ("".Equals(line))
                    {
                        if (name != null)
                        {
                            stage = Stages.DESC;
                            OnSimpleType(name);
                        }
                    }
                    else
                    {
                        m = reName.Match(line);
                        if (m.Success)
                        {
                            name = m.Groups[1].Captures[0].Value.Trim();
                            info = m.Groups[2].Captures[0].Value.Trim();
                        }
                        else
                        {
                            info += " " + line.Trim();
                        }
                    }
                    break;
                case Stages.DESC:
                    if ("".Equals(line))
                    {
                        stage = Stages.REPR;
                    }
                    else
                    {
                        m = reDesc.Match(line);
                        if (m.Success)
                        {
                            desc = m.Groups[1].Captures[0].Value.Trim();
                        }
                        else
                        {
                            if ("".Equals(desc))
                            {
                                throw new Exception(name + ": 'Desc:' not found.");
                            }
                            else
                            {
                                m = reRepr.Match(line);
                                if (m.Success)
                                {
                                    stage = Stages.REPR;
                                    continue;
                                }
                                else
                                {
                                    desc += " " + line.Trim();
                                }
                            }
                        }
                    }
                    break;
                case Stages.REPR:
                    if ("".Equals(line))
                    {
                        stage = Stages.NOTE;
                        value_note = "";
                    }
                    else
                    {
                        m = reRepr.Match(line);
                        if (m.Success)
                        {
                        }
                    }
                    break;
                case Stages.NOTE:
                    if ("".Equals(line))
                    {
                        stage = Stages.VALUE;
                    }
                    else
                    {
                        m = reNote.Match(line);
                        if (m.Success)
                        {
                            value_note = m.Groups[1].Captures[0].Value.Trim();
                        }
                        else
                        {
                            if ("".Equals(value_note))
                            {
                                stage = Stages.VALUE;
                                continue;
                            }
                            else
                            {
                                value_note += "\t" + line.Trim();
                            }
                        }
                    }
                    break;
                case Stages.VALUE:
                    if (reValue == null)
                    {
                        m = Regex.Match(line, "^[\\*+#|X]?\\s+([^\\s]+)\\s+.*$");
                        if (m.Success)
                        {
                            int max_value_pos = m.Groups[1].Captures[0].Index + m.Groups[1].Captures[0].Length;
                            reValue = new Regex("^([\\*+#|X]|\\s)\\s{1," + (max_value_pos - 1) + "}([^\\s]+)\\s+(.*)$");
                            reValueDesc = new Regex("^\\s{" + (max_value_pos + 1) + ",}.*$");
                        }
                    }
                    m = reValue.Match(line);
                    if (m.Success)
                    {
                        if (value != null)
                        {
                            OnItem(value, change_indicator, info, desc);
                        }
                        change_indicator = m.Groups[1].Captures[0].Value.Trim();
                        value = m.Groups[2].Captures[0].Value.Trim();
                        info = m.Groups[3].Captures[0].Value.Trim();
                        desc = "";
                    }
                    else
                    {
                        m = reValueDesc.Match(line);
                        if (m.Success)
                        {
                            if (!"".Equals(desc))
                            {
                                desc += " ";
                            }
                            desc += line.Trim();
                        }
                    }
                    break;
            }
            break;
        }
    }
}
