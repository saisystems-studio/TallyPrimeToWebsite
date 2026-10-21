using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TallyWebAPI.Data;
using TallyWebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddHttpClient<TallyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<VoucherService>();
builder.Services.AddHttpClient<SalesReportService>();
builder.Services.AddHttpClient<LedgerReportService>();
builder.Services.AddHttpClient<StockSummaryService>();
builder.Services.AddScoped<StockSummarySnapshotService>();
builder.Services.AddScoped<LedgerReportSnapshotService>();
builder.Services.AddHttpClient<OutstandingService>();
builder.Services.AddScoped<StockItemSyncService>();
builder.Services.AddScoped<CompanySyncService>();
builder.Services.AddScoped<LedgerSyncService>();
builder.Services.AddScoped<VoucherSyncService>();


// Enable automatic Tally sync only outside Development
if (!builder.Environment.IsDevelopment())
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


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Keep the legacy schema compatibility and admin-seeding step enabled in
// non-Development environments, but opt-in only for local Development runs.
// This lets a local API serve manually prepared databases without changing
// their schema or records during startup.
var runLegacyStartupDatabaseInitialization = !app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Startup:RunLegacyDatabaseInitialization");

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

app.MapControllers();

app.Run();
