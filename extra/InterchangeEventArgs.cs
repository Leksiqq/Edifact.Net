namespace Net.Leksi.Edifact;

internal class InterchangeEventArgs: BaseEventArgs
{
    internal string? SyntaxIdentifier { get; set; }
    internal string? SyntaxVersionNumber { get; set; }
    internal string? SenderIdentification { get; set; }
    internal string? SenderIdentificationCodeQualifier { get; set; }
    internal string? AddressForReverseRouting { get; set; }
    internal string? RecipientIdentification { get; set; }
    internal string? RecipientIdentificationCodeQualifier { get; set; }
    internal string? RoutingAddress { get; set; }
    internal string? RecipientReferenceOrPassword { get; set; }
    internal string? RecipientReferenceOrPasswordQualifier { get; set; }
    internal string? ApplicationReference { get; set; }
    internal string? ProcessingPriorityCode { get; set; }
    internal string? CommunicationAgreementId { get; set; }
    internal bool AcknowledgementRequest { get; set; } = false;
    internal bool TestIndicator { get; set; } = false;
}
