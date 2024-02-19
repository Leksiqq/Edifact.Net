namespace Net.Leksi.Edifact;

public class InterchangeEventArgs: BaseEventArgs
{
    public string syntax_identifier = null;
    public string syntax_version_number = null;
    public string sender_identification = null;
    public string sender_identification_code_qualifier = null;
    public string address_for_reverse_routing = null;
    public string recipient_identification = null;
    public string recipient_identification_code_qualifier = null;
    public string routing_address = null;
    public string recipient_reference_or_password = null;
    public string recipient_reference_or_password_qualifier = null;
    public string application_reference = null;
    public string processing_priority_code = null;
    public bool acknowledgement_request = false;
    public string communication_agreement_id = null;
    public bool test_indicator = false;
}
