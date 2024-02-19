namespace Net.Leksi.Edifact;

public class FunctionalGroupEventArgs : BaseEventArgs
{
    public string identification = null;
    public string sender_identification = null;
    public string sender_identification_code_qualifier = null;
    public string recipient_identification = null;
    public string recipient_identification_code_qualifier = null;
    public string controlling_agency = null;
    public string message_version = null;
    public string message_release = null;
    public string application_password = null;
    public string association_assigned_code = null;
    public InterchangeEventArgs interchange = null;
}
