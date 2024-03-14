﻿using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

internal static class Constants
{
    internal const string s_0 = "0";
    internal const string s_1 = "1";
    internal const string s_a = "a";
    internal const string s_agensy = "agency";
    internal const string s_allElementsCountXPath = "count(e:*)";
    internal const string s_allElementsXPath = "//e:*";
    internal const string s_annotation = "annotation";
    internal const string s_annotationPrefixDeclaration = "xmlns:an";
    internal const string s_args = "args";
    internal const string s_askKey = "/?";
    internal const string s_askSign = "?";
    internal const string s_asterisk = "*";
    internal const string s_attr = "attr";
    internal const string s_base = "base";
    internal const string s_baseSegment = "BASE-SEGMENT";
    internal const string s_batchInterchange = "batch_interchange";
    internal const string s_batchInterchange1 = "BATCH_INTERCHANGE";
    internal const string s_batchInterchangeXsd = "batch_interchange.xsd";
    internal const string s_bufferSize = "buffer-size";
    internal const string s_c = "c";
    internal const string s_change = "change";
    internal const string s_children = "children";
    internal const string s_cmd = "cmd";
    internal const string s_code = "code";
    internal const string s_commentsXPath = "/comment()[1]";
    internal const string s_complexContent = "complexContent";
    internal const string s_complexType = "complexType";
    internal const string s_composites = "composites";
    internal const string s_compositesXsd = "composites.xsd";
    internal const string s_d = "D";
    internal const string s_d00 = "D00";
    internal const string s_d0036 = "D0036";
    internal const string s_d0051XPath = "e:D0051";
    internal const string s_d0052XPath = "e:D0052";
    internal const string s_d0054XPath = "e:D0054";
    internal const string s_d0062 = "D0062";
    internal const string s_d0065XPath = "e:D0065";
    internal const string s_d0074XPathFormat = "/e:{0}/e:D0074";
    internal const string s_d01c = "D01C";
    internal const string s_d0340 = "D0340";
    internal const string s_d16a = "D16A";
    internal const string s_d20b = "D20B";
    internal const string s_d79 = "D79";
    internal const string s_d8 = "D8";
    internal const string s_d99z = "D99Z";
    internal const string s_dataElementNotFound = "DATA_ELEMENT_NOT_FOUND";
    internal const string s_description = "description";
    internal const string s_directoryFormat = "D{0:00}{1}";
    internal const string s_directoryNotFound = "DIRECTORY_NOT_FOUND";
    internal const string s_doctype = "<!DOCTYPE";
    internal const string s_documentation = "documentation";
    internal const string s_edcd = "EDCD";
    internal const string s_eded = "EDED";
    internal const string s_edifactDownloaderUsage = "EDIFACT_DOWNLOADER_USAGE";
    internal const string s_edifact = "edifact";
    internal const string s_edifactXsd = "edifact.xsd";
    internal const string s_edsd = "EDSD";
    internal const string s_element = "element";
    internal const string s_elements = "elements";
    internal const string s_elementsXsd = "elements.xsd";
    internal const string s_encoding = "encoding";
    internal const string s_enumeration = "enumeration";
    internal const string s_extension = "extension";
    internal const string s_failedUnzip = "FAILED_UNZIP";
    internal const string s_file = "file";
    internal const string s_fileNameFormat = "{0}.{1}";
    internal const string s_xsdFileWithSuffuxFormat = "{0}{1}.xsd";
    internal const string s_finish = "finish";
    internal const string s_folderUriFormat = "{0}/_";
    internal const string s_function = "function";
    internal const string s_g = "g";
    internal const string s_helpKey = "--help";
    internal const string s_idcd = "IDCD";
    internal const string s_idsd = "IDSD";
    internal const string s_input = "input";
    internal const string s_interactiveInterchange = "interactive_interchange";
    internal const string s_interactiveInterchange1 = "INTERACTIVE_INTERCHANGE";
    internal const string s_interactiveInterchangeXsd = "interactive_interchange.xsd";
    internal const string s_interval = "interval";
    internal const string s_invalidDirectoryNameOrInterval = "INVALID_DIRECTORY_NAME_OR_INTERVAL";
    internal const string s_invalidSequence = "INVALID_SEQUENCE";
    internal const string s_keyAlreadyUsed = "KEY_ALREADY_USED";
    internal const string s_length = "length";
    internal const string s_loadFixedFile = "LOAD_FIXED_FILE";
    internal const string s_logMessage = "{message}";
    internal const string s_m = "M";
    internal const string s_macosx = "__MACOSX";
    internal const string s_maxLength = "maxLength";
    internal const string s_maxOccurs = "maxOccurs";
    internal const string s_message = "message";
    internal const string s_message1 = "MESSAGE";
    internal const string s_messageNotFound = "MESSAGE_NOT_FOUND";
    internal const string s_messagesPatternFormat = "{0}_D.{1}";
    internal const string s_messageTypeAndVersion = @"<xs:annotation>
    <xs:appinfo>{0}{1}:{2}:{3}:{4}</xs:appinfo>  
</xs:annotation>
";
    internal const string s_messageFirstChildXpath = "/xs:schema/xs:complexType[@name='MESSAGE']/xs:*[1]";
    internal const string s_messageXsdFormat = "{0}/{1}{2}/{3}{4}.xsd";
    internal const string s_minLength = "minLength";
    internal const string s_minOccurs = "minOccurs";
    internal const string s_minOccursPatternFormat = "{0},";
    internal const string s_missedKeyValue = "MISSED_KEY_VALUE";
    internal const string s_missedMandatryKey = "MISSED_MANDATORY_KEY";
    internal const string s_n = "n";
    internal const string s_name = "name";
    internal const string s_noSimpleTypesFound = "NO_SIMPLE_TYPES_FOUND";
    internal const string s_note = "note";
    internal const string s_noteAtComposite = "Note: {0}: {1}";
    internal const string s_noSegmentsFound = "NO_SEGMENTS_FOUND";
    internal const string s_notEdifactSchema = "NOT_EDIFACT_SCHEMA";
    internal const string s_notSchema = "NOT_XML_SCHEMA";
    internal const string s_noTypesFound = "NO_TYPES_FOUND";
    internal const string s_numberTypePatternFormat = "^-?([0-9]\\.?){{{0}{1}}}[0-9]$";
    internal const string s_numberTypePattern = "^-?[^.]*\\.?[^.]+$";
    internal const string s_output = "output";
    internal const string s_parserCliUsage = "PARSER_CLI_USAGE";
    internal const string s_path1 = "/DAM/trade/untdid/{0}/{1}.zip";
    internal const string s_path2 = "/fileadmin/DAM/trade/untdid/{0}/{1}.zip";
    internal const string s_parentXPath = "..";
    internal const string s_pattern = "pattern";
    internal const string s_placeholderElement = "<root/>";
    internal const string s_position = "position";
    internal const string s_receivingDirectory = "RECEIVING_DIRECTORY";
    internal const string s_release = "release";
    internal const string s_renameElementFormat = "D{0}";
    internal const string s_replaceNsFormat = "{0}=\"{1}\"";
    internal const string s_repr = "representation";
    internal const string s_rest = "rest";
    internal const string s_restriction = "restriction";
    internal const string s_rmLabelsName = "labels";
    internal const string s_rmFixedName = "fixed";
    internal const string s_s009 = "S009";
    internal const string s_s306 = "S306";
    internal const string s_schema = "schema";
    internal const string s_schemasRoot = "schemas-root";
    internal const string s_schemaXPathFormat = "/{0}:schema";
    internal const string s_script = "script";
    internal const string s_secondLevelXPathFormat = "/e:{0}/e:{1}";
    internal const string s_segmentGroupNameFormat = "SG{0}";
    internal const string s_segments = "segments";
    internal const string s_segmentsXsd = "segments.xsd";
    internal const string s_selectStructureSequenceXPath = "//xs:sequence[@id='structure'][1]";
    internal const string s_sequence = "sequence";
    internal const string s_sharp = "#";
    internal const string s_simpleContent = "simpleContent";
    internal const string s_sourceArchiveDir = "--source";
    internal const string s_src = "src";
    internal const string s_start = "start";
    internal const string s_strict = "strict";
    internal const string s_suffix = "suffix";
    internal const string s_suffixes = "suffixes";
    internal const string s_systemComposites = "system_composites";
    internal const string s_systemCompositesXsd = "system_composites.xsd";
    internal const string s_systemElements = "system_elements";
    internal const string s_systemElementsXsd = "system_elements.xsd";
    internal const string s_systemSegments = "system_segments";
    internal const string s_systemSegmentsXsd = "system_segments.xsd";
    internal const string s_targetNamespaceXPath = "@targetNamespace";
    internal const string s_targetNamespaceXPath1 = "/xs:schema/@targetNamespace";
    internal const string s_t = "t";
    internal const string s_type = "type";
    internal const string s_typeForEnumXPathFormat = "/xs:schema/xs:complexType[@name='D{0}']/xs:simpleContent/xs:restriction";
    internal const string s_uih = "UIH";
    internal const string s_uit = "UIT";
    internal const string s_uiz = "UIZ";
    internal const string s_un = "UN";
    internal const string s_uncl = "UNCL";
    internal const string s_une = "UNE";
    internal const string s_unexpectedLine = "UNEXPECTED_LINE";
    internal const string s_uneD0048XPath = "/e:UNE/e:D0048";
    internal const string s_uneD0060XPath = "/e:UNE/e:D0060";
    internal const string s_ung = "UNG";
    internal const string s_ungD0048XPath = "/e:UNG/e:D0048";
    internal const string s_unh = "UNH";
    internal const string s_unknownKey = "UNKNOWN_KEY";
    internal const string s_unMessageFormat = " UN/{0} ";
    internal const string s_unsl99a = "UN\\D99A\\UNSL.99A";
    internal const string s_unt = "UNT";
    internal const string s_unz = "UNZ";
    internal const string s_uriFormat = "{0}{1}";
    internal const string s_uriSchemeNotSupported = "URI_SCHEME_NOT_SUPPORTED";
    internal const string s_value = "value";
    internal const string s_version = "version";
    internal const string s_webSite = "https://unece.org";
    internal const string s_webSite1 = "https://www.unece.org";
    internal const string s_xml = "xml";
    internal const string s_xsd = "xsd";
    internal const string s_xsPrefix = "xs";
    internal const string s_zip = "zip";
    internal const string s_zipPattern = "*.zip";

    internal static readonly Regex s_reSegmentGroup = new("^SG(?<code>\\d+)$");
    internal static readonly Regex s_reOriginalMessageXsd = new("^(?<message>[A-Z]{6}\\.xsd)$");

    internal static readonly ResourceManager s_rmLabels;

    static Constants()
    {
        s_rmLabels = new ResourceManager($"{typeof(Properties.Resources).Namespace}.{s_rmLabelsName}", Assembly.GetExecutingAssembly());
    }
}
