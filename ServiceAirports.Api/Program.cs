using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ServiceAirports.Api.Data;
using ServiceAirports.Api.Services;

using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddSimpleConsole();
});
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Bootstrap");

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is Exception exception)
    {
        bootstrapLogger.LogCritical(
            exception,
            "Unhandled AppDomain exception. IsTerminating: {IsTerminating}",
            eventArgs.IsTerminating);

        return;
    }

    bootstrapLogger.LogCritical(
        "Unhandled AppDomain exception object: {ExceptionObject}. IsTerminating: {IsTerminating}",
        eventArgs.ExceptionObject,
        eventArgs.IsTerminating);
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    bootstrapLogger.LogError(eventArgs.Exception, "Unobserved task exception.");
    eventArgs.SetObserved();
};

try
{
    var builder = WebApplication.CreateBuilder(args);
    var useDatabase = builder.Configuration.GetValue<bool>("DataSource:UseDatabase");
    var serviceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName")
        ?? builder.Environment.ApplicationName;
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString();
    var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");
    var useConsoleExporter = builder.Configuration.GetValue<bool>("OpenTelemetry:UseConsoleExporter");
    var azureMonitorConnectionString = builder.Configuration.GetValue<string>("AzureMonitor:ConnectionString");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion));

        if (useConsoleExporter)
        {
            logging.AddConsoleExporter();
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

    var openTelemetry = builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion));

    if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
    {
        openTelemetry.UseAzureMonitor(options =>
        {
            options.ConnectionString = azureMonitorConnectionString;
        });
    }

    openTelemetry.WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddHttpClientInstrumentation();

        if (useConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (useConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

    var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, serverVersion));

    builder.Services.AddScoped<IAirportService, AirportService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionHandlerFeature?.Error;
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            if (exception is not null)
            {
                logger.LogError(
                    exception,
                    "Unhandled HTTP exception. TraceId: {TraceId}; Method: {Method}; Path: {Path}; QueryString: {QueryString}",
                    traceId,
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString);
            }
            else
            {
                logger.LogError(
                    "Exception handler executed without exception details. TraceId: {TraceId}; Method: {Method}; Path: {Path}; QueryString: {QueryString}",
                    traceId,
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = StatusCodes.Status500InternalServerError,
                message = "An unexpected error occurred.",
                traceId,
                detail = app.Environment.IsDevelopment()
                    ? exception?.ToString()
                    : null
            });
        });
    });

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        if (useDatabase)
        {
            try
            {
                app.Logger.LogInformation("Applying database migrations and seed data.");

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.MigrateAsync();
                await ApplicationDbContextSeed.SeedAsync(dbContext);

                app.Logger.LogInformation("Database migrations and seed data completed.");
            }
            catch (Exception exception)
            {
                app.Logger.LogCritical(exception, "Database migration or seed failed during application startup.");
                throw;
            }
        }
    }

    await app.RunAsync();
}
catch (Exception exception)
{
    bootstrapLogger.LogCritical(exception, "Application terminated unexpectedly.");
    throw;
}
