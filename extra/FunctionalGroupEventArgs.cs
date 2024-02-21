namespace Net.Leksi.Edifact;

internal class FunctionalGroupEventArgs : BaseEventArgs
{
    internal string? Identification { get; set; }
    internal string? SenderIdentification { get; set; }
    internal string? SenderIdentificationCodeQualifier { get; set; }
    internal string? RecipientIdentification { get; set; }
    internal string? RecipientIdentificationCodeQualifier { get; set; }
    internal string? ControllingAgency { get; set; }
    internal string? MessageVersion { get; set; }
    internal string? MessageRelease { get; set; }
    internal string? ApplicationPassword { get; set; }
    internal string? AssociationAssignedCode { get; set; }
    internal InterchangeEventArgs? Interchange { get; set; }
}
