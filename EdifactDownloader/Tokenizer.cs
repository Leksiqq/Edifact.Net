using System.Text;
namespace Net.Leksi.Edifact;

public class Tokenizer
{
    const int BUF_LEN = 1024;

    public delegate void Segment(object sender, SegmentEventArgs e);
    public event Segment OnSegment;

    byte component_data_element_separator;
    byte segment_tag_and_data_element_separator;
    byte decimal_mark;
    byte release_character;
    byte segment_terminator;
    Encoding encoding = Encoding.ASCII;

    SegmentEventArgs segment_ea;
    LocatedString tag, sub_elem;
    Location location = new Location();
    int format_width;
    int element_position;
    StringBuilder sb = new StringBuilder();
    List<LocatedString> element = new List<LocatedString>();
    protected List<ParseError> errors = new List<ParseError>();

    public bool HasErrors
    {
        get
        {
            return (errors.Count > 0);
        }
    }

    public ParseError[] Errors
    {
        get
        {
            ParseError[] res = new ParseError[errors.Count];
            errors.CopyTo(res);
            return res;
        }
    }

    protected ParseError add_error(ErrorTypes type, ErrorKinds kind)
    {
        errors.Add(new ParseError(type, kind));
        return errors[errors.Count - 1];
    }

    public int FormatWidth
    {
        get
        {
            return format_width;
        }
        set
        {
            format_width = value;
        }
    }

    protected Tokenizer()
    {
        OnSegment += new Segment(delegate(object sender, SegmentEventArgs e) { });
    }

    void init()
    {
        component_data_element_separator = (byte)':';
        segment_tag_and_data_element_separator = (byte)'+';
        decimal_mark = (byte)'.';
        release_character = (byte)'?';
        segment_terminator = (byte)'\'';
        segment_ea = null;
        element_position = 0;
    }

    protected void Tokenize(string path)
    {
        FileStream fs = new FileStream(path, FileMode.Open);
        Tokenize(fs);
    }

    protected void Tokenize(Stream reader)
    {
        init();
        byte[] buf = new byte[BUF_LEN];
        int n = reader.Read(buf, 0, 3);
        if (n < 3)
        {
            add_error(ErrorTypes.FATAL, ErrorKinds.INVALID_FORMAT);
            return;
        }
        char[] ch = new char[1];
        char new_line = '\0';
        if (buf[0] == 'U' && buf[1] == 'N' && buf[2] == 'A')
        {
            n = reader.Read(buf, 3, 6);
            if (n < 6)
            {
                add_error(ErrorTypes.FATAL, ErrorKinds.INVALID_FORMAT);
                return;
            }
            component_data_element_separator = buf[3];
            segment_tag_and_data_element_separator = buf[4];
            decimal_mark = buf[5];
            release_character = buf[6];
            segment_terminator = buf[8];
            location.offset = 0;
            location.line = 1;
            location.col = 0;
            for (int pos = 0; pos < 9; pos++)
            {
                ch[0] = (char)buf[pos];
                if (ch[0] == '\n' || ch[0] == '\r')
                {
                    if (new_line == 0)
                    {
                        new_line = ch[0];
                    }
                    if (new_line == ch[0])
                    {
                        location.col = 0;
                        location.line++;
                    }
                }
                location.col++;
                location.offset++;
            }
        }
        n = reader.Read(buf, 0, BUF_LEN);
        bool release = false;
        do
        {
            for (int pos = 0; pos < n; pos++)
            {
                ch[0] = (char)buf[pos];
                if (ch[0] == '\n' || ch[0] == '\r')
                {
                    if (new_line == 0)
                    {
                        new_line = ch[0];
                    }
                    if (new_line == ch[0])
                    {
                        if (format_width > 0 && location.col < format_width)
                        {
                            ch[0] = ' ';
                        }
                        location.col = 0;
                        location.line++;
                    }
                    if (ch[0] != ' ')
                    {
                        continue;
                    }
                }
                location.col++;
                if (release)
                {
                    sb.Append(ch);
                    release = false;
                }
                else
                {
                    if (buf[pos] == release_character)
                    {
                        release = true;
                    }
                    else if (buf[pos] == segment_tag_and_data_element_separator)
                    {
                        if (tag.data == null)
                        {
                            tag.data = sb.ToString();
                            sb.Clear();
                            do_begin_tag();
                            element_position = 0;
                        }
                        else
                        {
                            if (sub_elem == null)
                            {
                                sub_elem = new LocatedString(location, null);
                            }
                            sub_elem.end.Set(location);
                            sub_elem.data = sb.ToString();
                            element.Add(sub_elem);
                            sub_elem = null;
                            sb.Clear();
                            do_element();
                            element.Clear();
                        }
                    }
                    else if (buf[pos] == component_data_element_separator)
                    {
                        if (sub_elem == null)
                        {
                            sub_elem = new LocatedString(location, null);
                        }
                        sub_elem.end.Set(location);
                        sub_elem.data = sb.ToString();
                        element.Add(sub_elem);
                        sub_elem = null;
                        sb.Clear();
                    }
                    else if (buf[pos] == segment_terminator)
                    {
                        tag.end.Set(location);
                        if (tag.data == null)
                        {
                            tag.data = sb.ToString();
                            sb.Clear();
                            do_begin_tag();
                            do_end_tag();
                            tag = null;
                        }
                        else
                        {
                            if (sub_elem == null)
                            {
                                sub_elem = new LocatedString(location, null);
                            }
                            sub_elem.end.Set(location);
                            sub_elem.data = sb.ToString();
                            element.Add(sub_elem);
                            sub_elem = null;
                            sb.Clear();
                            do_element();
                            element.Clear();
                            do_end_tag();
                            tag = null;
                        }
                    }
                    else
                    {
                        if (sb.Length == 0)
                        {
                            if (tag == null)
                            {
                                if (!char.IsWhiteSpace(ch[0]))
                                {
                                    tag = new LocatedString(location, null);
                                    sb.Append(ch[0]);
                                }
                            }
                            else
                            {
                                sub_elem = new LocatedString(location, null);
                                sb.Append(ch[0]);
                            }
                        }
                        else
                        {
                            if (tag.data != null || !char.IsWhiteSpace(ch[0]))
                            {
                                sb.Append(ch[0]);
                            }
                        }
                    }
                }
                location.offset++;
            }
        }
        while ((n = reader.Read(buf, 0, BUF_LEN)) > 0);
    }

    void do_element()
    {
        List<LocatedString> ss = new List<LocatedString>();
        for (int i = 0; i < element.Count; i++)
        {
            ss.Add(element[i]);
        }
        segment_ea.elements.Add(ss);
        element_position++;
    }

    void do_begin_tag()
    {
        segment_ea = new SegmentEventArgs();
        segment_ea.tag = tag;
    }

    void do_end_tag()
    {
        if (OnSegment != null)
        {
            OnSegment(this, segment_ea);
        }
        segment_ea = null;
    }

}
