using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Management;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Elsa.MassTransit.Options;
using Elsa.Extensions;
using Elsa.ServerAndStudio.Web.Extensions;
using Elsa.Hosting.Management.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Proto.Persistence.Sqlite;

const bool useMassTransit = true;
const bool useProtoActor = false;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
var services = builder.Services;
var configuration = builder.Configuration;
var sqliteConnectionString = configuration.GetConnectionString("Sqlite")!;
var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMq")!;
var azureServiceBusConnectionString = configuration.GetConnectionString("AzureServiceBus")!;
var identitySection = configuration.GetSection("Identity");
var identityTokenSection = identitySection.GetSection("Tokens");
var massTransitSection = configuration.GetSection("MassTransit");
var heartbeatSection = configuration.GetSection("Heartbeat");

services.Configure<MassTransitWorkflowDispatcherOptions>(massTransitSection);

// Add Elsa services.
services
    .AddElsa(elsa =>
    {
        elsa
            .UseSasTokens()
            .UseIdentity(identity =>
            {
                identity.IdentityOptions = options => identitySection.Bind(options);
                identity.TokenOptions = options => identityTokenSection.Bind(options);
                identity.UseConfigurationBasedUserProvider(options => identitySection.Bind(options));
                identity.UseConfigurationBasedApplicationProvider(options => identitySection.Bind(options));
                identity.UseConfigurationBasedRoleProvider(options => identitySection.Bind(options));
            })
            .UseDefaultAuthentication()
            .UseInstanceManagement(x => x.HeartbeatOptions = settings => heartbeatSection.Bind(settings))
            .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite(sqliteConnectionString)))
            .UseWorkflowRuntime(runtime =>
            {
                runtime.UseEntityFrameworkCore(ef => ef.UseSqlite(sqliteConnectionString));
                runtime.UseMassTransitDispatcher();
                if (useProtoActor)
                {
                    runtime.UseProtoActor(proto => proto.PersistenceProvider = _ =>
                    {
                        return new SqliteProvider(new SqliteConnectionStringBuilder(sqliteConnectionString));
                    });
                }
            })
            .UseScheduling()
            .UseJavaScript(options => options.AllowClrAccess = true)
            .UseLiquid()
            .UseCSharp()
            .UsePython()
            .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
            .UseEmail(email => email.ConfigureOptions = options => configuration.GetSection("Smtp").Bind(options))
            .UseWebhooks(webhooks => webhooks.WebhookOptions = options => builder.Configuration.GetSection("Webhooks").Bind(options))
            .UseWorkflowsApi()
            .UseRealTimeWorkflows()
            .AddActivitiesFrom<Program>()
            .AddWorkflowsFrom<Program>();

        if (useMassTransit)
        {
            elsa.UseMassTransit(massTransit =>
                {
                    // massTransit.UseAzureServiceBus(azureServiceBusConnectionString, serviceBusFeature => serviceBusFeature.ConfigureServiceBus = bus =>
                    // {
                    //     bus.PrefetchCount = 4;
                    //     bus.LockDuration = TimeSpan.FromMinutes(5);
                    //     bus.MaxConcurrentCalls = 32;
                    //     bus.MaxDeliveryCount = 8;
                    //     // etc.
                    // });
                    // massTransit.UseRabbitMq(rabbitMqConnectionString, rabbit => rabbit.ConfigureServiceBus = bus =>
                    //     {
                    //         bus.PrefetchCount = 4;
                    //         bus.Durable = true;
                    //         bus.AutoDelete = false;
                    //         bus.ConcurrentMessageLimit = 32;
                    //         // etc.
                    //     }))
                }
            );
        }
    });

services.AddHealthChecks();

services.AddCors(cors => cors.Configure(configuration.GetSection("CorsPolicy")));

// Razor Pages.
services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));

// Configure middleware pipeline.
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.MapHealthChecks("/health");
app.UseRouting();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseWorkflowsApi();
app.UseWorkflows();
app.UseWorkflowsSignalRHubs();
app.MapFallbackToPage("/_Host");
app.Run();