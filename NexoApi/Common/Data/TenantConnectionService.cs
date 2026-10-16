using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace NexoApi.Common.Data;

/// <summary>
/// Singleton que resuelve el connection string de un cliente NEXO
/// consultando admin_services en 45.171.180.181 y cacheando el resultado.
/// El campo services.home está cifrado con AES-256-CBC (IV=zeros, clave=SHA256 de la llave maestra).
/// </summary>
public class TenantConnectionService
{
    private const int NexoServiceId = 13;
    private const string ClaveMaestra = "S0LUC10N3S-900519299-515";

    private readonly string _adminConnectionString;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TenantConnectionService(IConfiguration configuration)
    {
        _adminConnectionString = configuration.GetConnectionString("AdminDb")
            ?? throw new InvalidOperationException("Falta 'AdminDb' en ConnectionStrings.");
    }

    public async Task<string> ResolveAsync(string subdomain)
    {
        if (_cache.TryGetValue(subdomain, out var cached))
            return cached;

        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(subdomain, out cached))
                return cached;

            using var conn = new SqlConnection(_adminConnectionString);

            var row = await conn.QuerySingleOrDefaultAsync(
                @"SELECT CS.subdomain_name,
                         CS.active_service,
                         C.locked,
                         S.home
                  FROM Companie_Services CS
                  JOIN co_companies C ON CS.companie_id = C.id
                  JOIN services     S ON CS.service_id  = S.id_Service
                  WHERE CS.service_id = @ServiceId
                    AND LOWER(CS.subdomain_name) = LOWER(@Subdomain)",
                new { ServiceId = NexoServiceId, Subdomain = subdomain });

            if (row is null)
                throw new UnauthorizedAccessException(
                    $"El subdominio '{subdomain}' no está registrado en el sistema (service_id={NexoServiceId}).");

            if (Convert.ToInt32(row.active_service) == 0)
                throw new UnauthorizedAccessException(
                    $"El cliente '{subdomain}' tiene NEXO registrado pero el servicio está inactivo (active_service=0).");

            if (Convert.ToInt32(row.locked) != 0)
                throw new UnauthorizedAccessException(
                    $"El cliente '{subdomain}' existe pero la empresa está bloqueada (locked=1).");

            var connStr = ConstruirConnectionString((string)row.subdomain_name, (string)row.home);
            _cache[subdomain] = connStr;
            return connStr;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ConstruirConnectionString(string dbName, string homeEncriptado)
    {
        // Descifrar home: AES-256-CBC, IV=zeros, clave=SHA256("S0LUC10N3S-...")
        // Formato plaintext: server|user|password|...
        var partes = DescifrarHome(homeEncriptado);

        var server   = partes[0]; // e.g. "45.171.180.182\VISIONS"
        var user     = partes[1];
        var password = partes[2];

        return new SqlConnectionStringBuilder
        {
            DataSource         = server,
            InitialCatalog     = dbName,
            UserID             = user,
            Password           = password,
            TrustServerCertificate = true
        }.ConnectionString;
    }

    private static string[] DescifrarHome(string homeEncriptado)
    {
        using var sha = SHA256.Create();
        var keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ClaveMaestra));

        using var aes = Aes.Create();
        aes.Key     = keyBytes;
        aes.IV      = new byte[16]; // zeros
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var encrypted = Convert.FromBase64String(homeEncriptado);
        using var ms = new MemoryStream(encrypted);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        var plain = sr.ReadToEnd();

        return plain.Split('|');
    }

    public void InvalidarCache(string subdomain) => _cache.Remove(subdomain);
}
