using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Utils.API
{
    public class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API routes
            config.MapHttpAttributeRoutes();

            // Make json the only and default response
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Camel-casing json response
            config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            config.Formatters.JsonFormatter.SerializerSettings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;

            // Enable CORS
            config.EnableCors(new EnableCorsAttribute("*", "*", "*"));
        }
    }
}