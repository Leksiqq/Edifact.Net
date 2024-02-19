using System.Text.RegularExpressions;
namespace Net.Leksi.Edifact;

public class EParser : PartsParser
{
    string name = null;
    string info;
    string desc;
    string repr;
    string note;
    string change_indicator;

    Regex reName = new Regex("^([-\\*+#|X]*)\\s*(\\d{4})\\s+(.*?)(?:\\s+\\[.\\])?\\s*$");
    Regex reDesr = new Regex("^[-\\*+#|X]*\\s*(Desc:.*)$");
    Regex reRepr = new Regex("^\\s+Repr:(.*)$");

    enum Stages { NONE, NAME, DESC, REPR, NOTE };
    Stages stage = Stages.NONE;
    public delegate void SimpleType(string name, string change_indicator, string info, string description, string repr, string note);

    public event SimpleType OnSimpleType;

    public EParser()
    {
        OnPart += new Part(on_part);
        OnLine += new Line(on_line);
        OnSimpleType += new SimpleType(delegate(string name, string change_indicator, string info, string description, string repr, string note) { });
    }

    public override void Run(string[] data)
    {
        base.Run(data);
        if (name != null)
        {
            OnSimpleType(name, change_indicator, info.Trim(), desc.Trim(), repr.Trim(), note.Trim());
        }
    }

    void on_part()
    {
        if (name != null)
        {
            OnSimpleType(name, change_indicator, info.Trim(), desc.Trim(), repr.Trim(), note.Trim());
        }
        name = null;
        stage = Stages.NAME;
        change_indicator = "";
        note = "";
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
                        }
                    }
                    else
                    {
                        m = reName.Match(line);
                        if (m.Success)
                        {
                            change_indicator = m.Groups[1].Captures[0].Value.Trim();
                            name = m.Groups[2].Captures[0].Value.Trim();
                            info = m.Groups[3].Captures[0].Value.Trim();
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
                        m = reDesr.Match(line);
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
                                desc += " " + line.Trim();
                            }
                        }
                    }
                    break;
                case Stages.REPR:
                    if ("".Equals(line))
                    {
                        stage = Stages.NOTE;
                    }
                    else
                    {
                        m = reRepr.Match(line);
                        if (m.Success)
                        {
                            repr = m.Groups[1].Captures[0].Value.Trim();
                        }
                    }
                    break;
                case Stages.NOTE:
                    if (!note.Equals(""))
                    {
                        note += "\t";
                    }
                    note += line.Trim() + "\r\n";
                    break;
            }
            break;
        }
    }
}
