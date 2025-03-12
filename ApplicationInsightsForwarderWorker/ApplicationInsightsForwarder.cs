using System;
using System.Text;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApplicationInsightsForwarderWorker
{
    public class ApplicationInsightsForwarder
    {
        private readonly ILogger<ApplicationInsightsForwarder> _logger;
        private readonly HttpClient _client;
        private readonly string _otlpEndpoint;
        private readonly ApplicationInsights2OTLP.Convert _converter;

        public ApplicationInsightsForwarder(ILogger<ApplicationInsightsForwarder> logger, IHttpClientFactory httpClientFactory, ApplicationInsights2OTLP.Convert otlpConverter)
        {
            _logger = logger;
            _converter = otlpConverter;

            _client = httpClientFactory.CreateClient("ApplicationInsightsExporter");

            _otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? string.Empty;
            if (!_otlpEndpoint.Contains("v1/traces"))
                _otlpEndpoint = _otlpEndpoint.TrimEnd('/') + "/v1/traces";
        }

        [Function("ForwardAI")]
        public async Task Run([EventHubTrigger("appinsights", Connection = "EHConnection")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    if (eventData.Body.Length == 0)
                    {
                        log.LogWarning("Evento recibido sin contenido en el cuerpo. Se omite.");
                        continue;
                    }

                    byte[] msgBody = eventData.Body.ToArray();
                    string messageBody = Encoding.UTF8.GetString(msgBody, 0, msgBody.Length);

                    if (string.IsNullOrWhiteSpace(messageBody))
                    {
                        log.LogWarning("El mensaje recibido está vacío o es inválido. Se omite.");
                        continue;
                    }

                    log.LogInformation($"Mensaje recibido: {messageBody.Substring(0, Math.Min(500, messageBody.Length))}...");

                    var exportTraceServiceRequest = _converter.FromApplicationInsights(messageBody);
                    if (exportTraceServiceRequest == null)
                    {
                        log.LogWarning("No se pudo convertir el evento correctamente. Se omite.");
                        continue;
                    }

                    var content = new ApplicationInsights2OTLP.ExportRequestContent(exportTraceServiceRequest);
                    var res = await _client.PostAsync(_otlpEndpoint, content);

                    if (!res.IsSuccessStatusCode)
                    {
                        log.LogError($"Error enviando la traza: {res.StatusCode}. Mensaje: {messageBody.Substring(0, Math.Min(500, messageBody.Length))}...");
                    }
                }
                catch (Exception e)
                {
                    log.LogError($"Error procesando evento: {e.Message}");
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
