using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
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
            builder.Services.Configure<ForwardedHeadersSettings>(builder.Configuration.GetSection("ForwardedHeaders"));
            builder.Services.Configure<GuestAgbSettings>(builder.Configuration.GetSection("GuestAgb"));
            builder.Services.Configure<PortalBrandingSettings>(builder.Configuration.GetSection("PortalBranding"));

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
            builder.Services.AddHealthChecks();

            // GuestController's routes are deliberately anonymous — unlike
            // PortalController, there's no Entra sign-in to naturally cap
            // request volume. Without this, anyone on the internet could
            // hammer /api/guest/authorize with guessed MACs; this doesn't
            // close that off entirely, but it blunts automated abuse.
            // Keyed on RemoteIpAddress, which by this point already
            // reflects the real client IP behind a trusted proxy (see
            // UseForwardedHeaders below, which runs before routing).
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("guest", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            });

            // Singleton: both own a single long-lived HttpClient (built once
            // in their constructor) so guest sign-ins reuse pooled
            // connections to UniFi/Graph instead of paying a fresh TCP/TLS
            // handshake per request. Both only depend on IOptions<T> and
            // ILogger<T>, which are singleton-safe.
            builder.Services.AddSingleton<IUniFiClientService, UniFiClientService>();
            builder.Services.AddSingleton<IGraphTokenProvider, MsalGraphTokenProvider>();
            builder.Services.AddSingleton<IGuestEligibilityService, GraphGuestEligibilityService>();

            builder.Services.AddScoped<IGatingService, GatingService>();
            builder.Services.AddScoped<IAuthorizedGuestRepository, AuthorizedGuestRepository>();
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

            // Must run first, before any middleware that inspects the
            // request scheme/host (HTTPS redirection, the OIDC handler's
            // redirect_uri construction). A reverse proxy/ingress that
            // terminates TLS at the edge (an OpenShift Route, most
            // Kubernetes Ingress controllers) forwards plain HTTP to this
            // app, so without this the app thinks every request is HTTP —
            // causing an HTTPS-redirect loop and an OIDC reply URL that
            // doesn't match what's registered in Entra.
            //
            // ForwardedHeaders:TrustAllProxies must be explicitly enabled
            // (e.g. via an env var in that deployment) for KnownIPNetworks/
            // KnownProxies to be cleared. This project is self-hosted by
            // other orgs in arbitrary topologies, so trusting these headers
            // unconditionally by default would let any client spoof
            // "X-Forwarded-Proto: https" and bypass UseHttpsRedirection on
            // a deployment that exposes Kestrel directly or sits behind a
            // proxy that doesn't strip client-supplied headers.
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            };
            if (app.Services.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value.TrustAllProxies)
            {
                forwardedHeadersOptions.KnownIPNetworks.Clear();
                forwardedHeadersOptions.KnownProxies.Clear();
            }

            app.UseForwardedHeaders(forwardedHeadersOptions);

            using (var startupScope = app.Services.CreateScope())
            {
                startupScope.ServiceProvider.GetRequiredService<PortalDbContext>().Database.Migrate();
            }

            app.UseDefaultFiles();
            app.MapStaticAssets();

            // Serves operator-supplied branding assets (logo, hero image)
            // from a physical folder outside the published wwwroot, so a
            // container image with no branding baked in can have a
            // deployment mount its own logo/hero over this path (e.g. a
            // Kubernetes ConfigMap/volume) without rebuilding the image.
            // PortalBrandingSettings.LogoPath/HeroImagePath are the
            // resulting "/branding/..." URLs. Relative AssetsPath is
            // resolved against the content root, matching how the SQLite
            // data directory is resolved above.
            var brandingSettings = app.Services.GetRequiredService<IOptions<PortalBrandingSettings>>().Value;
            var brandingAssetsPath = Path.IsPathRooted(brandingSettings.AssetsPath)
                ? brandingSettings.AssetsPath
                : Path.Combine(app.Environment.ContentRootPath, brandingSettings.AssetsPath);
            Directory.CreateDirectory(brandingAssetsPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(brandingAssetsPath),
                RequestPath = "/branding",
            });

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();

            app.MapHealthChecks("/healthz");

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
