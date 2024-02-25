﻿namespace Net.Leksi.Edifact;

public partial class EdifactDownloader1: IDownloader
{
    private const string s_annotation = "annotation";
    private const string s_annotationPrefixDeclaration = "xmlns:an";
    private const string s_args = "args";
    private const string s_change = "change";
    private const string s_cmd = "cmd";
    private const string s_commentsXPath = "/comment()[1]";
    private const string s_complexType = "complexType";
    private const string s_d16a = "D16A";
    private const string s_d20b = "D20B";
    private const string s_d9 = "D9";
    private const string s_description = "description";
    private const string s_directoryFormat = "D{0:00}{1}";
    private const string s_directoryNotFound = "DIRECTORY_NOT_FOUND";
    private const string s_documentation = "documentation";
    private const string s_edifact = "edifact";
    private const string s_edifactXsd = "edifact.xsd";
    private const string s_failedUnzip = "FAILED_UNZIP";
    private const string s_fileNameFormat = "{0}.{1}";
    private const string s_length = "length";
    private const string s_logMessage = "{message}";
    private const string s_macosx = "__MACOSX";
    private const string s_maxLength = "maxLength";
    private const string s_minLength = "minLength";
    private const string s_minOccursPatternFormat = "{0},";
    private const string s_minus = "-";
    private const string s_n = "n";
    private const string s_name = "name";
    private const string s_note = "note";
    private const string s_noSimpleTypesFound = "NO_SIMPLE_TYPES_FOUND";
    private const string s_numberTypePatternFormat = "^-?([0-9]\\.?){{{0}{1}}}[0-9]$";
    private const string s_numberTypePattern = "^-?[^.]*\\.?[^.]+$";
    private const string s_receivingDirectory = "RECEIVING_DIRECTORY";
    private const string s_renameElementFormat = "E{0}";
    private const string s_restriction = "restriction";
    private const string s_rmLabelsName = "Net.Leksi.Edifact.Properties.labels";
    private const string s_path1 = "/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_path2 = "/fileadmin/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_parentXPath = "..";
    private const string s_pattern = "pattern";
    private const string s_schema = "schema";
    private const string s_simpleContent = "simpleContent";
    private const string s_simpleTypes = "simpletypes";
    private const string s_simpleTypesXsd = "simpletypes.xsd";
    private const string s_sourceArchiveDir = "--source";
    private const string s_types = "types";
    private const string s_typesXsd = "types.xsd";
    private const string s_unMessageFormat = " UN/{0} ";
    private const string s_uriFormat = "{0}{1}";
    private const string s_usingExternalUnzip = "USING_EXTERNAL_UNZIP";
    private const string s_value = "value";
    private const string s_webSite = "https://unece.org";
    private const string s_webSite1 = "https://www.unece.org";
    private const string s_xsPrefix = "xs";
    private const string s_zipPattern = "*.zip";
}
