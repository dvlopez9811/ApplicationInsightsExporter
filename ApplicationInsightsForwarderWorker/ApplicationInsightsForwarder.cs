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


        HttpClient _client;
        string _otlpEndpoint;
        ApplicationInsights2OTLP.Convert _converter;


        public ApplicationInsightsForwarder(ILogger<ApplicationInsightsForwarder> logger, IHttpClientFactory httpClientFactory, ApplicationInsights2OTLP.Convert otlpConverter)
        {
            _logger = logger;
            _converter = otlpConverter;

            _client = httpClientFactory.CreateClient("ApplicationInsightsExporter");

            _otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? string.Empty;
            if (!_otlpEndpoint.Contains("v1/traces"))
                if (_otlpEndpoint.EndsWith("/"))
                    _otlpEndpoint = _otlpEndpoint += "v1/traces";
                else
                    _otlpEndpoint = _otlpEndpoint += "/v1/traces";
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
                    //string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
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

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
