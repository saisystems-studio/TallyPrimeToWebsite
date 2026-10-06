using TallyWebSyncAgent;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();