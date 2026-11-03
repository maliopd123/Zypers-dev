using SistemaDeChaves.DAO;

namespace SistemaDeChaves.Services;

public sealed class AtrasoMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AtrasoMonitorService> _logger;

    public AtrasoMonitorService(IServiceScopeFactory scopeFactory, ILogger<AtrasoMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configuracoes = scope.ServiceProvider.GetRequiredService<ConfiguracoesDAO>();
                var alertas = scope.ServiceProvider.GetRequiredService<AlertasDAO>();
                var config = await configuracoes.ObterAsync(stoppingToken);
                await alertas.CriarAlertasDeAtrasoAsync(config.MinutosParaAlerta, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Não foi possível atualizar os alertas de chaves em atraso.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
