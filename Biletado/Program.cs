// csharp
using Biletado;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Biletado.Repository.Swagger;
using System.Text.Json.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Setup Serilog early so startup logs are captured
var serilogConfig = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .Enrich.WithProperty("environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("machine", System.Environment.MachineName);

Log.Logger = serilogConfig.CreateLogger();

// integrate Serilog into generic host logging
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations(); // erlaubt [SwaggerOperation], [SwaggerParameter], [SwaggerSchema]
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // register custom OperationFilter / SchemaFilter
    c.OperationFilter<ReservationsOperationFilter>();
    c.SchemaFilter<EnumSchemaFilter>();
    c.OperationFilter<UuidParameterFilter>();

    // Keine Security-Definitionen — Auth wurde entfernt
});

// Configure JSON serializer to use string enums
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var isDevelopment = builder.Environment.IsDevelopment();

// Databases
builder.Services.AddDbContext<AssetsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AssetsConnection")));

{
    var reservationsConn = builder.Configuration.GetConnectionString("ReservationConnection");
    if (string.IsNullOrWhiteSpace(reservationsConn))
    {
        throw new InvalidOperationException("Connection string `ReservationConnection` is not configured.");
    }
    builder.Services.AddDbContext<ReservationsDbContext>(options =>
        options
            .UseNpgsql(reservationsConn)
            .LogTo(message => Console.WriteLine(message), LogLevel.Information)
            .EnableSensitiveDataLogging()
    );
}

var app = builder.Build();

// Swagger in Development
if (isDevelopment)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
