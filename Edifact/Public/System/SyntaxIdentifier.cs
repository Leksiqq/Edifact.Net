﻿namespace Net.Leksi.Edifact;

public class SyntaxIdentifier
{
    public string Identifier { get; set; } = null!;
    public string VersionNumber { get; set; } = null!;
    public string? ServiceCodeListDirectoryVersionNumber { get; set; }
    public string? CharacterEncodingCoded { get; set; }

}
