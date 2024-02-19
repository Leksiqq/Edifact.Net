using System.Xml;
using System.Xml.XPath;

XmlDocument doc = new();
doc.Load(args[0]);
XPathNavigator nav = doc.CreateNavigator()!;
XPathNavigator nav1 = nav.SelectSingleNode("/root/assembly[@alias='System.Windows.Forms']/@name")!;
string asm = nav1.Value.Replace("System.Windows.Forms", "mscorlib");
XPathNodeIterator ni = nav.Select("/root/data");
while (nav.SelectSingleNode("/root/data[position()=1]") is XPathNavigator data)
{
    Console.WriteLine($"delete: {data}");
    data.DeleteSelf();
}
XmlWriterSettings ws = new()
{
    Indent = true
};

Walk(
    args[1],
    doc.CreateNavigator()!.SelectSingleNode("/root")!,
    Path.GetDirectoryName(Path.GetFullPath(args[0]))!,
    Path.GetFullPath(args[1])!
);
XmlWriter wr = XmlWriter.Create(args[0], ws);
doc.WriteTo(wr);
wr.Close();

void Walk(string folder, XPathNavigator root, string base1, string base2)
{
    foreach (string file in Directory.GetFiles(folder))
    {
        root.AppendChild(string.Format(@"  <data name=""{0}"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>{1};System.Byte[], {2}</value>
  </data>
", Path.GetRelativePath(base2, Path.GetFullPath(file)), Path.GetRelativePath(base1, Path.GetFullPath(file)), asm));
    }
    foreach (string dir in Directory.GetDirectories(folder))
    {
        Walk(dir, root, base1, base2);
    }
}
