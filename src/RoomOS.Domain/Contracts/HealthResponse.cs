namespace RoomOS.Domain.Contracts;

/// <summary>Réponse de <c>GET /healthz</c>.</summary>
public sealed record HealthResponse(string Status, string Version, DateTimeOffset ServerTime);
