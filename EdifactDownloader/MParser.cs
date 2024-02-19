using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public class MParser
{
    public delegate void EndMessage();
    public delegate void BeginSG(int num, string desc);
    public delegate void EndSG(int num);
    public delegate void Segment(string name, string info, string desc, int sg);
    public delegate void Occurs(int sgnum, string segment, int min_occurs, int max_occurs);

    public event EndMessage OnEndMessage;
    public event BeginSG OnBeginSG;
    public event EndSG OnEndSG;
    public event Segment OnSegment;
    public event Occurs OnOccurs;

    enum Stage { NONE, SEGORSG, SG_REST, DESC, OCCURS, DONE };
    Stage stage = Stage.NONE;
    Regex reSegment = new Regex("^\\d{4}\\s+([-\\*+#|X]*)\\s*(\\w[\\w\\d]*)\\s*,?\\s*(.*)$");
    Regex reSG = new Regex("^\\d{4}\\s+([-\\*+#|X]*)\\s*Segment\\s+group\\s+(\\d+)\\s*:\\s*(?:(\\w[\\w\\d]*)\\s*-\\s*)*(\\w[\\w\\d]*)?[\\s.]*$");
    Regex reSGRest = new Regex("^\\s+(?:(\\w[\\w\\d]*)\\s*-\\s*)*(\\w[\\w\\d]*)?[\\s.]*$");
    Regex reOccSegment = null;
    Regex reOccSG = null;
    Regex reS = null;
    Regex reR = null;
    Regex reHeaders = new Regex("^\\s*(\\d{4})\\s+(UNH)\\s+(Message header)\\s+(M)\\s+(1)\\s*$", RegexOptions.IgnoreCase);
    string segment = null;
    string info = null;
    string desc = null;
    int sgnum = -1;
    int node_pos = -1;
    int node_off = 0;
    bool deleted = false;
    List<int> sg_stack = new List<int>();
    int line_number = 0;

    class Node
    {
        internal string name;
        internal int sgnum;
        internal List<int> sg_end = new List<int>();
        internal Node(string name, int sgnum)
        {
            this.name = name;
            this.sgnum = sgnum;
        }
    }

    List<Node> nodes = new List<Node>();

    public int LineNumber
    {
        get
        {
            return line_number;
        }
    }

    public MParser()
    {
        OnEndMessage += delegate() { };
        OnBeginSG += delegate(int num, string desc) { };
        OnEndSG += delegate(int num) { };
        OnSegment += delegate(string name, string info, string desc, int sg) { };
        OnOccurs += delegate(int sgnum, string segment, int min_occurs, int max_occurs) { };
    }

    public void Run(string[] data)
    {
        stage = Stage.SEGORSG;
        foreach (string line in data)
        {
            line_number++;
            on_line(line);
        }
    }

    private void on_line(string line)
    {
        Match m;
        while (true)
        {
            switch (stage)
            {
                case Stage.SEGORSG:
                    if (!"".Equals(line.Trim()))
                    {
                        m = reSG.Match(line);
                        if (m.Success)
                        {
                            //Console.WriteLine(line);
                            seg_or_sg_event();
                            sgnum = int.Parse(m.Groups[2].Captures[0].Value.Trim());
                            if (m.Groups[1].Captures[0].Value.Contains("-"))
                            {
                                deleted = true;
                            }
                            if (!deleted)
                            {
                                node_pos++;
                                //Console.WriteLine(node_pos + "/" + nodes.Count);
                                if (node_pos >= nodes.Count)
                                {
                                    nodes.Add(new Node("SG" + sgnum, 0));
                                }
                                else
                                {
                                    check_nodes_order();
                                }
                                node_off = node_pos;
                                foreach (Capture c in m.Groups[3].Captures)
                                {
                                    nodes.Insert(++node_off, new Node(c.Value.Trim(), sgnum));
                                }
                            }
                            if (m.Groups[4].Captures.Count > 0)
                            {
                                if (!deleted)
                                {
                                    nodes.Insert(++node_off, new Node(m.Groups[4].Captures[0].Value.Trim(), sgnum));
                                    if (nodes[node_pos].sg_end.Count > 0)
                                    {
                                        nodes[node_off].sg_end.AddRange(nodes[node_pos].sg_end);
                                    }
                                    nodes[node_off].sg_end.Add(sgnum);
                                }
                                stage = Stage.DESC;
                                desc = "";
                            }
                            else
                            {
                                stage = Stage.SG_REST;
                            }
                        }
                        else
                        {
                            m = reSegment.Match(line);
                            if (m.Success)
                            {
                                //Console.WriteLine(line);
                                seg_or_sg_event();
                                segment = m.Groups[2].Captures[0].Value.Trim();
                                info = m.Groups[3].Captures[0].Value.Trim();
                                if ("UNT".Equals(segment))
                                {
                                    //foreach (Node n in nodes)
                                    //{
                                    //    Console.WriteLine(n.name);
                                    //}
                                    stage = Stage.OCCURS;
                                    node_pos = 0;
                                    sgnum = 0;
                                    OnEndMessage();
                                }
                                else
                                {
                                    if (m.Groups[1].Captures[0].Value.Contains("-"))
                                    {
                                        deleted = true;
                                    }
                                    if (!deleted && !"UNH".Equals(segment))
                                    {
                                        node_pos++;
                                        //Console.WriteLine(node_pos + "/" + nodes.Count);
                                        if (node_pos >= nodes.Count)
                                        {
                                            nodes.Add(new Node(segment, 0));
                                        }
                                        else
                                        {
                                            check_nodes_order();
                                        }
                                    }
                                    stage = Stage.DESC;
                                    desc = "";
                                }
                            }
                            else
                            {
                                stage = Stage.DESC;
                                continue;
                            }
                        }
                    }
                    break;
                case Stage.DESC:
                    if ("".Equals(line.Trim()))
                    {
                        stage = Stage.SEGORSG;
                    }
                    else
                    {
                        desc += " " + line.Trim();
                    }
                    break;
                case Stage.SG_REST:
                    m = reSGRest.Match(line);
                    if (m.Success)
                    {
                        //Console.WriteLine(line);
                        if (!deleted)
                        {
                            foreach (Capture c in m.Groups[1].Captures)
                            {
                                nodes.Insert(++node_off, new Node(c.Value.Trim(), sgnum));
                            }
                        }
                        if (m.Groups[2].Captures.Count > 0)
                        {
                            if (!deleted)
                            {
                                nodes.Insert(++node_off, new Node(m.Groups[2].Captures[0].Value.Trim(), sgnum));
                                if (nodes[node_pos].sg_end.Count > 0)
                                {
                                    nodes[node_off].sg_end.AddRange(nodes[node_pos].sg_end);
                                }
                                nodes[node_off].sg_end.Add(sgnum);
                            }
                            stage = Stage.DESC;
                            desc = "";
                        }
                    }
                    break;
                case Stage.OCCURS:
                    if (!"".Equals(line.Trim()))
                    {
                        if (reOccSegment == null)
                        {
                            m = reHeaders.Match(line);
                            if (m.Success)
                            {
                                int pos = m.Groups[1].Captures[0].Index;
                                int tag = m.Groups[2].Captures[0].Index;
                                int name = m.Groups[3].Captures[0].Index;
                                int s = m.Groups[4].Captures[0].Index;
                                int r = m.Groups[5].Captures[0].Index;
                                reOccSegment = new Regex("^.{" + pos.ToString() + "}\\d{4}([-\\s\\*+#|X]{" + (tag - pos - 4).ToString() + "})(\\w[\\w\\d]*)\\s.*$");
                                reOccSG = new Regex("^.{" + pos.ToString() + "}\\d{4}([-\\s\\*+#|X]{" + (name - pos - 4).ToString() + "}).*?Segment group (\\d+).*$");
                                reS = new Regex("^.{" + s.ToString() + "}([CM]).*$");
                                reR = new Regex("^.{" + r.ToString() + "}(\\d+).*$");
                            }
                        }
                        else
                        {
                            bool found = false;
                            segment = null;
                            sgnum = 0;
                            m = reOccSegment.Match(line);
                            if (m.Success)
                            {
                                //Console.WriteLine(line);
                                if (!m.Groups[1].Captures[0].Value.Trim().Contains("-"))
                                {
                                    segment = m.Groups[2].Captures[0].Value.Trim();
                                    if (!"UNH".Equals(segment) && !"UNT".Equals(segment))
                                    {
                                        //Console.WriteLine("segment: " + name);
                                        if (!nodes[node_pos].name.Equals(segment))
                                        {
                                            throw new Exception(nodes[node_pos].name + " <> " + segment);
                                        }
                                        found = true;
                                        sgnum = nodes[node_pos].sgnum;
                                    }
                                }
                                if ("UNT".Equals(segment))
                                {
                                    stage = Stage.DONE;
                                }
                            }
                            else
                            {
                                m = reOccSG.Match(line);
                                if (m.Success)
                                {
                                    //Console.WriteLine(line);
                                    if (!m.Groups[1].Captures[0].Value.Trim().Contains("-"))
                                    {
                                        sgnum = int.Parse(m.Groups[2].Captures[0].Value.Trim());
                                        string name = "SG" + sgnum.ToString();
                                        //Console.WriteLine("sg: " + name);
                                        if (!nodes[node_pos].name.Equals(name))
                                        {
                                            throw new Exception(nodes[node_pos].name + " <> " + name);
                                        }
                                        found = true;
                                    }
                                }
                            }
                            if (found)
                            {
                                int min_occurs = 1;
                                int max_occurs = 1;
                                m = reS.Match(line);
                                if (m.Success)
                                {
                                    if ("C".Equals(m.Groups[1].Captures[0].Value.Trim()))
                                    {
                                        min_occurs = 0;
                                    }
                                }
                                m = reR.Match(line);
                                if (m.Success)
                                {
                                    max_occurs = int.Parse(m.Groups[1].Captures[0].Value.Trim());
                                }
                                OnOccurs(sgnum, segment, min_occurs, max_occurs);
                                node_pos++;
                                //Console.WriteLine(node_pos + "/" + nodes.Count);
                            }
                        }
                    }
                    break;
            }
            break;
        }
    }

    private void check_nodes_order()
    {
        if (!nodes[node_pos].name.Equals(sgnum == -1 ? segment : "SG" + sgnum))
        {
            throw new Exception((sgnum == -1 ? segment : "SG" + sgnum) + " <> " + nodes[node_pos].name);
        }
    }

    private void check_end_sg()
    {
        for (int i = nodes[node_pos].sg_end.Count - 1; i >= 0; i--)
        {
            OnEndSG(nodes[node_pos].sg_end[i]);
            sg_stack.RemoveAt(sg_stack.Count - 1);
        }
    }

    private void seg_or_sg_event()
    {
        if (sgnum != -1 || (segment != null && !"UNH".Equals(segment)))
        {
            if (!deleted)
            {
                if (sgnum != -1)
                {
                    sg_stack.Add(sgnum);
                    OnBeginSG(sgnum, desc);
                }
                else if (segment != null && !"UNH".Equals(segment))
                {
                    OnSegment(segment, info, desc, sg_stack.Count > 0 ? sg_stack[sg_stack.Count - 1] : 0);
                    check_end_sg();
                }
            }
            sgnum = -1;
            segment = null;
            deleted = false;
        }
    }
}
