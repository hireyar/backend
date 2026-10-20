using System.IO.Compression;
using System.Text;
using System.Xml;
using HireYar.Application.Administration;
namespace HireYar.Infrastructure.Administration;

internal static class CustomerWorkbook
{
    // Inline strings preserve mobile leading zeroes and prevent spreadsheet formula injection.
    public static byte[] Create(IEnumerable<AdminCustomerDto> customers)
    {
        using var buffer=new MemoryStream();
        using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true)) {
            Write(zip,"[Content_Types].xml","""
                <?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Write(zip,"_rels/.rels","""
                <?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Write(zip,"xl/workbook.xml","""
                <?xml version="1.0" encoding="utf-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="مشتریان" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Write(zip,"xl/_rels/workbook.xml.rels","""
                <?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            using var output=zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml=XmlWriter.Create(output,new XmlWriterSettings {Encoding=new UTF8Encoding(false),CloseOutput=false});
            const string ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            xml.WriteStartDocument();xml.WriteStartElement("worksheet",ns);xml.WriteStartElement("sheetViews",ns);xml.WriteStartElement("sheetView",ns);
            xml.WriteAttributeString("workbookViewId","0");xml.WriteAttributeString("rightToLeft","1");xml.WriteStartElement("pane",ns);xml.WriteAttributeString("ySplit","1");xml.WriteAttributeString("topLeftCell","A2");xml.WriteAttributeString("state","frozen");xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();
            xml.WriteStartElement("cols",ns);xml.WriteStartElement("col",ns);xml.WriteAttributeString("min","1");xml.WriteAttributeString("max","12");xml.WriteAttributeString("width","24");xml.WriteAttributeString("customWidth","1");xml.WriteEndElement();xml.WriteEndElement();
            xml.WriteStartElement("sheetData",ns);
            var row=1;
            void Row(params string?[] cells) {
                xml.WriteStartElement("row",ns);xml.WriteAttributeString("r",row.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for(var i=0;i<cells.Length;i++) {
                    xml.WriteStartElement("c",ns);xml.WriteAttributeString("r",((char)('A'+i)).ToString()+row);xml.WriteAttributeString("t","inlineStr");
                    xml.WriteStartElement("is",ns);xml.WriteStartElement("t",ns);xml.WriteAttributeString("xml","space",null,"preserve");xml.WriteString(XmlText(cells[i] ?? ""));xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndElement();
                }
                xml.WriteEndElement();row++;
            }
            Row("شناسه","نام مشتری","سازمان","موبایل","ایمیل","ثبت‌نام UTC","وضعیت ثبت‌نام","فروشنده","وضعیت اشتراک","پایان اشتراک UTC","مصاحبه باقی‌مانده","مصاحبه قابل دعوت");
            foreach(var c in customers) Row(c.Id.ToString(),c.FullName,c.OrganizationName,c.Mobile,c.Email,c.RegisteredAtUtc?.ToString("O"),c.RegistrationStatus,c.AgentName,c.SubscriptionStatus,c.SubscriptionExpiresAtUtc?.ToString("O"),c.RemainingInterviews.ToString(System.Globalization.CultureInfo.InvariantCulture),c.AvailableInterviews.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xml.WriteEndElement();xml.WriteStartElement("autoFilter",ns);xml.WriteAttributeString("ref","A1:L"+(row-1));xml.WriteEndElement();xml.WriteEndElement();xml.WriteEndDocument();
        }
        return buffer.ToArray();
    }
    private static string XmlText(string value)
    {
        var output=new StringBuilder(value.Length);
        foreach(var rune in value.EnumerateRunes())
            if(rune.Value is 0x9 or 0xA or 0xD or >=0x20 and <=0xD7FF or >=0xE000 and <=0xFFFD or >=0x10000 and <=0x10FFFF)
                output.Append(rune.ToString());
        return output.ToString();
    }
    private static void Write(ZipArchive zip,string path,string text) {using var stream=zip.CreateEntry(path).Open();using var writer=new StreamWriter(stream,new UTF8Encoding(false));writer.Write(text);}
}
