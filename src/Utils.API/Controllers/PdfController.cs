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
using System;

namespace Utils.API.Controllers
{
    [RoutePrefix("pdf")]
    public class PdfController : ApiController
    {
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
    }
}
