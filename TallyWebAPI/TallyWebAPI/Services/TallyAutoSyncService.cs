using Microsoft.Extensions.DependencyInjection;

namespace TallyWebAPI.Services
{
    public class TallyAutoSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TallyAutoSyncService> _logger;

        // First version:
        // Sync once every 1 minute.
        private readonly TimeSpan _syncInterval =
            TimeSpan.FromMinutes(1);

        public TallyAutoSyncService(
            IServiceScopeFactory scopeFactory,
            ILogger<TallyAutoSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Tally Auto Sync Service started."
            );

            // Small startup delay.
            // Allows ASP.NET Core application to finish starting.
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(10),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncCycleAsync(
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Important:
                    // One failed sync must NOT stop the backend.
                    _logger.LogError(
                        ex,
                        "Unexpected error during Tally Auto Sync cycle."
                    );
                }

                try
                {
                    await Task.Delay(
                        _syncInterval,
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Tally Auto Sync Service stopped."
            );
        }

        private async Task RunSyncCycleAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Tally Auto Sync cycle started at {Time}.",
                DateTime.Now
            );

            // New DI scope for every sync cycle.
            //
            // DbContext and our sync services are scoped services,
            // so they must be resolved inside a scope.
            using var scope =
                _scopeFactory.CreateScope();

            var provider =
                scope.ServiceProvider;

            // =====================================================
            // 1. COMPANY SYNC
            //
            // Must run first.
            //
            // If client creates a new company in Tally,
            // it must reach Companies_tbl before the other
            // company-wise sync services execute.
            // =====================================================

            await RunModuleAsync(
                "Company",
                async () =>
                {
                    var service =
                        provider.GetRequiredService<
                            CompanySyncService>();

                    await service.SyncAsync();
                },
                stoppingToken
            );

            // =====================================================
            // 2. LEDGER SYNC
            // =====================================================

            await RunModuleAsync(
                "Ledger",
                async () =>
                {
                    var service =
                        provider.GetRequiredService<
                            LedgerSyncService>();

                    await service.SyncAsync();
                },
                stoppingToken
            );

            // =====================================================
            // 3. STOCK ITEM SYNC
            // =====================================================

            await RunModuleAsync(
                "Stock Item",
                async () =>
                {
                    var service =
                        provider.GetRequiredService<
                            StockItemSyncService>();

                    await service.SyncAsync();
                },
                stoppingToken
            );

            // =====================================================
            // 4. VOUCHER SYNC
            // =====================================================

            await RunModuleAsync(
                "Voucher",
                async () =>
                {
                    var service =
                        provider.GetRequiredService<
                            VoucherSyncService>();

                    await service.SyncAsync();
                },
                stoppingToken
            );

            // =====================================================
            // 5. OUTSTANDING SYNC
            //
            // Keep this after Voucher Sync because outstanding
            // can change when vouchers are added/edited.
            // =====================================================

            await RunModuleAsync(
                "Outstanding",
                async () =>
                {
                    var service =
                        provider.GetRequiredService<
                            OutstandingSyncService>();

                    await service.SyncAsync();
                },
                stoppingToken
            );

            _logger.LogInformation(
                "Tally Auto Sync cycle completed at {Time}.",
                DateTime.Now
            );
        }

        private async Task RunModuleAsync(
            string moduleName,
            Func<Task> syncAction,
            CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                _logger.LogInformation(
                    "{Module} Auto Sync started.",
                    moduleName
                );

                await syncAction();

                _logger.LogInformation(
                    "{Module} Auto Sync completed.",
                    moduleName
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // IMPORTANT:
                //
                // If Ledger Sync fails, Stock/Voucher/Outstanding
                // can still continue.
                //
                // Backend also remains running.
                _logger.LogError(
                    ex,
                    "{Module} Auto Sync failed.",
                    moduleName
                );
            }
        }
    }
}