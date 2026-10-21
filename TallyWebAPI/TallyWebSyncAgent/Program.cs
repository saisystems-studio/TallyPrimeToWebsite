using TallyWebSyncAgent;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

builder.Services.AddHttpClient("Tally", client =>
{
    client.BaseAddress =
        new Uri("http://127.0.0.1:9000/");

    client.Timeout =
        TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<LedgerSyncWorker>();
builder.Services.AddSingleton<StockItemSyncWorker>();
builder.Services.AddSingleton<VoucherSyncWorker>();
builder.Services.AddSingleton<OutstandingSyncWorker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TallyWeb Sync Agent";
});


builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
