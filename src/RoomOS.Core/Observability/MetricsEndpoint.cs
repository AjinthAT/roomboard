using System.Globalization;
using System.Text;
using RoomOS.Core.Agents;
using RoomOS.Core.State;
using RoomOS.Domain.Contracts;

namespace RoomOS.Core.Observability;

/// <summary>
/// Export Prometheus, écrit à la main.
/// </summary>
/// <remarks>
/// <para>
/// Aucune bibliothèque : le format texte de Prometheus est une ligne par échantillon,
/// et nos métriques sont une poignée de jauges déjà présentes en mémoire. Une
/// dépendance apporterait ici des compteurs, des histogrammes et un registre global
/// dont le projet n'a aucun usage.
/// </para>
/// <para>
/// Ce sont bien les <em>mêmes</em> données que l'UI, lues au même endroit : la
/// télémétrie ne va pas en base (ADR D6), et Prometheus la récupère en scrutant cette
/// route. C'est lui qui garde l'historique, pas RoomOS.
/// </para>
/// </remarks>
public static class MetricsEndpoint
{
    public static IEndpointRouteBuilder MapMetricsEndpoint(this IEndpointRouteBuilder app)
    {
        // Sans authentification, comme /healthz : Prometheus ne porte pas de jeton, et
        // la route n'expose aucun secret. Elle n'est joignable que depuis le LAN.
        app.MapGet("/metrics", (StateStore state, AgentRegistry registry) =>
            Results.Text(Render(state, registry), "text/plain; version=0.0.4"));

        return app;
    }

    private static string Render(StateStore state, AgentRegistry registry)
    {
        var sb = new StringBuilder();

        foreach (var (pcId, pc) in state.Pcs)
        {
            Gauge(sb, "roomos_pc_online", "PC joignable, d'après la connexion de son agent",
                pc.Online ? 1 : 0, ("pc", pcId));

            if (pc.Uptime is { } uptime)
            {
                Gauge(sb, "roomos_pc_uptime_seconds", "Durée depuis le démarrage du PC",
                    (long)uptime.TotalSeconds, ("pc", pcId));
            }

            if (pc.Telemetry is { } t)
            {
                RenderTelemetry(sb, pcId, t);
            }
        }

        foreach (var (deviceId, light) in state.Lights)
        {
            Gauge(sb, "roomos_light_on", "Lampe allumée", light.On ? 1 : 0, ("light", deviceId));
            Gauge(sb, "roomos_light_reachable", "Lampe joignable",
                light.Reachable ? 1 : 0, ("light", deviceId));

            if (light.Brightness is { } brightness)
            {
                Gauge(sb, "roomos_light_brightness_percent", "Luminosité",
                    brightness, ("light", deviceId));
            }

            // Qualité du lien radio : trop instable pour l'écran, précieuse sur un
            // graphique. C'est exactement ce que Prometheus sait faire et pas le hub.
            if (light.LinkQuality is { } lqi)
            {
                Gauge(sb, "roomos_light_link_quality", "Qualité du lien Zigbee, 0 à 255",
                    lqi, ("light", deviceId));
            }
        }

        Gauge(sb, "roomos_music_playing", "Lecture Spotify en cours",
            state.Music.NowPlaying.IsPlaying ? 1 : 0);

        Gauge(sb, "roomos_agents_connected", "Agents connectés au hub",
            state.Pcs.Count(p => registry.IsOnline(p.Key)));

        return sb.ToString();
    }

    private static void RenderTelemetry(StringBuilder sb, string pcId, Telemetry t)
    {
        Gauge(sb, "roomos_cpu_usage_percent", "Charge CPU", t.CpuUsage, ("pc", pcId));
        Gauge(sb, "roomos_gpu_usage_percent", "Charge GPU", t.GpuUsage, ("pc", pcId));
        Gauge(sb, "roomos_ram_used_megabytes", "RAM utilisée", t.RamUsedMb, ("pc", pcId));
        Gauge(sb, "roomos_ram_total_megabytes", "RAM totale", t.RamTotalMb, ("pc", pcId));

        // Les températures sont nullables de bout en bout : un capteur absent ne doit
        // pas produire un échantillon à zéro, que Grafana tracerait comme une valeur.
        if (t.CpuTempC is { } cpuTemp)
        {
            Gauge(sb, "roomos_cpu_temperature_celsius", "Température CPU", cpuTemp, ("pc", pcId));
        }

        if (t.GpuTempC is { } gpuTemp)
        {
            Gauge(sb, "roomos_gpu_temperature_celsius", "Température GPU", gpuTemp, ("pc", pcId));
        }

        if (t.VramUsedMb is { } vramUsed)
        {
            Gauge(sb, "roomos_vram_used_megabytes", "VRAM utilisée", vramUsed, ("pc", pcId));
        }

        if (t.VramTotalMb is { } vramTotal)
        {
            Gauge(sb, "roomos_vram_total_megabytes", "VRAM totale", vramTotal, ("pc", pcId));
        }
    }

    /// <summary>
    /// Une jauge, en-têtes compris. Les en-têtes sont répétés par appel : avec une
    /// vingtaine d'échantillons, dédupliquer coûterait plus de code que d'octets.
    /// </summary>
    private static void Gauge(
        StringBuilder sb, string name, string help, double value, params (string Key, string Value)[] labels)
    {
        sb.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        sb.Append("# TYPE ").Append(name).AppendLine(" gauge");
        sb.Append(name);

        if (labels.Length > 0)
        {
            sb.Append('{');
            for (var i = 0; i < labels.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(labels[i].Key).Append("=\"").Append(Escape(labels[i].Value)).Append('"');
            }

            sb.Append('}');
        }

        sb.Append(' ').AppendLine(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    /// <summary>Un identifiant d'appareil vient de la base : il pourrait contenir une quote.</summary>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
