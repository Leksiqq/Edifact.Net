namespace Net.Leksi.Edifact;

public class BaseEventArgs : EventArgs
{
    public int control_count = 0;
    public string control_reference = null;
    public DateTime preparation_datetime = DateTime.MinValue;
    public List<string> tmp_message_files = null;
    public List<ParseError> errors = null;
}
