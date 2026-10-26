using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TallyWebAPI.Data;
using TallyWebAPI.Services;
using TallyWebAPI.Services.SalesRegister;
using TallyWebAPI.Services.PurchaseRegister;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var readOnlyPurchasePreviewMode = builder.Configuration.GetValue<bool>("Startup:ReadOnlyPurchasePreviewMode");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT bearer token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();


// ==========================================
// DATABASE - SQL SERVER
// ==========================================

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    );
});


// ==========================================
// TALLY SERVICE
// ==========================================

var tallyEndpoint = new Uri(
    builder.Configuration["Tally:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration value Tally:BaseUrl is required."),
    UriKind.Absolute);

builder.Services.AddHttpClient<TallyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.BaseAddress = tallyEndpoint;
});

builder.Services.AddHttpClient<VoucherService>(client => client.BaseAddress = tallyEndpoint);
builder.Services.AddHttpClient<SalesReportService>(client => client.BaseAddress = tallyEndpoint);
builder.Services.AddHttpClient<LedgerReportService>(client => client.BaseAddress = tallyEndpoint);
builder.Services.AddHttpClient<StockSummaryService>(client => client.BaseAddress = tallyEndpoint);
builder.Services.AddScoped<StockSummarySnapshotService>();
builder.Services.AddScoped<LedgerReportSnapshotService>();
builder.Services.AddHttpClient<OutstandingService>(client => client.BaseAddress = tallyEndpoint);
builder.Services.AddScoped<StockItemSyncService>();
builder.Services.AddScoped<CompanySyncService>();
builder.Services.AddScoped<LedgerSyncService>();
builder.Services.AddScoped<VoucherSyncService>();
builder.Services.AddScoped<SalesRegisterSyncService>();
// Approval plans and keys live outside SQL. This registration never runs an apply or a hosted sync.
var salesPlanDirectory = Path.Combine(builder.Environment.ContentRootPath, ".sales-register-plans");
var salesProtection = builder.Services.AddDataProtection().SetApplicationName("TallyWeb.SalesRegister")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(salesPlanDirectory, "keys")));
if (OperatingSystem.IsWindows()) salesProtection.ProtectKeysWithDpapi();
builder.Services.Configure<SalesSyncOptions>(builder.Configuration.GetSection("SalesRegisterSync"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISalesSyncPlanStore>(sp => new SalesSyncPlanStore(salesPlanDirectory, sp.GetRequiredService<IDataProtectionProvider>()));
builder.Services.AddScoped<ISalesSyncRepository, SalesSyncRepository>();
builder.Services.AddHttpClient<ISalesSyncSource, SalesSyncSource>(client => { client.BaseAddress = tallyEndpoint; client.Timeout = TimeSpan.FromSeconds(90); });
builder.Services.AddHttpClient<ISalesRangeSyncSource, SalesSyncSource>(client => { client.BaseAddress = tallyEndpoint; client.Timeout = TimeSpan.FromSeconds(90); });
builder.Services.AddScoped<SalesSyncCoordinator>();
builder.Services.AddScoped<BoundedSalesImportService>();
builder.Services.AddSingleton<PurchaseImportPlanStore>();
builder.Services.AddHttpClient<PurchaseTallySource>(client => { client.BaseAddress = tallyEndpoint; client.Timeout = TimeSpan.FromSeconds(90); });
builder.Services.AddScoped<PurchaseImportCoordinator>();


// Enable automatic Tally sync only outside Development
if (!builder.Environment.IsDevelopment() && !readOnlyPurchasePreviewMode)
{
    builder.Services.AddHostedService<TallyAutoSyncService>();
}


builder.Services.AddScoped<OutstandingSyncService>();




// ==========================================
// CORS - REACT FRONTEND
// ==========================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ==========================================
// JWT AUTHENTICATION
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Key is not configured in appsettings.json."
    );

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();


var app = builder.Build();

// Keep unhandled database/provider details in server logs, never in React responses.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiErrors");
    logger.LogError(exception, "Unhandled API request failure. TraceId={TraceId} Method={Method} Path={Path}",
        context.TraceIdentifier, context.Request.Method, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        message = "The request could not be completed. Check the API logs using the trace ID.",
        traceId = context.TraceIdentifier
    });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Keep the legacy schema compatibility and admin-seeding step enabled in
// non-Development environments, but opt-in only for local Development runs.
// This lets a local API serve manually prepared databases without changing
// their schema or records during startup.
var runLegacyStartupDatabaseInitialization = !readOnlyPurchasePreviewMode
    && (!app.Environment.IsDevelopment()
        || builder.Configuration.GetValue<bool>("Startup:RunLegacyDatabaseInitialization"));

if (runLegacyStartupDatabaseInitialization)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Keep existing SQL databases compatible with newly added stock fields.
    db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH('dbo.StockItems_tbl', 'OpeningStock') IS NULL
        BEGIN
            ALTER TABLE dbo.StockItems_tbl
            ADD OpeningStock decimal(18, 2) NOT NULL
                CONSTRAINT DF_StockItems_tbl_OpeningStock DEFAULT (0);
        END;

        IF COL_LENGTH('dbo.StockItems_tbl', 'OpeningRate') IS NULL
        BEGIN
            ALTER TABLE dbo.StockItems_tbl
            ADD OpeningRate decimal(18, 2) NOT NULL
                CONSTRAINT DF_StockItems_tbl_OpeningRate DEFAULT (0);
        END;

        IF COL_LENGTH('dbo.Companies_tbl', 'FinancialYearEnd') IS NULL
        BEGIN
            ALTER TABLE dbo.Companies_tbl
            ADD FinancialYearEnd nvarchar(8) NULL;
        END;

        IF OBJECT_ID('dbo.VoucherLedgerEntries_tbl', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.VoucherLedgerEntries_tbl
            (
                Id int IDENTITY(1,1) NOT NULL
                    CONSTRAINT PK_VoucherLedgerEntries_tbl PRIMARY KEY,
                VoucherId int NOT NULL,
                LedgerId int NULL,
                LedgerName nvarchar(250) NOT NULL,
                Amount decimal(18,4) NOT NULL,
                EntryIndex int NOT NULL,
                CONSTRAINT FK_VoucherLedgerEntries_tbl_Vouchers_tbl
                    FOREIGN KEY (VoucherId)
                    REFERENCES dbo.Vouchers_tbl(Id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IX_VoucherLedgerEntries_Ledger_Voucher
                ON dbo.VoucherLedgerEntries_tbl (LedgerId, VoucherId);
        END;
        """);

    if (!db.Users.Any(u => u.Username == "admin"))
    {
        db.Users.Add(new TallyWebAPI.Models.User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123"),
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();
    }
}


// ==========================================
// DEVELOPMENT
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


// ==========================================
// HTTP PIPELINE
// ==========================================

app.UseHttpsRedirection();

// React -> API
app.UseCors("Frontend");

// JWT
app.UseAuthentication();
app.UseAuthorization();

// This mode is intended for an isolated, read-only Purchase preview. It prevents
// startup schema/seed work, background synchronization, and all API routes that
// could write SQL data. Login and the Purchase preview endpoint only read SQL.
if (readOnlyPurchasePreviewMode)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var allowed = (HttpMethods.IsPost(context.Request.Method)
                       && (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                           || path.Equals("/api/purchase-register/import/preview", StringComparison.OrdinalIgnoreCase)))
            || (HttpMethods.IsGet(context.Request.Method)
                && (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)));

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("This backend is running in read-only Purchase preview mode.");
            return;
        }

        await next();
    });
}

app.MapControllers();

app.Run();
