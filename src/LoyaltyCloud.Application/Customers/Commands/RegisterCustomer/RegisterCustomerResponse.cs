namespace LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;

/// <param name="SerialNumber">Identificador imprimible/escaneable de la nueva tarjeta.</param>
/// <param name="PassDownloadUrl">URL para descargar el archivo .pkpass (válida ~15 min).</param>
/// <param name="CurrentPoints">Saldo tras aplicar el bono de bienvenida.</param>
/// <param name="Level">Nivel resultante (siempre Mist en el alta — el bono está debajo del umbral Glow).</param>
public sealed record RegisterCustomerResponse(
    string SerialNumber,
    string PassDownloadUrl,
    int CurrentPoints,
    string Level);
