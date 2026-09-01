using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Azure.Storage.Queues;
using Umayor.Dynamics.DeletePoc.Models;
using Umayor.Dynamics.DeletePoc.Services;
using Umayor.Dynamics.DeletePoc.Shared.Models;

namespace Umayor.Dynamics.DeletePoc.Services;

public class OutboxRecoveryWorker : BackgroundService
{
    private readonly DataverseConnectionFactory _factory;
    private readonly QueueClient _queueClient;
    private readonly AppSettings _settings;
    private readonly ILogger<OutboxRecoveryWorker> _logger;

    public OutboxRecoveryWorker(
        DataverseConnectionFactory factory,
        QueueClient queueClient,
        AppSettings settings,
        ILogger<OutboxRecoveryWorker> logger)
    {
        _factory = factory;
        _queueClient = queueClient;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxRecoveryWorker iniciado. Monitoreando lotes masivos huérfanos.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Esperar 2 minutos entre chequeos
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

                _logger.LogDebug("Chequeando lotes masivos Pendientes en Dataverse...");
                using var client = _factory.CreateClient(_settings.Dataverse);

                var query = new QueryExpression("um_massexecution")
                {
                    ColumnSet = new ColumnSet("um_massexecutionid", "createdon", "um_estado")
                };
                
                // Buscar registros que sigan en estado Pendiente
                query.Criteria.AddCondition("um_estado", ConditionOperator.Equal, MassOptionSets.HeaderEstadoPendiente);
                
                // Opcional: Procesar solo los creados hace más de 2 minutos para evitar conflictos con el flujo normal de la API
                query.Criteria.AddCondition("createdon", ConditionOperator.LessThan, DateTime.UtcNow.AddMinutes(-2));

                var results = client.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    _logger.LogWarning($"Se encontraron {results.Entities.Count} lotes masivos huérfanos en estado Pendiente. Re-encolando.");

                    foreach (var entity in results.Entities)
                    {
                        string executionId = entity.Id.ToString("N");
                        
                        var messageText = JsonSerializer.Serialize(new { executionId });
                        var messageBytes = Encoding.UTF8.GetBytes(messageText);
                        var base64Message = Convert.ToBase64String(messageBytes);

                        await _queueClient.SendMessageAsync(base64Message, stoppingToken);
                        _logger.LogInformation($"Lote masivo {executionId} re-encolado con éxito por OutboxRecoveryWorker.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Apagado normal de la aplicación
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en OutboxRecoveryWorker al recuperar lotes pendientes.");
            }
        }
    }
}
