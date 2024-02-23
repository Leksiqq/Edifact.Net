using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal class SCParser : PartsParser
{
    string name;
    string info;
    string description;
    string note;
    int min_occurs;
    int max_occurs;
    string repr;
    string change_indicator = "";
    string item_str;
    string item_name;

    Stages stage = Stages.NONE;
    Regex reName = new Regex("^\\s*(?:([-\\*+#|X]+)\\s*)?(\\w[^\\s]*)\\s+(.*)$");
    Regex reDesc = new Regex("^\\s*(?:[-\\*+#|X]+\\s*)?((?:Function|Desc):.*)$");
    Regex reNote = new Regex("^\\s*(?:[-\\*+#|X]+\\s*)?(Note:.*)$");
    Regex reItem = new Regex("^\\d*\\s*(?:[-\\*+#|X]+)?\\s*([C0-9]\\d{3})\\s+([^\\s].*?)\\s+([CM])(?:\\s+(.*))?$");
    Regex reItemStart = new Regex("^\\d*\\s*(?:[-\\*+#|X]+)?\\s*([C0-9]\\d{3})\\s+[^\\s]");
    Regex reMO = new Regex("^(\\d*)\\s*(.*)$");

    enum Stages { NONE, NAME, DESC, ITEM };
    public delegate void SegmentOrType(string name, string change_indicator, string info, string description, string note);
    public delegate void Item(string name, string info, int min_occurs, int max_occurs, string repr);

    public event SegmentOrType OnSegmentOrType;
    public event Item OnItem;
    public bool WaitEmptyStringForNextItem = true;

    public bool debug = false;

    public SCParser()
    {
        OnPart += new Part(on_part);
        OnLine += new Line(on_line);
        OnSegmentOrType += new SegmentOrType(delegate(string name, string change_indicator, string info, string description, string note) { });
        OnItem += new Item(delegate(string name, string info, int min_occurs, int max_occurs, string repr) { });
    }

    void on_part()
    {
        if (item_name != null)
        {
            OnItem(item_name, info, min_occurs, max_occurs, repr);
            item_name = null;
        }
        stage = Stages.NAME;
        change_indicator = "";
        name = null;
    }

    void on_line(string line)
    {
        Console.WriteLine(line);
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
                            description = "";
                            note = "";
                        }
                    }
                    else
                    {
                        m = reName.Match(line);
                        if (m.Success)
                        {
                            name = m.Groups[2].Captures[0].Value.Trim();
                            info = m.Groups[3].Captures[0].Value.Trim();
                            if (m.Groups[1].Captures.Count > 0)
                            {
                                change_indicator = m.Groups[1].Captures[0].Value.Trim();
                            }
                            if (debug)
                            {
                                Console.WriteLine(name);
                            }
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
                        stage = Stages.ITEM;
                        item_name = null;
                        item_str = null;
                    }
                    else
                    {
                        m = reDesc.Match(line);
                        if (m.Success)
                        {
                            description = m.Groups[1].Captures[0].Value.Trim();
                        }
                        else
                        {
                            m = reNote.Match(line);
                            if (m.Success)
                            {
                                note = m.Groups[1].Captures[0].Value.Trim();
                            }
                            else
                            {
                                if (!"".Equals(note))
                                {
                                    note += " " + line.Trim();
                                }
                                else
                                {
                                    description += " " + line.Trim();
                                }
                            }
                        }
                    }
                    break;
                case Stages.ITEM:
                    if ("".Equals(line))
                    {
                        if (item_name != null)
                        {
                            OnItem(item_name, info, min_occurs, max_occurs, repr);
                            item_name = null;
                            item_str = null;
                        }
                    }
                    else
                    {
                        if (item_str != null)
                        {
                            item_str += " " + line.Trim();
                        }
                        else
                        {
                            item_str = line;
                        }
                        m = reItem.Match(item_str);
                        if (m.Success)
                        {
                            //Console.WriteLine(item_str);
                            if (item_name == null)
                            {
                                if (name != null)
                                {
                                    OnSegmentOrType(name, change_indicator, info, description, note);
                                    name = null;
                                }
                                max_occurs = 1;
                                repr = "";
                                if (m.Groups[4].Captures.Count > 0)
                                {
                                    Match m1 = reMO.Match(m.Groups[4].Captures[0].Value.Trim());
                                    if (m1.Success)
                                    {
                                        if (m1.Groups[1].Captures[0].Length > 0)
                                        {
                                            max_occurs = int.Parse(m1.Groups[1].Captures[0].Value.Trim());
                                        }
                                        if (m1.Groups[2].Captures[0].Length > 0)
                                        {
                                            repr = m1.Groups[2].Captures[0].Value.Trim();
                                        }
                                    }
                                }
                                item_name = m.Groups[1].Captures[0].Value.Trim();
                                info = m.Groups[2].Captures[0].Value.Trim();
                                min_occurs = "C".Equals(m.Groups[3].Captures[0].Value.Trim()) ? 0 : 1;
                                if (!WaitEmptyStringForNextItem)
                                {
                                    OnItem(item_name, info, min_occurs, max_occurs, repr);
                                    item_name = null;
                                    item_str = null;
                                }
                            }
                        }
                        else
                        {
                            if (!reItemStart.IsMatch(item_str))
                            {
                                item_str = "";
                                stage = Stages.DESC;
                                continue;
                            }
                        }
                    }
                    break;
            }
            break;
        }
    }

}
