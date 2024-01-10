using System.Net.Http;
using System.Web.Http;
using System.Net;
using System.Linq;
using System.IO;
using System.Net.Http.Headers;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System.Web;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Formatting;

namespace Utils.API.Controllers
{
    [RoutePrefix("pdf")]
    public class PdfController : ApiController
    {
        [HttpGet, Route("invoice")]
        public HttpResponseMessage Invoice(string date, string number, string total, string emails, string clientNumber, string matter, string description)
        {
            byte[] pdf = null;
            using (var ms = new MemoryStream())
            {
                using (var doc = new Document())
                {
                    doc.SetMargins(60, 60, 40, 40);
                    PdfWriter.GetInstance(doc, ms);
                    doc.Open();

                    #region Header
                    var table = new PdfPTable(4) { WidthPercentage = 100 };
                    var colSizes = new List<float> { 120f, 130f, 140f };
                    colSizes.Add(doc.PageSize.Width - colSizes.Sum());
                    table.SetWidths(colSizes.ToArray());
                    table.AddCell(new PdfPCell
                    {
                        Image = Image.GetInstance(HttpContext.Current.Request.MapPath("~\\App_Data\\Logo.jpg")),
                        BorderColorRight = BaseColor.WHITE,
                        Rowspan = 3
                    });

                    table.AddCell(new PdfPCell(CreatePhrase("950 E. State Hwy 114\nSuite 160\nSouthlake, TX 76092"))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Rowspan = 3
                    });

                    table.AddCell(CreateCell("Invoice Date", Element.ALIGN_RIGHT));
                    table.AddCell(CreateCell(date, Element.ALIGN_CENTER));
                    table.AddCell(CreateCell("Invoice Number", Element.ALIGN_RIGHT));
                    table.AddCell(CreateCell(number, Element.ALIGN_CENTER));
                    table.AddCell(CreateCell("Invoice Total", Element.ALIGN_RIGHT));
                    table.AddCell(CreateCell(total, Element.ALIGN_CENTER));
                    doc.Add(table);
                    #endregion

                    #region Emails
                    doc.Add(CreatePhrase(" "));
                    table = new PdfPTable(1) { WidthPercentage = 100 };
                    table.AddCell(CreateCell($"Invoice for {emails}", Element.ALIGN_CENTER));
                    doc.Add(table);
                    #endregion

                    #region Matters
                    doc.Add(CreatePhrase(" "));
                    table = new PdfPTable(4) { WidthPercentage = 100 };
                    colSizes = new List<float> { 120f, 130f, 80f };
                    colSizes.Insert(2, doc.PageSize.Width - colSizes.Sum());
                    table.SetWidths(colSizes.ToArray());

                    var columns = new List<string> { "Client", "Matter", "Description", "Amount" };                    
                    columns.ForEach(c =>
                    {
                        var cell = new PdfPCell
                        {
                            Phrase = CreatePhrase(c, headerFont),
                            HorizontalAlignment = Element.ALIGN_CENTER                                                    
                        };
                        table.AddCell(cell);
                    });

                    columns = new List<string> { clientNumber, matter, description, total };
                    columns.ForEach(c =>
                    {
                        table.AddCell(CreateCell(c, Element.ALIGN_CENTER));
                    });
                    doc.Add(table);
                    #endregion

                    #region Footer
                    doc.Add(CreatePhrase("\nIf you have any questions, please contact us at info@syncids.com.\n"));
                    doc.Add(CreatePhrase("\nThank you for using SyncIDS.com!"));
                    #endregion
                }
                pdf = ms.ToArray();
            }  

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(pdf);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }

        [HttpGet, Route("merge")]
        public HttpResponseMessage Merge(string urls)
        {
            var urlArray = urls.Split(',');

            var wc = new WebClient();
            var pdfContents = urlArray.Select(u => Download(wc, u)).ToList();
            byte[] mergedPdf = null;
            using (var ms = new MemoryStream())
            {
                using (var document = new Document())
                {
                    using (var copy = new PdfCopy(document, ms))
                    {
                        document.Open();                 
                        pdfContents.ForEach(c => {
                            if (c != null) copy.AddDocument(new PdfReader(c));
                        });
                    }
                }
                mergedPdf = ms.ToArray();
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(mergedPdf);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }

        [HttpGet, Route("fill-form")]
        public HttpResponseMessage FillForm(string formFile, string dataUrl)
        {
            var file = formFile.Contains(".pdf") ? formFile : $"{formFile}.pdf";
            var form = HttpContext.Current.Request.MapPath($"~\\App_Data\\Forms\\{file}");

            byte[] filedPdf;
            using (var fileStream = File.OpenRead(form))
            {
                using (var outStream = new MemoryStream())
                {
                    XFAImport(fileStream, Download(new WebClient(), dataUrl), outStream);
                    filedPdf = outStream.ToArray();
                }
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(filedPdf);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }

        [HttpPost, Route("fill-form")]
        public HttpResponseMessage FillFormPost(string formFile)
        {
            var file = formFile.Contains(".pdf") ? formFile : $"{formFile}.pdf";
            var form = HttpContext.Current.Request.MapPath($"~\\App_Data\\Forms\\{file}");
            var dataXml = GetBody(Request).Result;

            byte[] filedPdf;
            using (var fileStream = File.OpenRead(form))
            {
                using (var outStream = new MemoryStream())
                {
                    XFAImport(fileStream, Encoding.UTF8.GetBytes(dataXml), outStream);
                    filedPdf = outStream.ToArray();
                }
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(filedPdf);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return response;
        }

        [HttpGet, Route("health-check")]
        public HttpResponseMessage HealthtCheck(string p = "No parameter p provided")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = new { success = true, p };
            response.Content = new ObjectContent(content.GetType(), content, new JsonMediaTypeFormatter());
            return response;
        }

        private async Task<string> GetBody(HttpRequestMessage request)
        {
            try
            {
                return await request.Content.ReadAsStringAsync();
            }
            catch
            {
                return "";
            }
        }

        private byte[] Download(WebClient wc, string url)
        {
            try
            {
                return wc.DownloadData(url);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Imports XFA Data into a new PDF file.
        /// </summary>
        /// <param name="pdfTemplate">A PDF File with an unpopulated form.</param>
        /// <param name="xmlFormData">XFA form data in XML format.</param>
        /// <returns>a memorystream containing the new PDF file.</returns>
        private void XFAImport(Stream pdfTemplate, byte[] xmlFormData, Stream outputStream)
        {
            using (var reader = new PdfReader(pdfTemplate))
            {
                using (var stamper = new PdfStamper(reader, outputStream, '\0', true))
                {
                    using (var ms = new MemoryStream(xmlFormData))
                    {
                        stamper.Writer.CloseStream = false;
                        stamper.AcroFields.Xfa.FillXfaForm(ms);
                    }
                }
            }
        }

        static Font headerFont = FontFactory.GetFont(BaseFont.HELVETICA, 10, Font.BOLD);
        static Font baseFont = FontFactory.GetFont(BaseFont.HELVETICA, 10);
        static PdfPCell CreateCell(string text, int? alignment)
        {
            var cell = new PdfPCell(CreatePhrase(text));
            if (alignment.HasValue) cell.HorizontalAlignment = alignment.Value;
            return cell;
        }

        static Phrase CreatePhrase(string str, Font font = null)
        {
            if (font == null) font = baseFont;
            return new Phrase(str, font);
        }
    }
}
