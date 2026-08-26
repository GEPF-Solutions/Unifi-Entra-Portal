namespace Unifi_Entra_Portal.Server.Infrastructure;

/// <summary>
/// Controls whether X-Forwarded-For/X-Forwarded-Proto headers are trusted
/// from any direct connection to the app. Bound from the "ForwardedHeaders"
/// configuration section.
/// </summary>
public class ForwardedHeadersSettings
{
    /// <summary>
    /// Set true only when this app is guaranteed to be reachable
    /// exclusively through a single trusted reverse proxy/ingress that
    /// strips any client-supplied X-Forwarded-* headers before forwarding
    /// its own (e.g. an OpenShift Route, most Kubernetes Ingress
    /// controllers). Leave false (the default) when the app is exposed
    /// directly, or reachable by a path that isn't guaranteed to strip
    /// these headers — otherwise a client could spoof
    /// "X-Forwarded-Proto: https" to make the app treat an insecure
    /// connection as already HTTPS.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}
