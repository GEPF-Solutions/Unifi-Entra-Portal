using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Unifi_Entra_Portal.Server.DbModel;
using Unifi_Entra_Portal.Server.Infrastructure;
using Unifi_Entra_Portal.Server.Repository;
using Unifi_Entra_Portal.Server.Repository.Abstractions;
using Unifi_Entra_Portal.Server.Services;
using Unifi_Entra_Portal.Server.Services.Abstractions;

namespace Unifi_Entra_Portal.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

            builder.Services.Configure<UniFiSettings>(builder.Configuration.GetSection("UniFi"));
            builder.Services.Configure<GatingSettings>(builder.Configuration.GetSection("Gating"));
            builder.Services.Configure<RevalidationSettings>(builder.Configuration.GetSection("Revalidation"));
            builder.Services.Configure<AzureAdCredentialsSettings>(builder.Configuration.GetSection("AzureAd"));

            builder.Services
                .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

            // The SPA calls /api/* endpoints via fetch and expects plain
            // status codes, not the cookie handler's default HTML redirect
            // to the Microsoft sign-in / access-denied page.
            builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddScoped<IUniFiClientService, UniFiClientService>();
            builder.Services.AddScoped<IGatingService, GatingService>();
            builder.Services.AddScoped<IAuthorizedGuestRepository, AuthorizedGuestRepository>();
            builder.Services.AddSingleton<IGraphTokenProvider, MsalGraphTokenProvider>();
            builder.Services.AddScoped<IGuestEligibilityService, GraphGuestEligibilityService>();
            builder.Services.AddHostedService<GuestRevalidationBackgroundService>();

            // Resolve the SQLite file against ContentRootPath explicitly
            // rather than a relative "Data Source" path — a relative path
            // is resolved against the process's current working directory,
            // which is NOT guaranteed to match the content root (e.g. it
            // doesn't under WebApplicationFactory's test host).
            var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
            Directory.CreateDirectory(dataDirectory);
            var sqliteConnectionString = builder.Configuration.GetConnectionString("Portal")
                ?? $"Data Source={Path.Combine(dataDirectory, "portal.db")}";

            builder.Services.AddDbContext<PortalDbContext>(options => options.UseSqlite(sqliteConnectionString));

            var app = builder.Build();

            using (var startupScope = app.Services.CreateScope())
            {
                startupScope.ServiceProvider.GetRequiredService<PortalDbContext>().Database.Migrate();
            }

            app.UseDefaultFiles();
            app.MapStaticAssets();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
