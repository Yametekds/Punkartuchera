namespace Punkartuchera.Relay;

/// <summary>
/// Configuración del relé (sección <c>Relay</c>; en Azure, ajustes <c>Relay__Clave</c>).
/// En Azure se usan los <c>*Endpoint</c> con la identidad administrada de la Function App, sin claves.
/// En local se usan las cadenas de conexión (Azurite y una instancia de SignalR de pruebas).
/// </summary>
public sealed class RelayOptions
{
    public const string Section = "Relay";

    /// <summary>Ej.: https://cuenta.table.core.windows.net</summary>
    public string? TablesEndpoint { get; set; }

    public string? TablesConnectionString { get; set; }

    public string TableName { get; set; } = "partidas";

    /// <summary>Ej.: https://punkartuchera.service.signalr.net</summary>
    public string? SignalREndpoint { get; set; }

    public string? SignalRConnectionString { get; set; }
}
