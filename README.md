**Attention!** _This article, as well as this announcement, are automatically translated from Russian_.

# Net.Leksi.Edifact
The **Net.Leksi.Edifact** library is designed to convert messages from the EDIFACT format to XML and vice versa. The library also contains a number of utilities for creating a working environment.

All classes are contained in the `Net.Leksi.Edifact` namespace.

[Contents of the library](https://github.com/Leksiqq/Edifact.Net/wiki/Review#contents-of-the-library)

The following functionality is available.

* [EdifactDownloaderCLI](https://github.com/Leksiqq/Edifact.Net/wiki/EdifactDownloaderCLI-en): preparation of a command line application for downloading specifications from the site [UNECE](https://unece.org/) with converting them into a set of XML schemas, which are subsequently used as a *grammar *, and to check data and structure restrictions for the translator between formats.
* [EdifactMessageCustomizerCLI](https://github.com/Leksiqq/Edifact.Net/wiki/EdifactMessageCustomizerCLI-en): preparation of a command line application for editing the specification of a standard message (*customization*) for the needs of an agreed exchange between specific parties. This includes reducing the set of segments used and changing data limits.
* [EdifactMessageVisualizerCLI](https://github.com/Leksiqq/Edifact.Net/wiki/EdifactMessageVisualizerCLI-en): template command line application for visualizing the specification of a standard or abbreviated message in the form of a tree of segments and groups of segments.
* [EdifactParser](EdifactParser-en): translation of the incoming flow of the exchange session (interchange) into events:
     - start of the session;
     - beginning of the functional group (if any);
     - the beginning of the message, the handler must provide a stream for writing the XML document received from the message;
     - end of message;
     - end of the functional group (if any);
     - end of the session.
* [EdifactBuilder](https://github.com/Leksiqq/Edifact.Net/wiki/EdifactBuilder-en): broadcasting calls of the corresponding methods to the specified outgoing stream of the exchange session:
     - start of the session;
     - beginning of the functional group (if any);
     - message transmission, the sender provides a stream to read the XML document for translation into the message;
     - end of the functional group (if any);
     - end of the session.
* [EdifactParserCLI](https://github.com/Leksiqq/Edifact.Net/wiki/EdifactParserCLI-en): template command line application for translating the exchange file into XML files.

In addition to the library, the working environment includes a directory of XML schemas containing schemas obtained from downloaded specifications, as well as custom schemas. [More details...](https://github.com/Leksiqq/Edifact.Net/wiki/SchemasRoot-en)

Customization is carried out using a script, which is an XML schema of a special structure. [More...](https://github.com/Leksiqq/Edifact.Net/wiki/CustomizerScript-en)

The structure of XML documents is described [Here](https://github.com/Leksiqq/Edifact.Net/wiki/XMLDocuments-en).
