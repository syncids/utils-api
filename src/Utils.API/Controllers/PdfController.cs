using System.Net.Http;
using System.Web.Http;
using System.Net;
using System.Linq;
using System.IO;
using System.Net.Http.Headers;
using iTextSharp.text.pdf;
using iTextSharp.text;

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
            var pdfContents = urlArray.Select(u => this.Download(wc, u)).ToList();
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
    }
}
