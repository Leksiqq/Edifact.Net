using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public class Preparser2
{
    TextWriter tw;
    enum Stages { NONE, SEGORSG, SG_REST, OCCURS, DONE };
    Stages stage = Stages.NONE;
    bool unh_passed = false;
    bool bgm_passed = false;
    bool unt_passed = false;
    bool headers_passed = false;
    int cnt = 0;
    Regex reSeg = new Regex("^\\s*([A-Z]{3})[^\\w].*$");
    Regex reSG = new Regex("^\\s*(?:Segment\\s+)?[gG]roup\\s+\\d+\\s*[:.,]?.*$");
    Regex reHeaders = new Regex("^\\s*(TAG)\\s+(NAME)\\s+(S|status)(?:\\s+|/)(REPT|repeats).*$", RegexOptions.IgnoreCase);
    Regex reUNH = new Regex("^\\s*UNH\\s*,?\\s*s_logMessage header.*$");
    Regex reBGM = new Regex("^\\s*BGM\\s*,?\\s*Beginning of message.*$");
    Regex reUNT = new Regex("^\\s*UNT\\s+s_logMessage trailer.*$");
    Regex reOccSeg = new Regex("^[-\\s\\*+#|X]*([A-Z]{3})\\s+(.*?)\\s*([CM])\\s+(\\d+).*$");
    Regex reOccSG = new Regex("^.*?Segment\\s+[gG]roup\\s+(\\d+).*?([CM])\\s+(\\d+).*$");
    int TAGpos = 0;
    int NAMEpos = 0;
    int Spos = 0;
    int Rpos = 0;
    string savefile;
    int line_num = 0;

    public Preparser2(string filename)
    {
        tw = new StreamWriter(filename, false, Encoding.GetEncoding(866));
        savefile = Path.GetDirectoryName(filename) + "\\" + Path.GetFileName(filename) + ".1";
        if (File.Exists(savefile))
        {
            File.Delete(savefile);
        }
    }

    public void Run(string[] data)
    {
        File.AppendAllLines(savefile, data, Encoding.GetEncoding(866));
        stage = Stages.NONE;
        unh_passed = false;
        bgm_passed = false;
        unt_passed = false;
        headers_passed = false;
        cnt = 0;
        TAGpos = 0;
        NAMEpos = 0;
        Spos = 0;
        Rpos = 0;
        line_num = 0;
        try
        {
            foreach (string line in data)
            {
                on_line(line);
            }
        }
        finally
        {
            tw.Close();
            //Console.WriteLine(line_num);
        }
    }

    private void on_line(string line)
    {
        line_num++;
        bool can_write = true;
        while (true)
        {
            Match m;
            switch (stage)
            {
                case Stages.NONE:
                    if (reUNH.IsMatch(line))
                    {
                        stage = Stages.SEGORSG;
                        continue;
                    }
                    break;
                case Stages.SEGORSG:
                    m = reSG.Match(line);
                    if (m.Success)
                    {
                        //Console.WriteLine(line);
                        cnt++;
                        line = string.Format("{0:0000}   ", cnt * 10) + line.Replace("Group", "group");
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
                        stage = Stages.SG_REST;
                    }
                    else
                    {
                        m = reSeg.Match(line);
                        if (m.Success)
                        {
                            //Console.WriteLine(line);
                            cnt++;
                            line = string.Format("{0:0000}   ", cnt * 10) + line;
                            stage = Stages.SG_REST;
                            if ("UNH".Equals(m.Groups[1].Captures[0].Value))
                            {
                                unh_passed = true;
                            }
                            else if ("BGM".Equals(m.Groups[1].Captures[0].Value))
                            {
                                bgm_passed = true;
                            }
                            else if ("UNT".Equals(m.Groups[1].Captures[0].Value))
                            {
                                unt_passed = true;
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
                    if (unh_passed && "".Equals(line.Trim()))
                    {
                        if (unt_passed)
                        {
                            stage = Stages.OCCURS;
                            cnt = 0;
                        }
                        else
                        {
                            stage = Stages.SEGORSG;
                        }
                    }
                    else
                    {
                        if (reBGM.IsMatch(line))
                        {
                            stage = Stages.SEGORSG;
                            continue;
                        }
                        line = "       " + line;
                    }
                    break;
                case Stages.OCCURS:
                    m = reHeaders.Match(line);
                    if (m.Success)
                    {
                        line = "";
                        if (!headers_passed)
                        {
                            TAGpos = m.Groups[1].Captures[0].Index + 7;
                            NAMEpos = m.Groups[2].Captures[0].Index + 7;
                            Spos = m.Groups[3].Captures[0].Index + 7;
                            Rpos = m.Groups[4].Captures[0].Index + 7;
                            headers_passed = true;
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
                        if (headers_passed && !"".Equals(line.Trim()))
                        {
                            if (line.Length > TAGpos - 5 && line[TAGpos - 7] != ' ' && line[TAGpos - 6] != ' ' && line[TAGpos - 5] != ' ')
                            {
                                if (reUNT.IsMatch(line))
                                {
                                    stage = Stages.DONE;
                                }
                                m = reOccSeg.Match(line);
                                if (m.Success)
                                {
                                    cnt++;
                                    line = string.Format(
                                        "{0:0000}   {1}{2," +
                                        (NAMEpos - TAGpos - m.Groups[1].Captures[0].Length + m.Groups[2].Captures[0].Length) +
                                        "}{3," +
                                        (Spos - NAMEpos - m.Groups[2].Captures[0].Length + m.Groups[3].Captures[0].Length) +
                                        "}{4," +
                                        (Rpos - Spos - m.Groups[3].Captures[0].Length + m.Groups[4].Captures[0].Length) +
                                        "}",
                                        cnt * 10,
                                        m.Groups[1].Captures[0].Value,
                                        m.Groups[2].Captures[0].Value,
                                        m.Groups[3].Captures[0].Value,
                                        m.Groups[4].Captures[0].Value
                                    );
                                }
                                else
                                {
                                    m = reOccSG.Match(line);
                                    if (m.Success)
                                    {
                                        cnt++;
                                        string sg = "--- Segment group " + m.Groups[1].Captures[0].Value + " ---";
                                        line = string.Format(
                                            "{0:0000}   {1," +
                                            (NAMEpos - TAGpos + sg.Length) +
                                            "}{2," +
                                            (Spos - NAMEpos - sg.Length + m.Groups[2].Captures[0].Length) +
                                            "}{3," +
                                            (Rpos - Spos - m.Groups[2].Captures[0].Length + m.Groups[3].Captures[0].Length) +
                                            "}",
                                            cnt * 10,
                                            sg,
                                            m.Groups[2].Captures[0].Value,
                                            m.Groups[3].Captures[0].Value
                                        );
                                    }
                                }
                            }
                            else
                            {
                                can_write = false;
                            }
                        }
                    }
                    if (headers_passed && "".Equals(line.Trim()))
                    {
                        can_write = false;
                    }
                    break;
            }
            break;
        }
        if (can_write)
        {
            tw.WriteLine(line);
        }
    }

}
