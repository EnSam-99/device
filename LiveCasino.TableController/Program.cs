using System.Net.Http;
using Grpc.Core;
using LiveCasino.TableController.Models;
using LiveCasino.TableController.Protos;
using LiveCasino.TableController.Services;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace LiveCasino.TableController
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.Configure<SerialPortOptions>(builder.Configuration.GetSection("SerialPort"));
            builder.Services.Configure<BackendClientOptions>(builder.Configuration.GetSection("Backend"));

            builder.Services.AddSingleton<ScannerStatus>();
            builder.Services.AddSingleton<IScanDispatcher, GrpcScanDispatcher>();
            builder.Services.AddHostedService<SerialScannerService>();

            builder.Services
                .AddGrpcClient<ScannerBackend.ScannerBackendClient>((provider, options) =>
                {
                    var backendOptions = provider.GetRequiredService<IOptions<BackendClientOptions>>().Value;
                    options.Address = new Uri(backendOptions.Endpoint);
                })
                .AddPolicyHandler(GetGrpcRetryPolicy(builder.Configuration));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            MapScannerEndpoints(app);

            app.Run();
        }

        private static IAsyncPolicy<HttpResponseMessage> GetGrpcRetryPolicy(IConfiguration configuration)
        {
            var options = configuration.GetSection("Backend").Get<BackendClientOptions>() ?? new BackendClientOptions();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(response => !response.IsSuccessStatusCode)
                .Or<Grpc.Core.RpcException>()
                .WaitAndRetryAsync(options.RetryCount,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt) * options.BaseRetryDelaySeconds));
        }

        private static void MapScannerEndpoints(WebApplication app)
        {
            app.MapGet("/scanner/status", (ScannerStatus status) => Results.Ok(new
            {
                hardwareAvailable = status.HardwareAvailable
            }));

            app.MapPost("/scanner/simulate", async (
                SimulatedScanRequest request,
                ScannerStatus status,
                IOptions<SerialPortOptions> serialOptions,
                IScanDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Payload))
                {
                    return Results.BadRequest(new { message = "Payload is required" });
                }

                var options = serialOptions.Value;
                if (!status.HardwareAvailable && !options.AllowMockWhenUnavailable)
                {
                    return Results.BadRequest(new { message = "Mocking scans is disabled while the scanner is offline." });
                }

                var source = string.IsNullOrWhiteSpace(request.Source) ? "simulated" : request.Source.Trim();
                var dispatchResult = await dispatcher.DispatchAsync(request.Payload.Trim(), source, cancellationToken);

                return Results.Ok(new
                {
                    dispatchResult.Accepted,
                    dispatchResult.Message,
                    source,
                    mocked = !status.HardwareAvailable
                });
            }).DisableAntiforgery();
        }
    }
}
