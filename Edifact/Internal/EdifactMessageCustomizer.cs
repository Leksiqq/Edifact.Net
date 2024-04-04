using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class EdifactMessageCustomizer
{
    private static readonly Regex s_reMessageIdentifier =
        new("^(?<type>[A-Z]{6}):(?<version>[^:]{1,3}):(?<release>[^:]{1,3}):(?<agency>[^:]{1,3})$");
    private static readonly Regex s_reXmlns = new("xmlns(?::[^=]+)?=\"[^\"]*\"");
    private readonly IServiceProvider _services;
    private readonly ILogger<EdifactMessageCustomizer>? _logger;
    private readonly XmlResolver _resolver;
    private readonly Dictionary<string, XmlDocument> _sourcesCache = [];
    private EdifactMessageCustomizerOptions _options = null!;
    private int _entersNum = 0;
    private XmlNameTable _nameTable = null!;
    private XmlNamespaceManager _man = null!;
    private XmlSchemaSet _schemaSet = null!;
    private string _targetNamespace = string.Empty;

    public EdifactMessageCustomizer(IServiceProvider services)
    {
        _services = services;
        _logger = _services.GetService<ILogger<EdifactMessageCustomizer>>();
        _resolver = new Resolver(_services);
    }

    public void Customize(EdifactMessageCustomizerOptions options)
    {
        try
        {
            if (Interlocked.Increment(ref _entersNum) != 1)
            {
                throw new Exception("TODO: Thread unsafety.");
            }
            _options = options;
            if (string.IsNullOrEmpty(_options.SchemasUri))
            {
                throw new Exception($"TODO: --{s_schemasRoot} is mandatary.");
            }
            if (string.IsNullOrEmpty(_options.ScriptUri))
            {
                throw new Exception($"TODO: --{s_script} is mandatary.");
            }
            _sourcesCache.Clear();
            _targetNamespace = string.Empty;
            _nameTable = new NameTable();
            _man = new(_nameTable);
            _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
            _man.AddNamespace(s_euPrefix, Properties.Resources.edifact_utility_ns);
            _man.AddNamespace("s", Properties.Resources.edifact_script_ns);

            _schemaSet = new()
            {
                XmlResolver = _resolver,
            };
            _schemaSet.ValidationEventHandler += _schemaSet_ValidationEventHandler;

            XmlDocument script = new();
            Uri scriptUri = new(_options.ScriptUri);
            using (Stream s = (Stream)_resolver.GetEntity(scriptUri, null, typeof(Stream))!)
            {
                script.Load(s);
            }

            string messageIdentifier = script.CreateNavigator()!.SelectSingleNode(s_scriptMessageIdentifierXpath, _man)?.Value
                ?? throw new Exception($"TODO: File is not a script: {scriptUri}"); ;
            string suffix = script.CreateNavigator()!.SelectSingleNode(s_scriptMessageSuffixXpath, _man)?.Value
                ?? throw new Exception($"TODO: File is not a script: {scriptUri}"); ;

            Match match = s_reMessageIdentifier.Match(messageIdentifier);
            if (!match.Success)
            {
                throw new Exception($"TODO: Not a message identifier: {messageIdentifier}.");
            }
            Uri inputUri = new(
                new Uri(string.Format(s_folderUriFormat, _options.SchemasUri)),
                string.Format(
                    s_fileInDirectoryXsdFormat,
                    match.Groups[s_agensy].Value,
                    match.Groups[s_version].Value,
                    match.Groups[s_release].Value,
                    match.Groups[s_type].Value,
                    string.Empty
                )
            );

            XmlDocument doc = new(_nameTable);
            using (Stream s = (Stream)_resolver.GetEntity(inputUri, null, typeof(Stream))!)
            {
                doc.Load(s);
            }

            XPathNavigator nav = doc.CreateNavigator()!;
            if (nav.SelectSingleNode(s_targetNamespaceXPath1, _man) is not XPathNavigator tns)
            {
                throw new Exception("TODO: not schema.");
            }
            _targetNamespace = tns.Value;
            _schemaSet.Add(
                Properties.Resources.edifact_utility_ns,
                new Uri(
                    new Uri(
                        string.Format(s_folderUriFormat, _options.SchemasUri)
                    ),
                    s_utilityXsd
                ).ToString()
            );
            _schemaSet.Add(_targetNamespace, inputUri.ToString());
            _schemaSet.Add(
                Properties.Resources.edifact_script_ns,
                _options.ScriptUri
            );

            _schemaSet.Compile();
            XPathNavigator nav1 = nav.SelectSingleNode(string.Format(s_appinfoXPathFormat, s_messageIdentifier), _man)!;
            nav1.MoveToParent();
            nav1.AppendChild(string.Format(s_suffixAppInfoFormat, suffix));

            XPathNodeIterator scriptOperationsNI = script.CreateNavigator()!.Select("/xs:schema/xs:element", _man);
            while (scriptOperationsNI.MoveNext())
            {
                string name = scriptOperationsNI.Current!.GetAttribute("name", string.Empty);
                if (s_reSegmentGroup.IsMatch(name))
                {
                    if (doc.CreateNavigator()!
                        .SelectSingleNode(
                            string.Format("//xs:element[@name='{0}']", name), _man
                        ) is XPathNavigator contextNav
                    )
                    {
                        if (scriptOperationsNI.Current!.SelectSingleNode("@eu:action", _man) is XPathNavigator actionNav)
                        {
                            if (actionNav.Value == "remove")
                            {
                                contextNav.DeleteSelf();
                            }
                            else
                            {
                                ThrowUnexpectedAction(actionNav.Value, scriptOperationsNI.Current!);
                            }
                        }
                        else
                        {
                            ProcessOccurs(contextNav, scriptOperationsNI.Current!);
                            contextNav = contextNav.SelectSingleNode("xs:complexType", _man)!;
                            XPathNodeIterator segmentsNI = scriptOperationsNI.Current!.Select("xs:complexType/xs:sequence/xs:element", _man);
                            while (segmentsNI.MoveNext())
                            {
                                string name1 = segmentsNI.Current!.GetAttribute("name", string.Empty);
                                if (!s_reSegmentGroup.IsMatch(name1!))
                                {
                                    ProcessSegment(contextNav, segmentsNI.Current!);
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"TODO: Segment group '{name}' not found.");
                    }
                }
                else
                {
                    if (doc.CreateNavigator()!
                                .SelectSingleNode("/xs:schema/xs:complexType[@name='MESSAGE']/xs:complexContent/xs:extension", _man) is XPathNavigator contextNav)
                    {
                        XPathNavigator scriptCurrentNav = scriptOperationsNI.Current!;
                        ProcessSegment(contextNav, scriptCurrentNav);
                    }
                }
            }

            scriptOperationsNI = script.CreateNavigator()!.Select("/xs:schema/xs:complexType", _man);
            while (scriptOperationsNI.MoveNext())
            {
                string name = scriptOperationsNI.Current!.GetAttribute("name", string.Empty);
                if (GetSchemaSource(name, _targetNamespace) is XmlDocument source)
                {
                    if (scriptOperationsNI.Current!.SelectSingleNode("xs:simpleContent/xs:restriction[@base='eu:D']", _man) is { })
                    {
                        CustomizeRestriction(doc, scriptOperationsNI, name, source);
                    }
                }
                else
                {
                    throw new Exception($"TODO: Type {name} not found.");
                }
            }



            Uri outputUri = new(
                new Uri(string.Format(s_folderUriFormat, _options.SchemasUri)),
                string.Format(
                    s_fileInDirectoryXsdFormat,
                    match.Groups[s_agensy].Value,
                    match.Groups[s_version].Value,
                    match.Groups[s_release].Value,
                    match.Groups[s_type].Value,
                    suffix
                )
            );

            IStreamFactory streamFactory = _services.GetRequiredKeyedService<IStreamFactory>(outputUri.Scheme);
            using Stream output = streamFactory.GetOutputStream(outputUri, FileMode.Create);
            using XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings
            {
                Indent = true,
            });
            doc.WriteTo(writer);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, string.Empty);
        }
        finally
        {
            Interlocked.Decrement(ref _entersNum);
        }
    }

    private static void ProcessOccurs(XPathNavigator originalElementNav, XPathNavigator scriptCurrentNav)
    {
        foreach(string occurs in new string[] { s_minOccurs, s_maxOccurs })
        {
            if (scriptCurrentNav.GetAttribute(occurs, Properties.Resources.edifact_utility_ns) is string scriptCurrentOccurs && !string.IsNullOrEmpty(scriptCurrentOccurs))
            {
                string originalElementOccurs = originalElementNav.GetAttribute(occurs, string.Empty);
                if (scriptCurrentOccurs != "1")
                {
                    if (originalElementOccurs != scriptCurrentOccurs)
                    {
                        originalElementNav.SelectSingleNode(string.Format(s_attributeXPathFormat, occurs))?.DeleteSelf();
                        originalElementNav.CreateAttribute(string.Empty, occurs, string.Empty, scriptCurrentOccurs);
                    }
                }
                else
                {
                    originalElementNav.SelectSingleNode(string.Format(s_attributeXPathFormat, occurs))?.DeleteSelf();
                }
            }
        }
    }

    private void ProcessSegment(XPathNavigator contextNav, XPathNavigator scriptCurrentNav)
    {
        string name2 = scriptCurrentNav.GetAttribute("name", string.Empty);
        if (
            contextNav.SelectSingleNode(
                string.Format("xs:sequence/xs:element[@name='{0}'][position()=1]", name2), _man
            ) is XPathNavigator originalElementNav
        )
        {
            if (scriptCurrentNav.SelectSingleNode("@eu:action", _man) is XPathNavigator actionNav)
            {
                if (actionNav.Value == "remove" || actionNav.Value == "removeUntilNext")
                {
                    string? next = scriptCurrentNav.SelectSingleNode("following-sibling::*[1]")?.GetAttribute("name", string.Empty);
                    List<XPathNavigator> toRemove = [originalElementNav];
                    if (actionNav.Value == "removeUntilNext")
                    {
                        XPathNodeIterator followingSiblingNI = originalElementNav.Select("following-sibling::*");
                        while (followingSiblingNI.MoveNext())
                        {
                            string name1 = followingSiblingNI.Current!.GetAttribute("name", string.Empty);
                            if (s_reSegmentGroup.IsMatch(name1!) || name1.Equals(next))
                            {
                                break;
                            }
                            toRemove.Add(followingSiblingNI.Current!.CreateNavigator());
                        }
                    }
                    foreach (var toRemoveNav in toRemove)
                    {
                        toRemoveNav.DeleteSelf();
                    }
                }
                else
                {
                    ThrowUnexpectedAction(actionNav.Value, scriptCurrentNav);
                }
            }
            else
            {
                ProcessOccurs(originalElementNav, scriptCurrentNav);
            }
        }
        else
        {
            throw new Exception($"TODO: Segment '{name2}' not found.");
        }
    }

    private void CustomizeRestriction(XmlDocument doc, XPathNodeIterator scriptOperationsNI, string name, XmlDocument source)
    {
        XPathNavigator originalType = source.CreateNavigator()!.SelectSingleNode(string.Format("/xs:schema/xs:complexType[@name='{0}']", name), _man)!;
        XPathNodeIterator segmentsNI = doc.CreateNavigator()!.Select(".//xs:element", _man);
        Dictionary<string, XPathNavigator> nodes = [];
        while (segmentsNI.MoveNext())
        {
            string elementName = segmentsNI.Current!.GetAttribute("name", string.Empty);
            if (!s_reSegmentGroup.IsMatch(elementName) && elementName != s_message1)
            {
                if (WalkSegmentTree(doc, segmentsNI.Current, name, nodes))
                {
                    segmentsNI.Current!.SelectSingleNode("@type")!.SetValue(string.Format("{0}.1", elementName));
                }
            }
        }
        if (scriptOperationsNI.Current!.SelectSingleNode(".//*[@eu:action]", _man) is XPathNavigator actionNav)
        {
            string action = actionNav.GetAttribute("action", Properties.Resources.edifact_utility_ns);
            if (action == "clearEnumerations")
            {
                if (actionNav.LocalName != "restriction")
                {
                    ThrowUnexpectedAction(action, actionNav);
                }
                XPathNavigator copiedRestrictionNav = doc.CreateNavigator()!
                    .SelectSingleNode(
                        string.Format(
                            "/xs:schema/xs:complexType[@name='{0}.1']/xs:simpleContent/xs:restriction",
                            name
                        ),
                        _man
                    )!;
                XPathNodeIterator enumerationsNI = copiedRestrictionNav.Select("xs:enumeration", _man);
                List<XPathNavigator> enumerationsToDelete = [];
                while (enumerationsNI.MoveNext())
                {
                    enumerationsToDelete.Add(enumerationsNI.Current!.CreateNavigator());
                }
                foreach (XPathNavigator it in enumerationsToDelete)
                {
                    it.DeleteSelf();
                }
            }
        }
        if (scriptOperationsNI.Current!.SelectSingleNode("xs:simpleContent/xs:restriction", _man) is XPathNavigator restrictionNav)
        {
            XPathNodeIterator facetsNI = restrictionNav.Select("xs:*", _man);
            XPathNavigator copiedRestrictionNav = doc.CreateNavigator()!
                .SelectSingleNode(
                    string.Format(
                        "/xs:schema/xs:complexType[@name='{0}.1']/xs:simpleContent/xs:restriction",
                        name
                    ),
                    _man
                )!;
            while (facetsNI.MoveNext())
            {
                if (facetsNI.Current!.LocalName == "enumeration")
                {
                    if (
                        originalType
                            .SelectSingleNode(
                                string.Format(
                                    "xs:simpleContent/xs:restriction/xs:enumeration[@value='{0}']",
                                    facetsNI.Current.SelectSingleNode("@value")!.Value
                                ),
                                _man
                            ) is XPathNavigator originalEnumeration
                    )
                    {
                        copiedRestrictionNav.AppendChild(s_reXmlns.Replace(originalEnumeration.OuterXml, string.Empty).Trim());
                    }
                    else
                    {
                        copiedRestrictionNav.AppendChild(s_reXmlns.Replace(facetsNI.Current.OuterXml, string.Empty).Trim());
                    }
                }
                else if (copiedRestrictionNav.SelectSingleNode(string.Format("xs:*[local-name()='{0}']", facetsNI.Current!.LocalName), _man) is XPathNavigator copiedFacet)
                {
                    copiedFacet.SelectSingleNode("@value")!.SetValue(facetsNI.Current.SelectSingleNode("@value")!.Value);
                }
                else
                {
                    copiedRestrictionNav.AppendChild(s_reXmlns.Replace(facetsNI.Current.OuterXml, string.Empty).Trim());
                }
            }
        }
    }

    private void ThrowUnexpectedAction(string action, XPathNavigator actionNav)
    {
        throw new Exception($"TODO: Unexpected '{action}' action at '{actionNav.Prefix}:{actionNav.LocalName}' element.");
    }

    private XmlDocument? GetSchemaSource(string typeName, string ns)
    {
        XmlDocument? source = null;
        if (_schemaSet.GlobalTypes[new XmlQualifiedName(typeName, ns)] is XmlSchemaObject obj)
        {
            if (!_sourcesCache.TryGetValue(obj.SourceUri!, out source))
            {
                source = new XmlDocument();
                using (Stream s = (Stream)_resolver.GetEntity(new Uri(obj.SourceUri!), null, typeof(Stream))!)
                {
                    source.Load(s);

                }
                _sourcesCache.Add(obj.SourceUri!, source);
            }
        }
        return source;
    }
    private bool WalkSegmentTree(XmlDocument doc, XPathNavigator current, string name, Dictionary<string, XPathNavigator> nodes)
    {
        bool result = false;
        string currentName = current.GetAttribute("name", string.Empty);

        if (GetSchemaSource(currentName, _targetNamespace) is XmlDocument source)
        {
            XPathNavigator originalTypeNav = source.CreateNavigator()!
                .SelectSingleNode(
                    string.Format(
                        "//xs:complexType[@name='{0}']",
                        currentName
                    ),
                    _man
                )!;

            if (currentName == name)
            {
                result = true;
                if (doc.CreateNavigator()!.SelectSingleNode(string.Format("/xs:schema/xs:complexType[@name='{0}.1']", currentName), _man) is not XPathNavigator nav1)
                {
                    doc.DocumentElement!.CreateNavigator()!.AppendChild(s_reXmlns.Replace(originalTypeNav.OuterXml, string.Empty).Trim());
                    ((XmlElement)doc.DocumentElement!.LastChild!).SetAttribute("name", string.Format("{0}.1", currentName));
                }

            }
            else
            {
                XPathNodeIterator ni = originalTypeNav.Select(".//xs:sequence/xs:element", _man);
                while (ni.MoveNext())
                {
                    if (WalkSegmentTree(doc, ni.Current!, name, nodes))
                    {
                        result = true;
                        if (doc.CreateNavigator()!.SelectSingleNode(string.Format("/xs:schema/xs:complexType[@name='{0}.1']", currentName), _man) is not XPathNavigator nav1)
                        {
                            doc.DocumentElement!.CreateNavigator()!.AppendChild(s_reXmlns.Replace(originalTypeNav.OuterXml, string.Empty).Trim());
                            ((XmlElement)doc.DocumentElement!.LastChild!).SetAttribute("name", string.Format("{0}.1", currentName));
                        }
                    }
                }
            }
            return result;
        }
        throw new Exception($"TODO: Type {current.GetAttribute("name", string.Empty)} not found.");

    }

    private void _schemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        switch (e.Severity)
        {
            case XmlSeverityType.Warning:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
            case XmlSeverityType.Error:
                throw e.Exception;
        }
    }
}
