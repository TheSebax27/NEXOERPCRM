using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using NexoApi.Common.Data;
using NexoApi.Features.Catalogo;
using NexoApi.Features.Catalogo.Dtos;
using NexoApi.Features.Integracion.Dtos;
using Microsoft.Extensions.Configuration;

namespace NexoApi.Features.Integracion;

public interface IIntegracionService
{
    Task<IEnumerable<EventoPendienteItem>> ObtenerEventosPendientesAsync(int centroCostoId);
    Task ConfirmarEventoSalienteAsync(long eventoId, int centroCostoId);
    Task RegistrarEventoEntranteAsync(RegistrarEventoEntranteRequest request, int centroCostoId);
    Task<GenerarApiKeyResponse> GenerarApiKeyAsync(GenerarApiKeyRequest request);
    Task<ConfiguracionAgenteResponse> ObtenerConfiguracionAgenteAsync(int centroCostoId);
    Task<IEnumerable<MapeoArticuloItem>> ListarMapeosAsync(int centroCostoId);
    Task CrearMapeoAsync(CrearMapeoArticuloRequest request);
    Task<IEnumerable<ArticuloPendienteMapeoItem>> ListarArticulosPendientesMapeoAsync(int centroCostoId);
    Task ResolverArticuloPendienteAsync(int pendienteId, ResolverArticuloPendienteRequest request);
    Task<ResolverTodosResponse> ResolverTodosAsync(int centroCostoId);
    Task<LatidoResponse> RegistrarLatidoAsync(int centroCostoId, string? versionAgente = null);
    Task<IEnumerable<EstadoIntegracionResponse>> ObtenerEstadoIntegracionAsync();
    Task<IEnumerable<ProgresoSyncResponse>> ObtenerProgresoSyncAsync();
    Task RegistrarFalloEventoAsync(long eventoId, int centroCostoId, string mensajeError);
    Task<ConfiguracionAgenteCompletaResponse> ObtenerConfiguracionCompletaAsync(int agenteSyncId);
    Task ActualizarConfiguracionCompletaAsync(int agenteSyncId, ActualizarConfiguracionAgenteRequest request);
    string GenerarAppsettingsJson(ConfiguracionAgenteCompletaResponse config, string apiKey);
    Task<string> PrepararAppsettingsConKeyFrescaAsync(int agenteSyncId);
    Task<string> PrepararDescargaInstaladorAsync(int agenteSyncId);
    byte[]? ObtenerPaqueteInstalador(string token);
    Task DesactivarAgenteAsync(int agenteSyncId);
    Task<ActividadAgenteResponse> ObtenerActividadAsync(int agenteSyncId);
    Task<int> ReintentarPendientesAsync(int agenteSyncId);
    Task<int> ReprocesarEntrantesAsync(int agenteSyncId);

    // Catalogos bidireccionales
    Task<IEnumerable<MarcaSyncItem>> ListarMarcasSyncAsync();
    Task UpsertMarcaAsync(MarcaSyncItem item);
    Task<IEnumerable<GrupoMayorSyncItem>> ListarGruposMayorSyncAsync();
    Task UpsertGrupoMayorAsync(GrupoMayorSyncItem item);
    Task<IEnumerable<GrupoMenorSyncItem>> ListarGruposMenorSyncAsync();
    Task UpsertGrupoMenorAsync(GrupoMenorSyncItem item);
    Task<IEnumerable<IvaSyncItem>> ListarIvaSyncAsync();
    Task UpsertIvaAsync(IvaSyncItem item);
    Task<IEnumerable<PresentacionSyncItem>> ListarPresentacionesSyncAsync();
    Task UpsertPresentacionAsync(PresentacionSyncItem item);

    // Facturas NEXO → Visions
    Task<IEnumerable<FacturaParaVisionsItem>> ListarFacturasParaVisionsAsync(int centroCostoId);
    Task MarcarFacturaExportadaVisionsAsync(int facturaId, int centroCostoId);

    // Sync bidireccional articulos (Visions → NEXO)
    Task SyncArticuloDesdeVisionsAsync(SyncArticuloDesdeVisionsRequest request);
    Task InactivarArticuloDesdeVisionsAsync(string referencia, string centroCostoVisions);

    // Imagenes para sync NEXO → Visions
    Task<IEnumerable<ArticuloImagenParaSyncDto>> ListarArticulosConImagenActualizadaAsync(DateTime? desde);

    // Clientes para sync NEXO → Visions
    Task<IEnumerable<ClienteParaSyncDto>> ListarClientesParaSyncAsync(DateTime? desde, int centroCostoId);
    // Clientes desde Visions → NEXO
    Task SyncClienteDesdeVisionsAsync(SyncClienteDesdeVisionsRequest request);

    // Proveedores para sync NEXO → Visions
    Task<IEnumerable<ProveedorParaSyncDto>> ListarProveedoresParaSyncAsync(int centroCostoId);
    // Proveedores desde Visions → NEXO
    Task SyncProveedorDesdeVisionsAsync(SyncProveedorDesdeVisionsRequest request);

    // Número de factura asignado por Visions → actualizar NEXO
    Task ActualizarNumeroVisionsAsync(int facturaId, ActualizarNumeroVisionsRequest request);

    // Salud del catálogo sincronizado (totales, completos, sin datos)
    Task<SaludCatalogoResponse> ObtenerSaludCatalogoAsync();

    // Ventas de Visions registradas en EventosEntrantes (solo lectura, sin conexión a Visions)
    Task<VentasVisionsPaginadasResponse> ListarVentasVisionsAsync(int? centroCostoId, string? tipDoc, DateTime? desde, DateTime? hasta, int pagina = 1, int tamano = 50);
    Task<IEnumerable<VentaVisionsLineaItem>> ListarLineasVentaVisionsAsync(int centroCostoId, string tipDoc, string nroDoc);

    // Limpieza de staging en Visions para entidades eliminadas en NEXO
    Task<IEnumerable<PendienteLimpiezaVisions>> ListarPendientesLimpiezaVisionsAsync();
    Task MarcarLimpiezaVisionsCompletadaAsync(int limpiezaId);

    // SINCANTSA en Visions: bloquear ventas sin inventario
    Task<bool> ObtenerSincAntsaAsync(int agenteSyncId);
    Task SetSincAntsaAsync(int agenteSyncId, bool activo);

    // Catalogo completo NEXO → Visions: genera EventosSalientes para articulos que faltan en este CC
    Task SolicitarSyncCatalogoCompletoAsync(int centroCostoId);

    // Pausa / reanuda sync de Visions para un agente
    Task SetSyncPausadaAsync(int agenteSyncId, bool pausada);

    // Purga datos de prueba: borra EventosEntrantes y resetea stock a 0 (uso pre-go-live)
    Task<int> PurgarVentasTestAsync(int agenteSyncId);

    // Llamado por el agente tras limpiar NEXO_VentasExportadas/NEXO_EntradasExportadas en Visions
    Task ResetPurgaVisionsAsync(int centroCostoId);

    // Reproceso de datos maestros
    Task SolicitarReprocesoAsync(int agenteSyncId, string tipo);
    Task<EstadoReprocesoDto?> ObtenerEstadoReprocesoAsync(int agenteSyncId);
    Task ActualizarProgresoReprocesoAsync(int centroCostoId, int procesados, int totalEstimado, string estado);
    Task ResetBanderasReprocesoAsync(int centroCostoId);
}

public class IntegracionService : IIntegracionService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICatalogoService _catalogoService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<IntegracionService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IntegracionService(IDbConnectionFactory db, ICatalogoService catalogoService, IMemoryCache cache, IConfiguration config, ILogger<IntegracionService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _catalogoService = catalogoService;
        _cache = cache;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IEnumerable<EventoPendienteItem>> ObtenerEventosPendientesAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        // ReferenciaVisions: se resuelve directamente desde Tarjetas.Referencia
        // (misma clave que TARJETA.REFERENCIA en Visions) sin necesidad de
        // pasar por MapeoArticulos -- el mapeo es ahora automatico.
        const string sql = @"
            SELECT e.EventoID, e.TipoEvento, e.Cantidad,
                   -- Para SINCRONIZAR_ARTICULO usar el Costo configurado, no CostoPromedio (que es 0 en articulos nuevos)
                   CASE WHEN e.TipoEvento = 'SINCRONIZAR_ARTICULO' THEN ISNULL(a.Costo, 0)
                        ELSE e.CostoUnitario END AS CostoUnitario,
                   e.FechaCreacion,
                   cc.IdentificadorClienteVisions AS CentroCostoVisions,
                   a.Referencia AS ReferenciaVisions,
                   a.Nombre AS NombreArticulo, a.PPublico AS PrecioVentaArticulo, a.StockMinimo AS StockMinimoArticulo,
                   CAST(NULL AS DECIMAL(18,4)) AS Fracciones, a.PresentacionCodigo, a.MarcaCodigo,
                   CAST(a.Iva2 AS DECIMAL(18,4)) AS Iva2, a.IvaDescripcion2,
                   a.GrupoMenorCodigo,
                   a.IvaSiNo,
                   CAST(a.IvaValor AS DECIMAL(18,4)) AS IvaValor,
                   a.IvaDescripcion,
                   a.PBodega,
                   a.PCredito,
                   a.UPublico,
                   a.UBodega,
                   a.UCredito,
                   ta.Codigo AS TipoProductoCodigo
            FROM Integracion.EventosSalientes e
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = e.ArticuloID
            LEFT JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            WHERE e.Estado = 'PENDIENTE' AND e.CentroCostoID = @CentroCostoId
              AND a.Referencia IS NOT NULL AND a.Referencia <> ''
            ORDER BY e.EventoID
            OFFSET 0 ROWS FETCH NEXT 200 ROWS ONLY";

        return await connection.QueryAsync<EventoPendienteItem>(sql, new { CentroCostoId = centroCostoId });
    }

    public async Task ConfirmarEventoSalienteAsync(long eventoId, int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        // El filtro AND CentroCostoID = @CentroCostoId no es decorativo: es lo
        // que impide que el agente de un cliente pueda confirmar (o espiar)
        // eventos que pertenecen a otro cliente, aunque adivinara un EventoID.
        const string sql = @"
            UPDATE Integracion.EventosSalientes
            SET Estado = 'CONFIRMADO', FechaEnvio = SYSUTCDATETIME()
            WHERE EventoID = @EventoId AND CentroCostoID = @CentroCostoId AND Estado = 'PENDIENTE'";

        var filas = await connection.ExecuteAsync(sql, new { EventoId = eventoId, CentroCostoId = centroCostoId });

        if (filas == 0)
            throw new KeyNotFoundException("El evento no existe, no pertenece a este agente, o ya fue confirmado.");
    }

    private record EventoEntranteExistente(long EventoEntranteID, bool Procesado);

    public async Task RegistrarEventoEntranteAsync(RegistrarEventoEntranteRequest r, int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        var existente = await connection.QuerySingleOrDefaultAsync<EventoEntranteExistente>(
            "SELECT EventoEntranteID, Procesado FROM Integracion.EventosEntrantes WHERE IdEventoExterno = @IdEventoExterno",
            new { r.IdEventoExterno });

        // Ya se proceso exitosamente antes (idempotencia real: el agente
        // reenvio por un corte de internet, por ejemplo) -- no hay nada que hacer.
        if (existente is not null && existente.Procesado)
            return;

        long eventoEntranteId;

        var parametros = new DynamicParameters();

        // ENTRADA_COMPRA = mercancía recibida en Visions (FRMENTRADAS) → stock SUMA.
        // NOTA DEVOLUCION = cliente devuelve mercancía → stock SUMA.
        // Cualquier otro tipo (VENTA) → stock RESTA via sp_ProcesarEventoEntrante.
        var spNombre = r.TipoEvento == "ENTRADA_COMPRA"
            ? "Integracion.sp_ProcesarEntradaCompraVisions"
            : (r.TipDoc?.Contains("DEVOLUCION", StringComparison.OrdinalIgnoreCase) == true)
                ? "Integracion.sp_ProcesarDevolucionVisions"
                : "Integracion.sp_ProcesarEventoEntrante";

        if (existente is not null)
        {
            // Ya existia pero se habia quedado sin procesar (ej. la primera vez
            // fallo por falta de mapeo de articulo) -- NO se vuelve a insertar,
            // se reintenta el mismo EventoEntranteID. Antes de agosto 2026 este
            // caso se trataba igual que "ya procesado" y el evento quedaba
            // atascado para siempre, incluso despues de arreglar el mapeo.
            eventoEntranteId = existente.EventoEntranteID;
        }
        else
        {
            const string sqlInsert = @"
                INSERT INTO Integracion.EventosEntrantes
                    (IdEventoExterno, TipoEvento, CentroCostoID, CodigoArticuloVisions, Cantidad, FechaEventoOrigen,
                     NombreArticuloVisions, CostoArticuloVisions, PrecioArticuloVisions,
                     TipDoc, NroDoc, NitCliente, NombreCliente)
                OUTPUT INSERTED.EventoEntranteID
                VALUES (@IdEventoExterno, @TipoEvento, @CentroCostoId, @CodigoArticuloVisions, @Cantidad, @FechaEventoOrigen,
                        @NombreArticuloVisions, @CostoArticuloVisions, @PrecioArticuloVisions,
                        @TipDoc, @NroDoc, @NitCliente, @NombreCliente)";

            eventoEntranteId = await connection.ExecuteScalarAsync<long>(sqlInsert, new
            {
                r.IdEventoExterno,
                r.TipoEvento,
                CentroCostoId = centroCostoId,
                r.CodigoArticuloVisions,
                r.Cantidad,
                r.FechaEventoOrigen,
                r.NombreArticuloVisions,
                r.CostoArticuloVisions,
                r.PrecioArticuloVisions,
                r.TipDoc,
                r.NroDoc,
                NitCliente    = r.ClienteNit,
                NombreCliente = r.ClienteNombre
            });
        }

        parametros.Add("EventoEntranteID", eventoEntranteId);

        try
        {
            await connection.ExecuteAsync(spNombre, parametros, commandType: CommandType.StoredProcedure);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 54000)
        {
            // Articulo vendido en Visions cuya Referencia no existe en NEXO.
            // Se auto-crea en Catalogo.Tarjetas usando el mismo codigo que Visions
            // usa como REFERENCIA (puede ser SKU alfanumerico o codigo de barras EAN:
            // ambos son validos como Referencia en NEXO). Luego se reintenta el SP,
            // que ahora encontrara el articulo via el LEFT JOIN a Catalogo.Tarjetas.
            var tipoId = await connection.ExecuteScalarAsync<int?>(
                "SELECT TOP 1 TipoArticuloID FROM Catalogo.TiposArticulo WHERE Nombre = 'Producto Terminado'");

            if (tipoId is null)
            {
                _logger.LogWarning("No se encontro TipoArticulo 'Producto Terminado'; articulo {Codigo} queda sin procesar", r.CodigoArticuloVisions);
                return;
            }

            // Auto-crear Marca y Presentacion si Visions las trae pero no existen en NEXO aún.
            if (!string.IsNullOrWhiteSpace(r.MarcaCodigo))
                await connection.ExecuteAsync(
                    "IF NOT EXISTS (SELECT 1 FROM Catalogo.Marca WHERE Codigo = @Codigo) INSERT INTO Catalogo.Marca (Codigo, Marca) VALUES (@Codigo, @Codigo)",
                    new { Codigo = r.MarcaCodigo });
            if (!string.IsNullOrWhiteSpace(r.PresentacionCodigo))
                await connection.ExecuteAsync(
                    "IF NOT EXISTS (SELECT 1 FROM Catalogo.Presentacion WHERE Codigo = @Codigo) INSERT INTO Catalogo.Presentacion (Codigo, Presentacion) VALUES (@Codigo, @Codigo)",
                    new { Codigo = r.PresentacionCodigo });

            var sinDatos = string.IsNullOrWhiteSpace(r.NombreArticuloVisions) || (r.PrecioArticuloVisions ?? 0m) == 0m;
            // TipoArticuloID desde TipoProductoCodigo de Visions si viene, si no "Producto Terminado".
            var tipoIdFinal = !string.IsNullOrWhiteSpace(r.TipoProductoCodigo)
                ? (await connection.ExecuteScalarAsync<int?>(
                    "SELECT TOP 1 TipoArticuloID FROM Catalogo.TiposArticulo WHERE Codigo = @Codigo",
                    new { Codigo = r.TipoProductoCodigo }) ?? tipoId.Value)
                : tipoId.Value;

            var insertadoSync = await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM Catalogo.Tarjetas WHERE Referencia = @Referencia)
                INSERT INTO Catalogo.Tarjetas
                    (Referencia, Nombre, TipoArticuloID, PPublico, Costo,
                     PBodega, PCredito, UPublico, UBodega, UCredito,
                     StockMinimo, PuntoReorden, Fracciona, Estado,
                     MarcaCodigo, GrupoMenorCodigo, PresentacionCodigo,
                     IvaSiNo, IvaValor, IvaDescripcion,
                     Iva2, IvaDescripcion2)
                VALUES
                    (@Referencia, @Nombre, @TipoArticuloID, @PPublico, @Costo,
                     @PBodega, @PCredito, @UPublico, @UBodega, @UCredito,
                     @StockMinimo, 0, 'N', @Estado,
                     @MarcaCodigo,
                     (SELECT Codigo FROM Catalogo.GrupoMenor WHERE Codigo = @GrupoMenorCodigo),
                     @PresentacionCodigo,
                     @IvaSiNo, @IvaValor, @IvaDescripcion,
                     @Iva2, @IvaDescripcion2)",
                new
                {
                    Referencia         = r.CodigoArticuloVisions,
                    Nombre             = r.NombreArticuloVisions ?? r.CodigoArticuloVisions,
                    TipoArticuloID     = tipoIdFinal,
                    PPublico           = r.PrecioArticuloVisions ?? 0m,
                    Costo              = r.CostoArticuloVisions ?? 0m,
                    StockMinimo        = r.ExistenciasMinimas ?? 0m,
                    PBodega            = (r.PBodega ?? 0m) > 0m ? r.PBodega : (decimal?)null,
                    PCredito           = (r.PCredito ?? 0m) > 0m ? r.PCredito : (decimal?)null,
                    UPublico           = (r.UPublico ?? 0m) > 0m ? r.UPublico : (decimal?)null,
                    UBodega            = (r.UBodega  ?? 0m) > 0m ? r.UBodega  : (decimal?)null,
                    UCredito           = (r.UCredito ?? 0m) > 0m ? r.UCredito : (decimal?)null,
                    Estado             = sinDatos ? 0 : 1,
                    MarcaCodigo        = string.IsNullOrWhiteSpace(r.MarcaCodigo)        ? null : r.MarcaCodigo,
                    GrupoMenorCodigo   = string.IsNullOrWhiteSpace(r.GrupoMenorCodigo)   ? null : r.GrupoMenorCodigo,
                    PresentacionCodigo = string.IsNullOrWhiteSpace(r.PresentacionCodigo) ? null : r.PresentacionCodigo,
                    IvaSiNo            = r.IvaSiNo ?? "SI",
                    IvaValor           = r.IvaValor.HasValue ? (short?)Math.Clamp((long)r.IvaValor.Value, short.MinValue, short.MaxValue) : (short?)null,
                    IvaDescripcion     = r.IvaDescripcion,
                    Iva2               = r.Iva2.HasValue ? (short?)Math.Clamp((long)r.Iva2.Value, short.MinValue, short.MaxValue) : (short?)null,
                    IvaDescripcion2    = r.IvaDescripcion2
                });

            if (insertadoSync > 0)
            {
                _logger.LogInformation("Articulo {Codigo} auto-creado en NEXO desde Visions", r.CodigoArticuloVisions);
                var artIdHist = await connection.ExecuteScalarAsync<int?>(
                    "SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Ref",
                    new { Ref = r.CodigoArticuloVisions });
                if (artIdHist.HasValue)
                    await RegistrarPreciosInicialesAsync(connection, artIdHist.Value,
                        r.PrecioArticuloVisions, r.CostoArticuloVisions, r.PBodega, r.PCredito);
            }

            // Crear mapeo y resolver pendiente para que no siga apareciendo en la UI.
            var articuloId = await connection.ExecuteScalarAsync<int?>(
                "SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Referencia",
                new { Referencia = r.CodigoArticuloVisions });

            if (articuloId.HasValue)
            {
                await connection.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM Integracion.MapeoArticulos
                                   WHERE ArticuloID = @ArticuloID AND CentroCostoID = @CentroCostoID
                                     AND CodigoArticuloVisions = @CodigoArticuloVisions)
                    INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado, FechaCreacion)
                    VALUES (@ArticuloID, @CentroCostoID, @CodigoArticuloVisions, 1, GETDATE())",
                    new { ArticuloID = articuloId.Value, CentroCostoID = centroCostoId, CodigoArticuloVisions = r.CodigoArticuloVisions });

                await connection.ExecuteAsync(@"
                    UPDATE Integracion.ArticulosPendientesMapeo
                    SET Resuelto = 1, FechaResuelto = GETDATE()
                    WHERE CodigoArticuloVisions = @CodigoArticuloVisions AND CentroCostoID = @CentroCostoID AND Resuelto = 0",
                    new { CodigoArticuloVisions = r.CodigoArticuloVisions, CentroCostoID = centroCostoId });

                // Inicializar stock con las existencias actuales de Visions (solo en auto-create).
                if ((r.ExistenciasActuales ?? 0m) > 0m)
                {
                    var bodegaId = await connection.ExecuteScalarAsync<int?>(
                        "SELECT BodegaVentaVisionsID FROM Organizacion.CentrosCosto WHERE CentroCostoID = @CcId",
                        new { CcId = centroCostoId });
                    if (bodegaId.HasValue)
                        await connection.ExecuteAsync(@"
                            IF NOT EXISTS (SELECT 1 FROM Inventario.InventarioStock
                                           WHERE ArticuloID = @ArtId AND BodegaID = @BodId)
                            INSERT INTO Inventario.InventarioStock
                                (ArticuloID, BodegaID, CantidadActual, CostoUnitarioLote, FechaUltimaActualizacion)
                            VALUES (@ArtId, @BodId, @Cantidad, @Costo, GETDATE())",
                            new { ArtId = articuloId.Value, BodId = bodegaId.Value,
                                  Cantidad = r.ExistenciasActuales!.Value, Costo = r.CostoArticuloVisions ?? 0m });
                }
            }

            // Reintentar ahora que el articulo existe en Catalogo.Tarjetas.
            await connection.ExecuteAsync(spNombre, parametros, commandType: CommandType.StoredProcedure);
        }
        catch (Exception) when (existente is null)
        {
            // SP fallo en un evento recien insertado (no 54000, no auto-creable).
            // Eliminar el registro para no dejar huerfanos Procesado=0.
            // Si el agente reenvía el mismo IdEventoExterno en el proximo ciclo,
            // el SELECT inicial encontrara null y lo reinsertara limpio.
            try { await connection.ExecuteAsync(
                "DELETE FROM Integracion.EventosEntrantes WHERE EventoEntranteID = @Id AND Procesado = 0",
                new { Id = eventoEntranteId }); }
            catch { /* ignorar fallo de cleanup */ }
            throw;
        }

        // Si la venta vino con NIT de cliente, crear o completar el registro en CRM.
        // MERGE: si ya existe con datos completos, no los pisa (COALESCE);
        // si existe vacío (auto-creado antes), lo completa; si no existe, inserta todo.
        if (!string.IsNullOrWhiteSpace(r.ClienteNit))
        {
            var nombreCompleto = string.Join(" ", new[] { r.ClientePrimerNombre, r.ClienteSegundoNombre,
                r.ClientePrimerApellido, r.ClienteSegundoApellido }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var nombre = !string.IsNullOrWhiteSpace(r.ClienteEmpresa) ? r.ClienteEmpresa
                       : !string.IsNullOrWhiteSpace(nombreCompleto)   ? nombreCompleto
                       : r.ClienteNombre ?? r.ClienteNit;

            await connection.ExecuteAsync(@"
                MERGE Crm.Clientes AS dest
                USING (SELECT @Nit AS NIT) AS src ON dest.NIT = src.NIT
                WHEN MATCHED THEN UPDATE SET
                    Nombre         = CASE WHEN ISNULL(dest.Nombre,'') IN ('',dest.NIT) AND ISNULL(@Nombre,'') <> '' THEN @Nombre ELSE dest.Nombre END,
                    TipoPersona    = COALESCE(dest.TipoPersona,    @TipoPersona),
                    PrimerNombre   = COALESCE(dest.PrimerNombre,   @PrimerNombre),
                    SegundoNombre  = COALESCE(dest.SegundoNombre,  @SegundoNombre),
                    PrimerApellido = COALESCE(dest.PrimerApellido, @PrimerApellido),
                    SegundoApellido= COALESCE(dest.SegundoApellido,@SegundoApellido),
                    Telefono       = COALESCE(dest.Telefono,       @Telefono),
                    Direccion      = COALESCE(dest.Direccion,      @Direccion),
                    Ciudad         = COALESCE(dest.Ciudad,         @Ciudad),
                    Departamento   = COALESCE(dest.Departamento,   @Departamento),
                    CodigoMuni     = COALESCE(dest.CodigoMuni,     @CodigoMuni),
                    CodigoDept     = COALESCE(dest.CodigoDept,     @CodigoDept),
                    FechaModificacion = GETDATE()
                WHEN NOT MATCHED THEN INSERT
                    (Nombre, NIT, TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                     Telefono, Direccion, Ciudad, Departamento, CodigoMuni, CodigoDept,
                     FuenteContacto, TipoCliente, FechaModificacion)
                VALUES
                    (@Nombre, @Nit, @TipoPersona, @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                     @Telefono, @Direccion, @Ciudad, @Departamento, @CodigoMuni, @CodigoDept,
                     'VISIONS', 'CLIENTE', GETDATE());",
                new
                {
                    Nit            = r.ClienteNit,
                    Nombre         = nombre,
                    TipoPersona    = r.ClienteTipoPersona,
                    PrimerNombre   = r.ClientePrimerNombre,
                    SegundoNombre  = r.ClienteSegundoNombre,
                    PrimerApellido = r.ClientePrimerApellido,
                    SegundoApellido= r.ClienteSegundoApellido,
                    Telefono       = r.ClienteTelefono,
                    Direccion      = r.ClienteDireccion,
                    Ciudad         = r.ClienteCiudad,
                    Departamento   = r.ClienteDepartamento,
                    CodigoMuni     = r.ClienteCodigoMuni,
                    CodigoDept     = r.ClienteCodigoDept
                });
        }

        // Crear o complementar la Factura en NEXO que agrupa las lineas del mismo documento Visions.
        // Solo cuando el agente envio TipDoc/NroDoc (ventas recientes; ventas históricas no lo traen).
        if (!string.IsNullOrWhiteSpace(r.TipDoc) && !string.IsNullOrWhiteSpace(r.NroDoc))
        {
            var articuloIdFactura = await connection.ExecuteScalarAsync<int?>(
                @"SELECT TOP 1 ma.ArticuloID
                  FROM Integracion.MapeoArticulos ma
                  JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                  WHERE ma.Estado = 1 AND cc.TieneVisions = 1
                    AND cc.IdentificadorClienteVisions = CAST(@CentroCostoID AS NVARCHAR(20))
                    AND ma.CodigoArticuloVisions = @CodigoArticuloVisions",
                new { CentroCostoID = centroCostoId, r.CodigoArticuloVisions });

            if (articuloIdFactura.HasValue)
            {
                var clienteIdFactura = !string.IsNullOrWhiteSpace(r.ClienteNit)
                    ? await connection.ExecuteScalarAsync<int?>(
                        "SELECT TOP 1 ClienteID FROM Crm.Clientes WHERE NIT = @Nit AND Estado = 1",
                        new { Nit = r.ClienteNit })
                    : null;
                // Si el NIT no existe en NEXO, usar CONSUMIDOR FINAL (CF-SYS), no el primer cliente
                clienteIdFactura ??= await connection.ExecuteScalarAsync<int?>(
                    "SELECT TOP 1 ClienteID FROM Crm.Clientes WHERE NIT = 'CF-SYS' AND Estado = 1");

                var usuarioSistemaId = await connection.ExecuteScalarAsync<int?>(
                    "SELECT UsuarioID FROM Seguridad.Usuarios WHERE Username = 'sistema.sync'");

                if (clienteIdFactura.HasValue && usuarioSistemaId.HasValue)
                {
                    var facturaId = await connection.ExecuteScalarAsync<int?>(
                        @"SELECT FacturaID FROM Facturacion.Facturas
                          WHERE TipDoc = @TipDoc AND NroDoc = @NroDoc AND CentroCostoID = @CentroCostoID",
                        new { r.TipDoc, r.NroDoc, CentroCostoID = centroCostoId });

                    if (facturaId is null)
                    {
                        facturaId = await connection.ExecuteScalarAsync<int>(
                            @"INSERT INTO Facturacion.Facturas
                                  (ClienteID, Fecha, TipDoc, NroDoc, UsuarioID, CentroCostoID, StockDescontado, VisionsConfirmado)
                              OUTPUT INSERTED.FacturaID
                              VALUES (@ClienteID, @Fecha, @TipDoc, @NroDoc, @UsuarioID, @CentroCostoID, 1, 1)",
                            new {
                                ClienteID     = clienteIdFactura.Value,
                                Fecha         = r.FechaEventoOrigen,
                                r.TipDoc, r.NroDoc,
                                UsuarioID     = usuarioSistemaId.Value,
                                CentroCostoID = centroCostoId
                            });
                    }

                    await connection.ExecuteAsync(
                        @"IF NOT EXISTS (
                              SELECT 1 FROM Facturacion.FacturaLineas
                              WHERE FacturaID = @FacturaID AND ArticuloID = @ArticuloID
                                AND ABS(Cantidad - @Cantidad) < 0.001)
                          INSERT INTO Facturacion.FacturaLineas
                              (FacturaID, ArticuloID, DescripcionLinea, Cantidad, PrecioUnitario)
                          VALUES (@FacturaID, @ArticuloID, @DescripcionLinea, @Cantidad, @PrecioUnitario)",
                        new {
                            FacturaID        = facturaId.Value,
                            ArticuloID       = articuloIdFactura.Value,
                            DescripcionLinea = r.NombreArticuloVisions,
                            r.Cantidad,
                            PrecioUnitario   = r.PrecioArticuloVisions ?? 0m
                        });
                }
            }
        }
    }

    public async Task<GenerarApiKeyResponse> GenerarApiKeyAsync(GenerarApiKeyRequest r)
    {
        using var connection = _db.CreateConnection();

        var apiKey    = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

        // Upsert: si ya existe un agente activo para este CentroCosto se regenera
        // su clave en lugar de crear un registro nuevo -- garantiza 1 agente por CentroCosto.
        var existingId = await connection.ExecuteScalarAsync<int?>(
            @"SELECT TOP 1 AgenteSyncID FROM Integracion.AgentesSync
              WHERE CentroCostoID = @CentroCostoID AND Activo = 1
              ORDER BY AgenteSyncID DESC",
            new { r.CentroCostoID });

        if (existingId.HasValue)
        {
            await connection.ExecuteAsync(
                @"UPDATE Integracion.AgentesSync
                  SET ApiKeyHash = @ApiKeyHash, ApiKeyPlain = @ApiKeyPlain, Descripcion = @Descripcion
                  WHERE AgenteSyncID = @AgenteSyncID",
                new { ApiKeyHash = apiKeyHash, ApiKeyPlain = apiKey, r.Descripcion, AgenteSyncID = existingId.Value });
            await connection.ExecuteAsync(
                "UPDATE Organizacion.CentrosCosto SET TieneVisions = 1, IdentificadorClienteVisions = CASE WHEN NULLIF(IdentificadorClienteVisions,'') IS NULL THEN CAST(@CentroCostoID AS NVARCHAR(20)) ELSE IdentificadorClienteVisions END WHERE CentroCostoID = @CentroCostoID",
                new { r.CentroCostoID });
            return new GenerarApiKeyResponse(existingId.Value, apiKey);
        }

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Integracion.AgentesSync (CentroCostoID, ApiKeyHash, ApiKeyPlain, Descripcion)
              OUTPUT INSERTED.AgenteSyncID
              VALUES (@CentroCostoID, @ApiKeyHash, @ApiKeyPlain, @Descripcion)",
            new { r.CentroCostoID, ApiKeyHash = apiKeyHash, ApiKeyPlain = apiKey, r.Descripcion });

        await connection.ExecuteAsync(
            "UPDATE Organizacion.CentrosCosto SET TieneVisions = 1, IdentificadorClienteVisions = '1' WHERE CentroCostoID = @CentroCostoID",
            new { r.CentroCostoID });

        return new GenerarApiKeyResponse(id, apiKey);
    }

    public async Task<ConfiguracionAgenteResponse> ObtenerConfiguracionAgenteAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        // TRY_CAST porque IdentificadorClienteVisions es nvarchar en NEXO (permite
        // texto libre), pero en Visions CENTROCOSTO siempre es numerico (smallint).
        // IntervalMinutes viene de AgentesSync -- el agente usa el valor guardado en DB
        // en vez de appsettings.json para que el admin pueda cambiarlo desde la web.
        const string sql = @"
            SELECT TRY_CAST(cc.IdentificadorClienteVisions AS INT) AS CentroCostoVisions,
                   cc.Estado AS Activo,
                   cc.PrefijosDocumentoVentaVisions AS PrefijosDocumentoVenta,
                   ISNULL(a.IntervalMinutes, 5) AS IntervalMinutes,
                   a.FechaInicioSyncVentas,
                   ISNULL(a.SincAntsaActivo, 0) AS SincAntsaActivo,
                   ISNULL(a.SyncPausada, 0) AS SyncPausada,
                   ISNULL(a.PurgaVisionsRequerida, 0) AS PurgaVisionsRequerida,
                   ISNULL(a.ReprocesarArticulos, 0) AS ReprocesarArticulos,
                   ISNULL(a.ReprocesarClientes, 0) AS ReprocesarClientes,
                   ISNULL(a.ReprocesarProveedores, 0) AS ReprocesarProveedores
            FROM Organizacion.CentrosCosto cc
            LEFT JOIN Integracion.AgentesSync a
                   ON a.CentroCostoID = cc.CentroCostoID AND a.Activo = 1
            WHERE cc.CentroCostoID = @CentroCostoId";

        var resultado = await connection.QuerySingleOrDefaultAsync<ConfiguracionAgenteResponse>(sql, new { CentroCostoId = centroCostoId });

        return resultado ?? throw new KeyNotFoundException($"No existe el centro de costo {centroCostoId}.");
    }

    public async Task<IEnumerable<MapeoArticuloItem>> ListarMapeosAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT m.MapeoID, m.ArticuloID, a.Referencia AS SkuArticulo, a.Nombre AS NombreArticulo,
                   m.CentroCostoID, m.CodigoArticuloVisions, m.Estado, m.FechaCreacion
            FROM Integracion.MapeoArticulos m
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = m.ArticuloID
            WHERE m.CentroCostoID = @CentroCostoId
            ORDER BY a.Nombre";

        return await connection.QueryAsync<MapeoArticuloItem>(sql, new { CentroCostoId = centroCostoId });
    }

    public async Task CrearMapeoAsync(CrearMapeoArticuloRequest r)
    {
        using var connection = _db.CreateConnection();

        // Visions es un sistema de punto de venta: solo tiene sentido mapear
        // Producto Terminado (lo unico que se vende ahi). Materia Prima,
        // Insumos y Servicios se quedan solo en NEXO.
        var tipoArticulo = await connection.ExecuteScalarAsync<string?>(
            @"SELECT ta.Nombre FROM Catalogo.Tarjetas a
              JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
              WHERE a.ArticuloID = @ArticuloID", new { r.ArticuloID });

        if (tipoArticulo != "Producto Terminado")
            throw new InvalidOperationException("Solo se pueden mapear articulos de tipo Producto Terminado hacia Visions.");

        const string sqlInsert = @"
            INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions)
            VALUES (@ArticuloID, @CentroCostoID, @CodigoArticuloVisions)";

        await connection.ExecuteAsync(sqlInsert, r);

        await EncolarSincronizacionArticuloAsync(connection, r.ArticuloID, r.CentroCostoID);
    }

    // Encola el evento saliente que hace que el Agente cree/actualice el
    // articulo en dbo.TARJETA de Visions (nombre, costo, precio publico,
    // existencias minimas y stock actual de Inventario.InventarioStock).
    private static async Task EncolarSincronizacionArticuloAsync(IDbConnection connection, int articuloId, int centroCostoId)
    {
        const string sql = @"
            INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'SINCRONIZAR_ARTICULO', @CentroCostoID, @ArticuloID,
                   ISNULL((
                       SELECT SUM(s.CantidadActual)
                       FROM Inventario.InventarioStock s
                       JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                       WHERE s.ArticuloID = @ArticuloID AND b.CentroCostoID = @CentroCostoID
                   ), 0),
                   a.CostoPromedio
            FROM Catalogo.Tarjetas a WHERE a.ArticuloID = @ArticuloID";

        await connection.ExecuteAsync(sql, new { ArticuloID = articuloId, CentroCostoID = centroCostoId });
    }

    public async Task<IEnumerable<ArticuloPendienteMapeoItem>> ListarArticulosPendientesMapeoAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT PendienteID, CentroCostoID, CodigoArticuloVisions, NombreVisions, CostoVisions, PrecioVisions,
                   CantidadDetectada, FechaDetectado
            FROM Integracion.ArticulosPendientesMapeo
            WHERE CentroCostoID = @CentroCostoId AND Resuelto = 0
            ORDER BY FechaDetectado DESC";

        return await connection.QueryAsync<ArticuloPendienteMapeoItem>(sql, new { CentroCostoId = centroCostoId });
    }

    private record PendienteMapeoBasico(int CentroCostoID, string CodigoArticuloVisions);

    public async Task ResolverArticuloPendienteAsync(int pendienteId, ResolverArticuloPendienteRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var pendiente = await connection.QuerySingleOrDefaultAsync<PendienteMapeoBasico>(
                "SELECT CentroCostoID, CodigoArticuloVisions FROM Integracion.ArticulosPendientesMapeo WHERE PendienteID = @PendienteID AND Resuelto = 0",
                new { PendienteID = pendienteId }, transaction);

            if (pendiente is null)
                throw new KeyNotFoundException($"No existe (o ya se resolvio) el articulo pendiente {pendienteId}.");

            int articuloId;

            if (r.ArticuloIDExistente is not null)
            {
                articuloId = r.ArticuloIDExistente.Value;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(r.SkuNuevo) || string.IsNullOrWhiteSpace(r.NombreNuevo))
                    throw new InvalidOperationException("Falta SKU o Nombre para crear el articulo nuevo.");

                var tipoProductoTerminadoId = await connection.ExecuteScalarAsync<int>(
                    "SELECT TipoArticuloID FROM Catalogo.TiposArticulo WHERE Nombre = 'Producto Terminado'", transaction: transaction);

                var stockMinimo = r.StockMinimoNuevo ?? 0;

                const string sqlCrearArticulo = @"
                    INSERT INTO Catalogo.Tarjetas (Referencia, Nombre, TipoArticuloID, PPublico, StockMinimo, PuntoReorden)
                    OUTPUT INSERTED.ArticuloID
                    VALUES (@Referencia, @Nombre, @TipoArticuloID, @PPublico, @StockMinimo, @StockMinimo)";

                articuloId = await connection.ExecuteScalarAsync<int>(sqlCrearArticulo, new
                {
                    Referencia = r.SkuNuevo,
                    Nombre = r.NombreNuevo,
                    TipoArticuloID = tipoProductoTerminadoId,
                    PPublico = r.PrecioVentaNuevo ?? 0,
                    StockMinimo = stockMinimo
                }, transaction);
            }

            const string sqlMapeo = @"
                INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions)
                VALUES (@ArticuloID, @CentroCostoID, @CodigoArticuloVisions)";

            await connection.ExecuteAsync(sqlMapeo, new
            {
                ArticuloID = articuloId,
                pendiente.CentroCostoID,
                pendiente.CodigoArticuloVisions
            }, transaction);

            await connection.ExecuteAsync(
                "UPDATE Integracion.ArticulosPendientesMapeo SET Resuelto = 1, FechaResuelto = SYSUTCDATETIME() WHERE PendienteID = @PendienteID",
                new { PendienteID = pendienteId }, transaction);

            // Procesar automaticamente los EventosEntrantes que quedaron Procesado=0
            // esperando este mapeo (ventas que llegaron antes de que existiera el mapeo).
            var eventosBlockeados = await connection.QueryAsync<long>(
                @"SELECT EventoEntranteID FROM Integracion.EventosEntrantes
                  WHERE CodigoArticuloVisions = @CodigoArticuloVisions AND CentroCostoID = @CentroCostoID AND Procesado = 0",
                new { pendiente.CodigoArticuloVisions, pendiente.CentroCostoID }, transaction);

            foreach (var eid in eventosBlockeados)
            {
                var p = new DynamicParameters();
                p.Add("EventoEntranteID", eid);
                await connection.ExecuteAsync(
                    "Integracion.sp_ProcesarEventoEntrante", p, transaction,
                    commandType: CommandType.StoredProcedure);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<ResolverTodosResponse> ResolverTodosAsync(int centroCostoId)
    {
        var pendientes = (await ListarArticulosPendientesMapeoAsync(centroCostoId)).ToList();
        int resueltos = 0, errores = 0;

        foreach (var p in pendientes)
        {
            try
            {
                var sku = p.CodigoArticuloVisions?.Trim() ?? "";
                var nombre = !string.IsNullOrWhiteSpace(p.NombreVisions) ? p.NombreVisions.Trim() : sku;

                await ResolverArticuloPendienteAsync(p.PendienteID, new ResolverArticuloPendienteRequest(
                    ArticuloIDExistente: null,
                    SkuNuevo:           sku,
                    NombreNuevo:        nombre,
                    PrecioVentaNuevo:   p.PrecioVisions ?? 0,
                    StockMinimoNuevo:   0));
                resueltos++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("ResolverTodos: pendiente {Id} ({Codigo}) fallo — {Msg}",
                    p.PendienteID, p.CodigoArticuloVisions, ex.Message);
                errores++;
            }
        }

        return new ResolverTodosResponse(pendientes.Count, resueltos, errores);
    }

    public async Task<LatidoResponse> RegistrarLatidoAsync(int centroCostoId, string? versionAgente = null)
    {
        using var connection = _db.CreateConnection();

        var parametros = new DynamicParameters();
        parametros.Add("AgenteSyncID", await ObtenerAgenteSyncIdAsync(connection, centroCostoId));
        parametros.Add("VersionAgente", versionAgente);
        await connection.ExecuteAsync("Integracion.sp_RegistrarLatido", parametros, commandType: CommandType.StoredProcedure);

        var pendientes = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Integracion.EventosSalientes WHERE Estado = 'PENDIENTE' AND CentroCostoID = @CentroCostoId",
            new { CentroCostoId = centroCostoId });

        var versionDisponible = _config.GetValue<string>("Sync:AgentVersion") ?? "1.0.0";
        return new LatidoResponse(DateTime.UtcNow, pendientes, versionDisponible);
    }

    private static async Task<int> ObtenerAgenteSyncIdAsync(System.Data.IDbConnection connection, int centroCostoId)
    {
        var id = await connection.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 AgenteSyncID FROM Integracion.AgentesSync WHERE CentroCostoID = @CentroCostoId AND Activo = 1 ORDER BY AgenteSyncID DESC",
            new { CentroCostoId = centroCostoId });
        return id ?? throw new KeyNotFoundException($"No hay agente activo para el centro de costo {centroCostoId}.");
    }

    private record EstadoFila(
        int AgenteSyncID, string Descripcion, int CentroCostoID, string NombreCentroCosto,
        bool Activo, DateTime? UltimoLatido, string? VersionAgente);

    public async Task<IEnumerable<EstadoIntegracionResponse>> ObtenerEstadoIntegracionAsync()
    {
        const string cacheKey = "integracion_estado_v1";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<EstadoIntegracionResponse>? cachedEstado) && cachedEstado is not null)
            return cachedEstado;

        using var connection = _db.CreateConnection();
        var hoy = DateTime.UtcNow.Date;

        var rows = await connection.QueryAsync<(
            int AgenteSyncID, string Descripcion, int CentroCostoID, string NombreCentroCosto,
            bool Activo, DateTime? UltimoLatido, string? VersionAgente,
            int Pendientes, int ProcesadosHoy, int VentasHoy, int ConError, int ArticulosFaltantes, bool SyncPausada)>(@"
            SELECT
                a.AgenteSyncID, a.Descripcion, a.CentroCostoID, cc.Nombre AS NombreCentroCosto,
                a.Activo, a.UltimoLatido, a.VersionAgente,
                sal.Pendientes, sal.ProcesadosHoy, ent.VentasHoy, sal.ConError, falt.ArticulosFaltantes,
                ISNULL(a.SyncPausada, 0) AS SyncPausada
            FROM Integracion.AgentesSync a
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = a.CentroCostoID
            CROSS APPLY (
                SELECT
                    SUM(CASE WHEN Estado = 'PENDIENTE'  THEN 1 ELSE 0 END) AS Pendientes,
                    SUM(CASE WHEN Estado = 'CONFIRMADO' AND CAST(FechaEnvio AS DATE) = @Hoy THEN 1 ELSE 0 END) AS ProcesadosHoy,
                    SUM(CASE WHEN Estado = 'ERROR'      THEN 1 ELSE 0 END) AS ConError
                FROM Integracion.EventosSalientes WHERE CentroCostoID = a.CentroCostoID
            ) sal
            CROSS APPLY (
                SELECT SUM(CASE WHEN TipoEvento = 'VENTA' AND Procesado = 1 AND CAST(FechaRecepcion AS DATE) = @Hoy THEN 1 ELSE 0 END) AS VentasHoy
                FROM Integracion.EventosEntrantes WHERE CentroCostoID = a.CentroCostoID
            ) ent
            CROSS APPLY (
                SELECT COUNT(DISTINCT ma_otros.ArticuloID) AS ArticulosFaltantes
                FROM Integracion.MapeoArticulos ma_otros
                JOIN Organizacion.CentrosCosto cc2 ON cc2.CentroCostoID = ma_otros.CentroCostoID
                WHERE cc2.TieneVisions = 1 AND ma_otros.Estado = 1
                  AND ma_otros.CentroCostoID <> a.CentroCostoID
                  AND NOT EXISTS (
                      SELECT 1 FROM Integracion.MapeoArticulos ma_este
                      WHERE ma_este.ArticuloID = ma_otros.ArticuloID
                        AND ma_este.CentroCostoID = a.CentroCostoID AND ma_este.Estado = 1)
            ) falt
            WHERE a.Activo = 1
            ORDER BY a.UltimoLatido DESC",
            new { Hoy = hoy });

        var versionActual = _config.GetValue<string>("Sync:AgentVersion") ?? "1.0.0";
        var resultado = rows.Select(r => new EstadoIntegracionResponse(
            r.AgenteSyncID, r.Descripcion, r.CentroCostoID, r.NombreCentroCosto,
            r.Activo, r.UltimoLatido, r.VersionAgente, versionActual,
            r.Pendientes, r.ProcesadosHoy, r.VentasHoy, r.ConError, r.ArticulosFaltantes,
            SyncPausada: r.SyncPausada)).ToList();

        _cache.Set(cacheKey, (IEnumerable<EstadoIntegracionResponse>)resultado, TimeSpan.FromSeconds(30));
        return resultado;
    }

    public async Task<IEnumerable<ProgresoSyncResponse>> ObtenerProgresoSyncAsync()
    {
        using var connection = _db.CreateConnection();

        var agentes = (await connection.QueryAsync<(int AgenteSyncID, int CentroCostoID, string NombreCentroCosto, DateTime? UltimoLatido)>(
            @"SELECT a.AgenteSyncID, a.CentroCostoID, cc.Nombre AS NombreCentroCosto, a.UltimoLatido
              FROM Integracion.AgentesSync a
              JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = a.CentroCostoID
              WHERE a.Activo = 1")).ToList();

        var resultado = new List<ProgresoSyncResponse>();
        var totalArticulosNexo = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Catalogo.Tarjetas WHERE Estado = 1");

        foreach (var ag in agentes)
        {
            var mapeados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(DISTINCT ArticuloID) FROM Integracion.MapeoArticulos WHERE CentroCostoID = @CcId AND Estado = 1",
                new { CcId = ag.CentroCostoID });

            var pendientes = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Integracion.ArticulosPendientesMapeo WHERE CentroCostoID = @CcId AND Resuelto = 0",
                new { CcId = ag.CentroCostoID });

            var eventosProcesados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Integracion.EventosEntrantes WHERE CentroCostoID = @CcId AND Procesado = 1",
                new { CcId = ag.CentroCostoID });

            var facturasVisions = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Facturacion.Facturas WHERE CentroCostoID = @CcId AND VisionsConfirmado = 1",
                new { CcId = ag.CentroCostoID });

            var articulosCompletos = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(DISTINCT ma.ArticuloID)
                FROM Integracion.MapeoArticulos ma
                JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
                WHERE ma.CentroCostoID = @CcId AND ma.Estado = 1
                  AND t.MarcaCodigo IS NOT NULL AND t.IvaValor IS NOT NULL",
                new { CcId = ag.CentroCostoID });

            var articulosSinDatos = await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(DISTINCT ma.ArticuloID)
                FROM Integracion.MapeoArticulos ma
                JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
                WHERE ma.CentroCostoID = @CcId AND ma.Estado = 1
                  AND (t.MarcaCodigo IS NULL OR t.IvaValor IS NULL)",
                new { CcId = ag.CentroCostoID });

            resultado.Add(new ProgresoSyncResponse(
                ag.AgenteSyncID, ag.NombreCentroCosto,
                totalArticulosNexo, mapeados, pendientes,
                eventosProcesados, facturasVisions, ag.UltimoLatido,
                articulosCompletos, articulosSinDatos));
        }

        return resultado;
    }

    public async Task RegistrarFalloEventoAsync(long eventoId, int centroCostoId, string mensajeError)
    {
        using var connection = _db.CreateConnection();

        var mensaje = mensajeError.Length > 500 ? mensajeError[..500] : mensajeError;

        const string sql = @"
            UPDATE Integracion.EventosSalientes
            SET IntentosEnvio  = IntentosEnvio + 1,
                UltimoError    = @Mensaje,
                Estado         = CASE WHEN IntentosEnvio + 1 >= 3 THEN 'ERROR' ELSE Estado END
            WHERE EventoID = @EventoId AND CentroCostoID = @CentroCostoId AND Estado = 'PENDIENTE'";

        await connection.ExecuteAsync(sql, new { EventoId = eventoId, CentroCostoId = centroCostoId, Mensaje = mensaje });
    }

    public async Task<ConfiguracionAgenteCompletaResponse> ObtenerConfiguracionCompletaAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT a.AgenteSyncID, a.Descripcion, a.CentroCostoID, cc.Nombre AS NombreCentroCosto,
                   a.Activo, a.VisionsDbConexion, a.NexoApiBaseUrl, a.IntervalMinutes, a.IntervalSeconds, a.AgentePath,
                   cc.PrefijosDocumentoVentaVisions AS PrefijosDocumentoVenta,
                   a.FechaInicioSyncVentas
            FROM Integracion.AgentesSync a
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = a.CentroCostoID
            WHERE a.AgenteSyncID = @AgenteSyncId";

        var resultado = await connection.QuerySingleOrDefaultAsync<ConfiguracionAgenteCompletaResponse>(
            sql, new { AgenteSyncId = agenteSyncId });

        return resultado ?? throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");
    }

    public async Task ActualizarConfiguracionCompletaAsync(int agenteSyncId, ActualizarConfiguracionAgenteRequest r)
    {
        using var connection = _db.CreateConnection();

        // NexoApiBaseUrl siempre se toma del servidor — el cliente no tiene que escribirla.
        var nexoApiUrl = (_config["ApiBaseUrl"] ?? _config["NexoApi:BaseUrl"] ?? "https://localhost:7144").TrimEnd('/') + "/";

        const string sqlAgente = @"
            UPDATE Integracion.AgentesSync
            SET VisionsDbConexion    = @VisionsDbConexion,
                NexoApiBaseUrl       = @NexoApiBaseUrl,
                IntervalMinutes      = @IntervalMinutes,
                IntervalSeconds      = @IntervalSeconds,
                AgentePath           = @AgentePath,
                FechaInicioSyncVentas = @FechaInicioSyncVentas
            WHERE AgenteSyncID = @AgenteSyncId";

        var filas = await connection.ExecuteAsync(sqlAgente, new
        {
            AgenteSyncId          = agenteSyncId,
            r.VisionsDbConexion,
            NexoApiBaseUrl        = nexoApiUrl,
            r.IntervalMinutes,
            r.IntervalSeconds,
            r.AgentePath,
            FechaInicioSyncVentas = r.FechaInicioSyncVentas.HasValue
                ? (object)r.FechaInicioSyncVentas.Value.Date
                : DBNull.Value
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        // Actualizar prefijos de documento de venta en CentrosCosto (persiste en NEXO y se propaga
        // a Visions en la proxima ronda del agente via TareaSincronizarConfiguracion).
        var ccId = await connection.ExecuteScalarAsync<int>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });
        await connection.ExecuteAsync(
            "UPDATE Organizacion.CentrosCosto SET PrefijosDocumentoVentaVisions = @Prefijos WHERE CentroCostoID = @CcId",
            new { Prefijos = string.IsNullOrWhiteSpace(r.PrefijosDocumentoVenta) ? null : r.PrefijosDocumentoVenta, CcId = ccId });
    }

    public async Task DesactivarAgenteAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();

        var ccId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @Id AND Activo = 1",
            new { Id = agenteSyncId });

        var filas = await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET Activo = 0 WHERE AgenteSyncID = @Id",
            new { Id = agenteSyncId });
        if (filas == 0)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        if (ccId.HasValue)
            await connection.ExecuteAsync(
                @"UPDATE Organizacion.CentrosCosto
                  SET TieneVisions = 0, IdentificadorClienteVisions = NULL,
                      BodegaVentaVisionsID = NULL, PrefijosDocumentoVentaVisions = NULL
                  WHERE CentroCostoID = @CcId",
                new { CcId = ccId.Value });
    }

    public async Task<int> ReintentarPendientesAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        var ccId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @Id AND Activo = 1",
            new { Id = agenteSyncId });
        if (!ccId.HasValue) throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        return await connection.ExecuteAsync(
            @"UPDATE Integracion.EventosSalientes
              SET Estado = 'PENDIENTE', IntentosEnvio = 0, UltimoError = NULL
              WHERE CentroCostoID = @CcId AND Estado = 'ERROR'",
            new { CcId = ccId.Value });
    }

    public async Task<int> ReprocesarEntrantesAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        var ccId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @Id AND Activo = 1",
            new { Id = agenteSyncId });
        if (!ccId.HasValue) throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        var pendientes = (await connection.QueryAsync<(long EventoEntranteID, string TipoEvento, string? TipDoc)>(@"
            SELECT TOP 500 EventoEntranteID, TipoEvento, TipDoc
            FROM Integracion.EventosEntrantes
            WHERE CentroCostoID = @CcId AND Procesado = 0
            ORDER BY EventoEntranteID",
            new { CcId = ccId.Value })).ToList();

        int exitosos = 0;
        foreach (var (id, tipoEvento, tipDoc) in pendientes)
        {
            var spNombre = tipoEvento == "ENTRADA_COMPRA"
                ? "Integracion.sp_ProcesarEntradaCompraVisions"
                : (tipDoc?.Contains("DEVOLUCION", StringComparison.OrdinalIgnoreCase) == true)
                    ? "Integracion.sp_ProcesarDevolucionVisions"
                    : "Integracion.sp_ProcesarEventoEntrante";
            try
            {
                var p = new DynamicParameters();
                p.Add("EventoEntranteID", id);
                await connection.ExecuteAsync(spNombre, p, commandType: CommandType.StoredProcedure);
                exitosos++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("ReprocesarEntrantes: EventoEntranteID {Id} fallo — {Msg}", id, ex.Message);
            }
        }
        return exitosos;
    }

    public async Task<ActividadAgenteResponse> ObtenerActividadAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        var ccId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @Id AND Activo = 1",
            new { Id = agenteSyncId });
        if (!ccId.HasValue) throw new KeyNotFoundException("Agente no encontrado.");

        var hoy = DateTime.UtcNow.Date;

        var articulosEnlazados = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Integracion.MapeoArticulos WHERE CentroCostoID = @CcId AND Estado = 1",
            new { CcId = ccId.Value });

        var facturasHoy = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Integracion.EventosSalientes
              WHERE TipoEvento = 'FACTURA_NEXO' AND Estado = 'CONFIRMADO'
                AND CentroCostoID = @CcId AND CAST(FechaEnvio AS DATE) = @Hoy",
            new { CcId = ccId.Value, Hoy = hoy });

        var comprasHoy = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Integracion.EventosSalientes
              WHERE TipoEvento = 'RECEPCION_COMPRA' AND Estado = 'CONFIRMADO'
                AND CentroCostoID = @CcId AND CAST(FechaEnvio AS DATE) = @Hoy",
            new { CcId = ccId.Value, Hoy = hoy });

        var ajustesHoy = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Integracion.EventosSalientes
              WHERE TipoEvento IN ('AJUSTE_INVENTARIO','BAJA_INVENTARIO') AND Estado = 'CONFIRMADO'
                AND CentroCostoID = @CcId AND CAST(FechaEnvio AS DATE) = @Hoy",
            new { CcId = ccId.Value, Hoy = hoy });

        var eventos = (await connection.QueryAsync<EventoActividadItem>(
            @"SELECT TOP 25
                e.EventoID, e.TipoEvento, e.Estado,
                a.Referencia AS ReferenciaVisions, a.Nombre AS NombreArticulo,
                e.Cantidad, e.UltimoError AS MensajeError, e.FechaCreacion, e.FechaEnvio,
                e.IntentosEnvio
              FROM Integracion.EventosSalientes e
              LEFT JOIN Catalogo.Tarjetas a ON a.ArticuloID = e.ArticuloID
              WHERE e.CentroCostoID = @CcId
              ORDER BY e.FechaCreacion DESC",
            new { CcId = ccId.Value })).ToList();

        // Totales globales del catálogo NEXO
        var totalArticulos   = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Catalogo.Tarjetas WHERE Estado = 1");
        var totalMarcas      = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Catalogo.Marca");
        var totalGruposMayor = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Catalogo.GrupoMayor");
        var totalGruposMenor = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Catalogo.GrupoMenor");
        var totalClientes    = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Crm.Clientes");

        // Sync hoy por dirección
        var facturasNexoVisionsHoy = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Integracion.EventosSalientes
              WHERE TipoEvento = 'FACTURA_NEXO' AND Estado = 'CONFIRMADO'
                AND CentroCostoID = @CcId AND CAST(FechaEnvio AS DATE) = @Hoy",
            new { CcId = ccId.Value, Hoy = hoy });

        var ventasVisionsNexoHoy = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Integracion.EventosEntrantes
              WHERE TipoEvento = 'VENTA' AND Procesado = 1
                AND CentroCostoID = @CcId AND CAST(FechaProcesado AS DATE) = @Hoy",
            new { CcId = ccId.Value, Hoy = hoy });

        return new ActividadAgenteResponse(
            articulosEnlazados, facturasHoy, comprasHoy, ajustesHoy, eventos,
            totalArticulos, totalMarcas, totalGruposMayor, totalGruposMenor, totalClientes,
            facturasNexoVisionsHoy, ventasVisionsNexoHoy);
    }

    // Formato del EXE combinado generado para el cliente:
    // [NexoInstaladorAgente.exe] + [appsettings UTF-8] + [int32: settings_len]
    // + [NexoSyncAgent.exe] + [int64: agent_len] + ["NEXO_SETUP_V1" 13 bytes]
    private const string InstallerMagic = "NEXO_SETUP_V1";

    public async Task<string> PrepararDescargaInstaladorAsync(int agenteSyncId)
    {
        var origenDir = Path.Combine(AppContext.BaseDirectory, "Agent");
        if (!Directory.Exists(origenDir))
        {
            // En desarrollo (Debug) no existe Agent/ en bin\Debug; usar NexoSyncAgent\publish\ junto a la solución.
            var devFallback = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NexoSyncAgent", "publish"));
            if (Directory.Exists(devFallback))
                origenDir = devFallback;
            else
                throw new InvalidOperationException(
                    "El agente no está bundled. En desarrollo ejecuta 'dotnet publish NexoSyncAgent' primero.");
        }

        var installerPath = Path.Combine(origenDir, "NexoInstaladorAgente.exe");
        var agentPath     = Path.Combine(origenDir, "NexoSyncAgent.exe");

        if (!File.Exists(installerPath) || !File.Exists(agentPath))
            throw new InvalidOperationException(
                "Faltan archivos en la carpeta Agent/ del servidor. Republica NexoApi.");

        // Reusar la key existente si ya hay un agente desplegado; solo generar nueva si no hay plaintext.
        // Esto evita romper agentes activos cada vez que alguien descarga el instalador.
        using var connection = _db.CreateConnection();
        var existingPlain = await connection.ExecuteScalarAsync<string?>(
            "SELECT ApiKeyPlain FROM Integracion.AgentesSync WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });

        string apiKey;
        if (!string.IsNullOrWhiteSpace(existingPlain))
        {
            apiKey = existingPlain;
        }
        else
        {
            apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
            await connection.ExecuteAsync(
                "UPDATE Integracion.AgentesSync SET ApiKeyHash = @ApiKeyHash, ApiKeyPlain = @ApiKeyPlain WHERE AgenteSyncID = @AgenteSyncId",
                new { ApiKeyHash = apiKeyHash, ApiKeyPlain = apiKey, AgenteSyncId = agenteSyncId });
        }

        var config          = await ObtenerConfiguracionCompletaAsync(agenteSyncId);
        var settingsBytes   = Encoding.UTF8.GetBytes(GenerarAppsettingsJson(config, apiKey));
        var installerBytes  = await File.ReadAllBytesAsync(installerPath);
        var agentBytes      = await File.ReadAllBytesAsync(agentPath);
        var magicBytes      = Encoding.ASCII.GetBytes(InstallerMagic);

        // Ensamblar el EXE combinado en memoria.
        using var ms = new MemoryStream(
            installerBytes.Length + settingsBytes.Length + 4 + agentBytes.Length + 8 + magicBytes.Length);
        await ms.WriteAsync(installerBytes);
        await ms.WriteAsync(settingsBytes);
        await ms.WriteAsync(BitConverter.GetBytes(settingsBytes.Length));  // int32
        await ms.WriteAsync(agentBytes);
        await ms.WriteAsync(BitConverter.GetBytes((long)agentBytes.Length)); // int64
        await ms.WriteAsync(magicBytes);

        // Guardar en caché con token de un solo uso (10 min).
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(token, ms.ToArray(), TimeSpan.FromMinutes(10));
        return token;
    }

    public byte[]? ObtenerPaqueteInstalador(string token)
    {
        if (!_cache.TryGetValue(token, out byte[]? bytes)) return null;
        _cache.Remove(token); // un solo uso
        return bytes;
    }

    public async Task<string> PrepararAppsettingsConKeyFrescaAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        var existingPlain = await connection.ExecuteScalarAsync<string?>(
            "SELECT ApiKeyPlain FROM Integracion.AgentesSync WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });

        string apiKey;
        if (!string.IsNullOrWhiteSpace(existingPlain))
        {
            apiKey = existingPlain;
        }
        else
        {
            apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
            await connection.ExecuteAsync(
                "UPDATE Integracion.AgentesSync SET ApiKeyHash = @ApiKeyHash, ApiKeyPlain = @ApiKeyPlain WHERE AgenteSyncID = @AgenteSyncId",
                new { ApiKeyHash = apiKeyHash, ApiKeyPlain = apiKey, AgenteSyncId = agenteSyncId });
        }

        var config = await ObtenerConfiguracionCompletaAsync(agenteSyncId);
        return GenerarAppsettingsJson(config, apiKey);
    }

    public string GenerarAppsettingsJson(ConfiguracionAgenteCompletaResponse config, string apiKey)
    {
        var serverName = config.VisionsDbConexion ?? "SERVIDOR\\INSTANCIA";
        var db         = $"Server={serverName};Database=VISIONSDBL1;User Id=sas;Password=admin12345;TrustServerCertificate=True;";
        var url        = (config.NexoApiBaseUrl ?? _config["ApiBaseUrl"] ?? _config["NexoApi:BaseUrl"] ?? "https://nexo.mi-empresa.com/").TrimEnd('/') + "/";
        var minutos  = config.IntervalMinutes > 0 ? config.IntervalMinutes : 5;
        var segundos = config.IntervalSeconds > 0 ? config.IntervalSeconds : 0;

        // TenantMiddleware ya resolvió el host y lo guardó en Items["Nexo_TenantHost"].
        // Es más confiable que releer el header porque el middleware ya validó el tenant.
        var tenantHost = (_httpContextAccessor.HttpContext?.Items["Nexo_TenantHost"] as string)
                      ?? _httpContextAccessor.HttpContext?.Request.Headers["X-Nexo-Host"].FirstOrDefault()
                      ?? "";

        // JsonSerializer.Serialize incluye las comillas y escapa correctamente \, ", etc.
        // Necesario porque serverName puede contener SERVIDOR\INSTANCIA (barra invertida).
        var dbJson          = System.Text.Json.JsonSerializer.Serialize(db);
        var urlJson         = System.Text.Json.JsonSerializer.Serialize(url);
        var apiKeyJson      = System.Text.Json.JsonSerializer.Serialize(apiKey);
        var tenantHostJson  = System.Text.Json.JsonSerializer.Serialize(tenantHost);

        return $$"""
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "VisionsDb": {{dbJson}}
  },
  "NexoApi": {
    "BaseUrl": {{urlJson}},
    "ApiKey": {{apiKeyJson}},
    "TenantHost": {{tenantHostJson}}
  },
  "Sync": {
    "IntervalMinutes": {{(segundos > 0 ? 0 : minutos)}},
    "IntervalSeconds": {{segundos}},
    "AgentVersion": {{System.Text.Json.JsonSerializer.Serialize(_config.GetValue<string>("Sync:AgentVersion") ?? "1.0.0")}}
  }
}
""";
    }

    // ──────────── Sincronizacion de catalogos ────────────

    public async Task<IEnumerable<MarcaSyncItem>> ListarMarcasSyncAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<MarcaSyncItem>(
            "SELECT Codigo, Marca AS Nombre FROM Catalogo.Marca");
    }

    public async Task UpsertMarcaAsync(MarcaSyncItem item)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"MERGE Catalogo.Marca AS d
              USING (SELECT @Codigo AS Codigo) AS s ON d.Codigo = s.Codigo
              WHEN MATCHED THEN UPDATE SET Marca = @Nombre
              WHEN NOT MATCHED THEN INSERT (Codigo, Marca) VALUES (@Codigo, @Nombre);",
            new { item.Codigo, item.Nombre });
    }

    public async Task<IEnumerable<GrupoMayorSyncItem>> ListarGruposMayorSyncAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<GrupoMayorSyncItem>(
            "SELECT Codigo, Nombre FROM Catalogo.GrupoMayor");
    }

    public async Task UpsertGrupoMayorAsync(GrupoMayorSyncItem item)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"MERGE Catalogo.GrupoMayor AS d
              USING (SELECT @Codigo AS Codigo) AS s ON d.Codigo = s.Codigo
              WHEN MATCHED THEN UPDATE SET Nombre = @Nombre
              WHEN NOT MATCHED THEN INSERT (Codigo, Nombre) VALUES (@Codigo, @Nombre);",
            new { item.Codigo, item.Nombre });
    }

    public async Task<IEnumerable<GrupoMenorSyncItem>> ListarGruposMenorSyncAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<GrupoMenorSyncItem>(
            "SELECT Codigo, Nombre, GrupoMayor FROM Catalogo.GrupoMenor");
    }

    public async Task UpsertGrupoMenorAsync(GrupoMenorSyncItem item)
    {
        using var connection = _db.CreateConnection();
        // GrupoMayor debe existir en NEXO antes de insertar el menor.
        // Si no existe se omite este elemento (se sincronizara en la proxima ronda tras crear el mayor).
        var existeMayor = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Catalogo.GrupoMayor WHERE Codigo = @GrupoMayor", new { item.GrupoMayor });
        if (existeMayor == 0) return;

        await connection.ExecuteAsync(
            @"MERGE Catalogo.GrupoMenor AS d
              USING (SELECT @Codigo AS Codigo) AS s ON d.Codigo = s.Codigo
              WHEN MATCHED THEN UPDATE SET Nombre = @Nombre, GrupoMayor = @GrupoMayor
              WHEN NOT MATCHED THEN INSERT (Codigo, Nombre, GrupoMayor) VALUES (@Codigo, @Nombre, @GrupoMayor);",
            new { item.Codigo, item.Nombre, item.GrupoMayor });
    }

    public async Task<IEnumerable<IvaSyncItem>> ListarIvaSyncAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<IvaSyncItem>(
            "SELECT IvaID, Iva AS IvaValor, Descripcion FROM Catalogo.Iva");
    }

    public async Task UpsertIvaAsync(IvaSyncItem item)
    {
        using var connection = _db.CreateConnection();
        // IVA se une por el valor numerico (Iva), no por IvaID (que es autoincrement en NEXO).
        await connection.ExecuteAsync(
            @"MERGE Catalogo.Iva AS d
              USING (SELECT @IvaValor AS Iva) AS s ON d.Iva = s.Iva
              WHEN MATCHED THEN UPDATE SET Descripcion = @Descripcion
              WHEN NOT MATCHED THEN INSERT (Iva, Descripcion) VALUES (@IvaValor, @Descripcion);",
            new { item.IvaValor, item.Descripcion });
    }

    public async Task<IEnumerable<PresentacionSyncItem>> ListarPresentacionesSyncAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<PresentacionSyncItem>(
            "SELECT Codigo, Presentacion, Fracciones FROM Catalogo.Presentacion");
    }

    public async Task UpsertPresentacionAsync(PresentacionSyncItem item)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"MERGE Catalogo.Presentacion AS d
              USING (SELECT @Codigo AS Codigo) AS s ON d.Codigo = s.Codigo
              WHEN MATCHED THEN UPDATE SET Presentacion = @Presentacion, Fracciones = @Fracciones
              WHEN NOT MATCHED THEN INSERT (Codigo, Presentacion, Fracciones) VALUES (@Codigo, @Presentacion, @Fracciones);",
            new { item.Codigo, item.Presentacion, item.Fracciones });
    }

    // ──────────── Facturas NEXO → Visions ────────────

    public async Task<IEnumerable<FacturaParaVisionsItem>> ListarFacturasParaVisionsAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        // Solo facturas del CC de este agente con stock descontado que aun no se han exportado.
        // El filtro por CentroCostoID garantiza que cada agente solo procesa sus propias facturas
        // y la tabla FacturasExportadasVisions impide duplicados entre agentes del mismo CC.
        const string sqlFacturas = @"
            SELECT f.FacturaID, f.TipDoc, f.NroDoc, f.Fecha,
                   c.NIT AS ClienteNit, c.Nombre AS ClienteNombre
            FROM Facturacion.Facturas f
            JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
            WHERE f.CentroCostoID = @CentroCostoId
              AND NOT EXISTS (
                  SELECT 1 FROM Facturacion.FacturasExportadasVisions fev
                  WHERE fev.FacturaID = f.FacturaID AND fev.CentroCostoID = @CentroCostoId)";

        var facturas = (await connection.QueryAsync<dynamic>(sqlFacturas, new { CentroCostoId = centroCostoId })).ToList();
        if (facturas.Count == 0) return [];

        // ReferenciaVisions viene de Tarjetas.Referencia directamente (mapeo automatico).
        // Las lineas sin Referencia (combos o articulos sin codigo Visions) se omiten
        // pero NO bloquean la exportacion de la factura completa.
        const string sqlLineas = @"
            SELECT
                fl.LineaID,
                fl.FacturaID,
                ROW_NUMBER() OVER (PARTITION BY fl.FacturaID ORDER BY fl.LineaID) AS Orden,
                a.Referencia AS ReferenciaVisions,
                a.Nombre AS NombreArticulo,
                a.MarcaCodigo,
                a.GrupoMenorCodigo,
                fl.Cantidad,
                fl.PrecioUnitario,
                ISNULL(a.Costo, 0) AS Costo
            FROM Facturacion.FacturaLineas fl
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = fl.ArticuloID
            WHERE fl.ArticuloID IS NOT NULL
              AND a.Referencia IS NOT NULL AND a.Referencia <> ''";

        var todasLineas = (await connection.QueryAsync<dynamic>(sqlLineas)).ToList();

        var resultado = new List<FacturaParaVisionsItem>();
        foreach (var f in facturas)
        {
            var lineas = todasLineas
                .Where(l => l.FacturaID == f.FacturaID)
                .Select(l => new LineaFacturaParaVisionsItem(
                    (int)l.Orden,
                    (string?)l.ReferenciaVisions,
                    (string)l.NombreArticulo,
                    (string?)l.MarcaCodigo,
                    (string?)l.GrupoMenorCodigo,
                    (decimal)l.Cantidad,
                    (decimal)l.PrecioUnitario,
                    (decimal)l.Costo))
                .ToList();

            // Exportar si al menos una linea tiene Referencia de Visions.
            // Las lineas sin Referencia (combos u articulos sin codigo) se omiten silenciosamente.
            if (lineas.Count > 0)
            {
                resultado.Add(new FacturaParaVisionsItem(
                    (int)f.FacturaID, (string)f.TipDoc, (string?)f.NroDoc, (DateTime)f.Fecha,
                    (string?)f.ClienteNit, (string)f.ClienteNombre, lineas));
            }
        }

        return resultado;
    }

    public async Task MarcarFacturaExportadaVisionsAsync(int facturaId, int centroCostoId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"IF NOT EXISTS (SELECT 1 FROM Facturacion.FacturasExportadasVisions
                             WHERE FacturaID = @FacturaId AND CentroCostoID = @CentroCostoId)
              INSERT INTO Facturacion.FacturasExportadasVisions (FacturaID, CentroCostoID)
              VALUES (@FacturaId, @CentroCostoId)",
            new { FacturaId = facturaId, CentroCostoId = centroCostoId });
    }

    public async Task SyncArticuloDesdeVisionsAsync(SyncArticuloDesdeVisionsRequest r)
    {
        using var connection = _db.CreateConnection();

        // Buscar el articulo por su referencia en Visions a traves del mapeo.
        var articuloId = await connection.ExecuteScalarAsync<int?>(
            @"SELECT TOP 1 ma.ArticuloID
              FROM Integracion.MapeoArticulos ma
              JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
              WHERE ma.Estado = 1 AND cc.TieneVisions = 1
                AND cc.IdentificadorClienteVisions = @CentroCostoVisions
                AND ma.CodigoArticuloVisions = @ReferenciaVisions",
            new { r.ReferenciaVisions, r.CentroCostoVisions });

        if (articuloId is null)
        {
            // Articulo en Visions que aun no tiene representacion en NEXO.
            // Se auto-crea y se mapea para que la proxima sincronizacion lo actualice.
            var tipoId = !string.IsNullOrWhiteSpace(r.TipoProductoCodigo)
                ? await connection.ExecuteScalarAsync<int?>(
                    "SELECT TOP 1 TipoArticuloID FROM Catalogo.TiposArticulo WHERE Codigo = @Codigo",
                    new { Codigo = r.TipoProductoCodigo })
                : null;
            tipoId ??= await connection.ExecuteScalarAsync<int?>(
                "SELECT TOP 1 TipoArticuloID FROM Catalogo.TiposArticulo WHERE Nombre = 'Producto Terminado'");
            if (tipoId is null)
            {
                _logger.LogError("SyncArticuloDesdeVisions: Catalogo.TiposArticulo esta vacio — ejecutar seed de TiposArticulo en esta BD. Articulo {Ref} NO creado.", r.ReferenciaVisions);
                throw new InvalidOperationException("Catalogo.TiposArticulo vacio. Ejecutar seed SQL en la BD del tenant.");
            }

            var ccId = await connection.ExecuteScalarAsync<int?>(
                @"SELECT CentroCostoID FROM Organizacion.CentrosCosto
                  WHERE TieneVisions = 1 AND IdentificadorClienteVisions = @CentroCostoVisions",
                new { r.CentroCostoVisions });
            if (ccId is null)
            {
                _logger.LogError("SyncArticuloDesdeVisions: no existe CentroCosto con TieneVisions=1 e IdentificadorClienteVisions='{CC}'. Articulo {Ref} NO creado.", r.CentroCostoVisions, r.ReferenciaVisions);
                throw new InvalidOperationException($"CentroCosto con IdentificadorClienteVisions='{r.CentroCostoVisions}' no encontrado.");
            }

            // Auto-crear Marca y Presentacion si Visions trae codigos huerfanos
            // (codigos en TARJETA que no existen en dbo.MARCA / dbo.PRESENTACION).
            if (!string.IsNullOrWhiteSpace(r.MarcaCodigo))
                await connection.ExecuteAsync(
                    "IF NOT EXISTS (SELECT 1 FROM Catalogo.Marca WHERE Codigo = @Codigo) INSERT INTO Catalogo.Marca (Codigo, Marca) VALUES (@Codigo, @Codigo)",
                    new { Codigo = r.MarcaCodigo });
            if (!string.IsNullOrWhiteSpace(r.PresentacionCodigo))
                await connection.ExecuteAsync(
                    "IF NOT EXISTS (SELECT 1 FROM Catalogo.Presentacion WHERE Codigo = @Codigo) INSERT INTO Catalogo.Presentacion (Codigo, Presentacion) VALUES (@Codigo, @Codigo)",
                    new { Codigo = r.PresentacionCodigo });

            var insertadoActualizar = await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM Catalogo.Tarjetas WHERE Referencia = @Referencia)
                INSERT INTO Catalogo.Tarjetas
                    (Referencia, Nombre, TipoArticuloID, PPublico, Costo,
                     PBodega, PCredito, UPublico, UBodega, UCredito,
                     StockMinimo, PuntoReorden, Fracciona,
                     MarcaCodigo, GrupoMenorCodigo, PresentacionCodigo,
                     IvaSiNo, IvaValor, IvaDescripcion,
                     Iva2, IvaDescripcion2)
                VALUES
                    (@Referencia, @Nombre, @TipoArticuloID, @PPublico, @Costo,
                     @PBodega, @PCredito, @UPublico, @UBodega, @UCredito,
                     @StockMinimo, 0, CASE WHEN ISNULL(@Fracciones, 0) > 1 THEN 'SI' ELSE 'NO' END,
                     @MarcaCodigo,
                     (SELECT Codigo FROM Catalogo.GrupoMenor WHERE Codigo = @GrupoMenorCodigo),
                     @PresentacionCodigo,
                     @IvaSiNo, @IvaValor, @IvaDescripcion,
                     @Iva2, @IvaDescripcion2)",
                new {
                    Referencia         = r.ReferenciaVisions,
                    Nombre             = r.Nombre ?? r.ReferenciaVisions,
                    TipoArticuloID     = tipoId.Value,
                    PPublico           = r.PPublico ?? 0m,
                    Costo              = r.Costo ?? 0m,
                    StockMinimo        = r.ExistenciasMinimas ?? 0m,
                    PBodega            = r.PBodega > 0 ? r.PBodega : (decimal?)null,
                    PCredito           = r.PCredito > 0 ? r.PCredito : (decimal?)null,
                    UPublico           = r.UPublico > 0 ? r.UPublico : (decimal?)null,
                    UBodega            = r.UBodega  > 0 ? r.UBodega  : (decimal?)null,
                    UCredito           = r.UCredito > 0 ? r.UCredito : (decimal?)null,
                    MarcaCodigo        = string.IsNullOrWhiteSpace(r.MarcaCodigo)        ? null : r.MarcaCodigo,
                    GrupoMenorCodigo   = string.IsNullOrWhiteSpace(r.GrupoMenorCodigo)   ? null : r.GrupoMenorCodigo,
                    PresentacionCodigo = string.IsNullOrWhiteSpace(r.PresentacionCodigo) ? null : r.PresentacionCodigo,
                    IvaSiNo            = r.IvaSiNo ?? "SI",
                    IvaValor           = (short)(r.IvaValor.HasValue ? Math.Clamp((long)r.IvaValor.Value, short.MinValue, short.MaxValue) : 19L),
                    IvaDescripcion     = r.IvaDescripcion ?? "IVA 19%",
                    Iva2               = r.Iva2.HasValue ? (short?)Math.Clamp((long)r.Iva2.Value, short.MinValue, short.MaxValue) : null,
                    IvaDescripcion2    = r.IvaDescripcion2,
                    Fracciones         = r.Fracciones
                });

            articuloId = await connection.ExecuteScalarAsync<int?>(
                "SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Referencia",
                new { Referencia = r.ReferenciaVisions });

            if (insertadoActualizar > 0 && articuloId.HasValue)
                await RegistrarPreciosInicialesAsync(connection, articuloId.Value,
                    r.PPublico, r.Costo, r.PBodega > 0 ? r.PBodega : null, r.PCredito > 0 ? r.PCredito : null);
            if (articuloId is null) return;

            await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM Integracion.MapeoArticulos
                               WHERE ArticuloID = @ArticuloID AND CentroCostoID = @CentroCostoID
                                 AND CodigoArticuloVisions = @Codigo)
                INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado, FechaCreacion)
                VALUES (@ArticuloID, @CentroCostoID, @Codigo, 1, GETDATE())",
                new { ArticuloID = articuloId.Value, CentroCostoID = ccId.Value, Codigo = r.ReferenciaVisions });

            await connection.ExecuteAsync(@"
                UPDATE Integracion.ArticulosPendientesMapeo
                SET Resuelto = 1, FechaResuelto = GETDATE()
                WHERE CodigoArticuloVisions = @Codigo AND CentroCostoID = @CcId AND Resuelto = 0",
                new { Codigo = r.ReferenciaVisions, CcId = ccId.Value });

            // Inicializar stock en NEXO con las existencias actuales de Visions (solo en auto-create).
            // Solo aplica si Visions reporta stock > 0 y el CentroCosto tiene bodega de venta configurada.
            if ((r.ExistenciasActuales ?? 0m) > 0m)
            {
                var bodegaId = await connection.ExecuteScalarAsync<int?>(
                    "SELECT BodegaVentaVisionsID FROM Organizacion.CentrosCosto WHERE CentroCostoID = @CcId",
                    new { CcId = ccId.Value });
                if (bodegaId.HasValue)
                    await connection.ExecuteAsync(@"
                        IF NOT EXISTS (SELECT 1 FROM Inventario.InventarioStock
                                       WHERE ArticuloID = @ArtId AND BodegaID = @BodId)
                        INSERT INTO Inventario.InventarioStock
                            (ArticuloID, BodegaID, CantidadActual, CostoUnitarioLote, FechaUltimaActualizacion)
                        VALUES (@ArtId, @BodId, @Cantidad, @Costo, GETDATE())",
                        new { ArtId = articuloId.Value, BodId = bodegaId.Value,
                              Cantidad = r.ExistenciasActuales!.Value, Costo = r.Costo ?? 0m });
            }

            // Fan-out: mapear y emitir SINCRONIZAR_ARTICULO para todos los otros CentroCostos
            // con TieneVisions=1. El agente de cada punto recibe el evento y crea/actualiza
            // el articulo en su propio TARJETA. Sin esto, articulos creados en un punto
            // nunca llegarian a los demas puntos de venta.
            await connection.ExecuteAsync(@"
                INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado, FechaCreacion)
                SELECT @ArticuloID, cc.CentroCostoID, @Codigo, 1, GETDATE()
                FROM Organizacion.CentrosCosto cc
                WHERE cc.TieneVisions = 1 AND cc.CentroCostoID <> @OriginCC
                  AND NOT EXISTS (
                      SELECT 1 FROM Integracion.MapeoArticulos ma2
                      WHERE ma2.ArticuloID = @ArticuloID AND ma2.CentroCostoID = cc.CentroCostoID AND ma2.Estado = 1);

                INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
                SELECT 'SINCRONIZAR_ARTICULO', ma.CentroCostoID, ma.ArticuloID, 0, ISNULL(t.CostoPromedio, 0)
                FROM Integracion.MapeoArticulos ma
                JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
                JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                WHERE ma.ArticuloID = @ArticuloID AND ma.Estado = 1
                  AND cc.TieneVisions = 1 AND cc.CentroCostoID <> @OriginCC
                  AND NOT EXISTS (
                      SELECT 1 FROM Integracion.EventosSalientes e
                      WHERE e.ArticuloID = @ArticuloID AND e.CentroCostoID = ma.CentroCostoID
                        AND e.TipoEvento = 'SINCRONIZAR_ARTICULO' AND e.Estado IN ('PENDIENTE','CONFIRMADO','ERROR'));",
                new { ArticuloID = articuloId.Value, Codigo = r.ReferenciaVisions, OriginCC = ccId.Value });

            if (!string.IsNullOrWhiteSpace(r.ImagenBase64) && articuloId.HasValue)
            {
                try { await _catalogoService.ActualizarImagenArticuloAsync(articuloId.Value, new ActualizarImagenRequest(r.ImagenBase64, r.ImagenContentType ?? "image/jpeg")); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo guardar imagen para articulo {Ref} (no critico)", r.ReferenciaVisions); }
            }

            _logger.LogInformation("Articulo {Ref} auto-creado en NEXO desde SINCRONIZAR_ARTICULO", r.ReferenciaVisions);
            return;
        }

        // Solo aplica si el cambio de Visions es mas de 5 segundos posterior
        // a la ultima modificacion de NEXO (evita bucle: NEXO→Visions→NEXO).

        // Auto-crear Marca y Presentacion si Visions trae codigos huerfanos.
        // GrupoMenor se omite condicionalmente (requiere GrupoMayor padre que no viaja en el request).
        if (!string.IsNullOrWhiteSpace(r.MarcaCodigo))
            await connection.ExecuteAsync(
                "IF NOT EXISTS (SELECT 1 FROM Catalogo.Marca WHERE Codigo = @Codigo) INSERT INTO Catalogo.Marca (Codigo, Marca) VALUES (@Codigo, @Codigo)",
                new { Codigo = r.MarcaCodigo });
        if (!string.IsNullOrWhiteSpace(r.PresentacionCodigo))
            await connection.ExecuteAsync(
                "IF NOT EXISTS (SELECT 1 FROM Catalogo.Presentacion WHERE Codigo = @Codigo) INSERT INTO Catalogo.Presentacion (Codigo, Presentacion) VALUES (@Codigo, @Codigo)",
                new { Codigo = r.PresentacionCodigo });

        var filas = await connection.ExecuteAsync(
            @"UPDATE Catalogo.Tarjetas
              SET Nombre             = COALESCE(@Nombre,  Nombre),
                  Costo              = COALESCE(@Costo,   Costo),
                  PPublico           = COALESCE(@PPublico, PPublico),
                  PBodega            = CASE WHEN @PBodega IS NOT NULL THEN @PBodega ELSE PBodega END,
                  PCredito           = CASE WHEN @PCredito IS NOT NULL THEN @PCredito ELSE PCredito END,
                  UPublico           = CASE WHEN @UPublico IS NOT NULL THEN @UPublico ELSE UPublico END,
                  UBodega            = CASE WHEN @UBodega IS NOT NULL THEN @UBodega ELSE UBodega END,
                  UCredito           = CASE WHEN @UCredito IS NOT NULL THEN @UCredito ELSE UCredito END,
                  MarcaCodigo        = COALESCE(NULLIF(@MarcaCodigo, ''),        MarcaCodigo),
                  GrupoMenorCodigo   = CASE WHEN NULLIF(@GrupoMenorCodigo, '') IS NULL THEN GrupoMenorCodigo
                                            WHEN EXISTS (SELECT 1 FROM Catalogo.GrupoMenor WHERE Codigo = @GrupoMenorCodigo) THEN @GrupoMenorCodigo
                                            ELSE GrupoMenorCodigo END,
                  PresentacionCodigo = COALESCE(NULLIF(@PresentacionCodigo, ''), PresentacionCodigo),
                  IvaSiNo            = COALESCE(@IvaSiNo,            IvaSiNo),
                  IvaValor           = COALESCE(@IvaValor,           IvaValor),
                  IvaDescripcion     = COALESCE(@IvaDescripcion,     IvaDescripcion),
                  Iva2               = COALESCE(@Iva2,               Iva2),
                  IvaDescripcion2    = COALESCE(@IvaDescripcion2,    IvaDescripcion2),
                  TipoArticuloID     = CASE WHEN @TipoProductoCodigo IS NOT NULL
                                            THEN COALESCE(
                                                (SELECT TOP 1 TipoArticuloID FROM Catalogo.TiposArticulo WHERE Codigo = @TipoProductoCodigo),
                                                TipoArticuloID)
                                            ELSE TipoArticuloID END,
                  StockMinimo        = COALESCE(@ExistenciasMinimas, StockMinimo),
                  Fracciona          = CASE WHEN @Fracciones IS NOT NULL AND @Fracciones > 1 THEN 'SI' ELSE Fracciona END,
                  FechaModificacion  = @FechaCambio
              WHERE ArticuloID = @ArticuloId",
            new {
                r.Nombre, r.Costo, r.PPublico,
                PBodega  = r.PBodega  > 0 ? r.PBodega  : (decimal?)null,
                PCredito = r.PCredito > 0 ? r.PCredito : (decimal?)null,
                UPublico = r.UPublico > 0 ? r.UPublico : (decimal?)null,
                UBodega  = r.UBodega  > 0 ? r.UBodega  : (decimal?)null,
                UCredito = r.UCredito > 0 ? r.UCredito : (decimal?)null,
                r.MarcaCodigo, r.GrupoMenorCodigo, r.PresentacionCodigo,
                r.IvaSiNo,
                IvaValor = r.IvaValor.HasValue ? (short?)Math.Clamp((long)r.IvaValor.Value, short.MinValue, short.MaxValue) : null,
                r.IvaDescripcion,
                Iva2 = r.Iva2.HasValue ? (short?)Math.Clamp((long)r.Iva2.Value, short.MinValue, short.MaxValue) : null,
                r.IvaDescripcion2,
                r.TipoProductoCodigo,
                ExistenciasMinimas = r.ExistenciasMinimas,
                Fracciones         = r.Fracciones,
                r.FechaCambio, ArticuloId = articuloId
            });

        if (filas > 0)
        {
            _logger.LogInformation(
                "Articulo {ID} actualizado desde Visions (ref {Ref})", articuloId, r.ReferenciaVisions);

            // Fan-out: al actualizar desde un punto VISIONS, crear mapeos y eventos
            // SINCRONIZAR_ARTICULO para todos los demas CentroCostos con TieneVisions=1.
            // Esto propaga gradualmente articulos existentes a puntos que aun no los tienen,
            // sin necesidad de un script de migracion masivo.
            var originCcId = await connection.ExecuteScalarAsync<int?>(
                @"SELECT CentroCostoID FROM Organizacion.CentrosCosto
                  WHERE TieneVisions = 1 AND IdentificadorClienteVisions = @CentroCostoVisions",
                new { r.CentroCostoVisions });

            if (originCcId.HasValue)
                await connection.ExecuteAsync(@"
                    INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado, FechaCreacion)
                    SELECT @ArticuloID, cc.CentroCostoID, @Codigo, 1, GETDATE()
                    FROM Organizacion.CentrosCosto cc
                    WHERE cc.TieneVisions = 1 AND cc.CentroCostoID <> @OriginCC
                      AND NOT EXISTS (
                          SELECT 1 FROM Integracion.MapeoArticulos ma2
                          WHERE ma2.ArticuloID = @ArticuloID AND ma2.CentroCostoID = cc.CentroCostoID AND ma2.Estado = 1);

                    INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
                    SELECT 'SINCRONIZAR_ARTICULO', ma.CentroCostoID, ma.ArticuloID, 0, ISNULL(t.CostoPromedio, 0)
                    FROM Integracion.MapeoArticulos ma
                    JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
                    JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                    WHERE ma.ArticuloID = @ArticuloID AND ma.Estado = 1
                      AND cc.TieneVisions = 1 AND cc.CentroCostoID <> @OriginCC
                      AND NOT EXISTS (
                          SELECT 1 FROM Integracion.EventosSalientes e
                          WHERE e.ArticuloID = @ArticuloID AND e.CentroCostoID = ma.CentroCostoID
                            AND e.TipoEvento = 'SINCRONIZAR_ARTICULO' AND e.Estado IN ('PENDIENTE','CONFIRMADO','ERROR'));",
                    new { ArticuloID = articuloId, Codigo = r.ReferenciaVisions, OriginCC = originCcId.Value });
        }

        if (!string.IsNullOrWhiteSpace(r.ImagenBase64))
        {
            try { await _catalogoService.ActualizarImagenArticuloAsync(articuloId!.Value, new ActualizarImagenRequest(r.ImagenBase64, r.ImagenContentType ?? "image/jpeg")); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo guardar imagen para articulo {Ref} (no critico)", r.ReferenciaVisions); }
        }
    }

    private record ImagenRow(string Referencia, byte[] Imagen, string ContentType);

    public async Task<IEnumerable<ArticuloImagenParaSyncDto>> ListarArticulosConImagenActualizadaAsync(DateTime? desde)
    {
        using var connection = _db.CreateConnection();
        var rows = await connection.QueryAsync<ImagenRow>(
            @"SELECT t.Referencia, t.Imagen, ISNULL(t.ImagenContentType, 'image/jpeg') AS ContentType
              FROM Catalogo.Tarjetas t
              WHERE t.Imagen IS NOT NULL
                AND (@Desde IS NULL OR t.FechaImagenActualizada >= @Desde)",
            new { Desde = desde });
        return rows.Select(r => new ArticuloImagenParaSyncDto(r.Referencia, Convert.ToBase64String(r.Imagen), r.ContentType));
    }

    public async Task<IEnumerable<ClienteParaSyncDto>> ListarClientesParaSyncAsync(DateTime? desde, int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        return await connection.QueryAsync<ClienteParaSyncDto>(
            @"SELECT c.ClienteID, c.NIT, c.Nombre, c.Telefono, c.Email, c.Direccion, c.FechaModificacion,
                     c.TipoPersona, c.PrimerNombre, c.SegundoNombre, c.PrimerApellido, c.SegundoApellido,
                     c.Departamento, c.Ciudad,
                     ti.Detalle AS TipoIdentificacionDetalle,
                     m.NombreDept, m.NombreMuni,
                     c.CodigoDept, c.CodigoMuni,
                     c.DigitoVerificacion,
                     CAST(c.Estado AS BIT) AS Estado
              FROM Crm.Clientes c
              LEFT JOIN Catalogo.TiposIdentificacion ti ON ti.Codigo = c.TipoIdentificacion
              LEFT JOIN Catalogo.Municipios m ON m.CodigoDept = c.CodigoDept AND m.CodigoMuni = c.CodigoMuni
              WHERE
                -- Sync inicial (desde=null): solo activos
                -- Sync incremental (desde!=null): activos modificados + inactivos recientes (para propagar el de-rol)
                ((c.Estado = 1 AND (@Desde IS NULL OR c.FechaModificacion IS NULL OR c.FechaModificacion > @Desde))
                 OR (c.Estado = 0 AND @Desde IS NOT NULL AND c.FechaModificacion > @Desde))
              ORDER BY c.Estado DESC, c.FechaModificacion",
            new { Desde = desde });
    }

    public async Task InactivarArticuloDesdeVisionsAsync(string referencia, string centroCostoVisions)
    {
        using var connection = _db.CreateConnection();
        // Solo inactivar si ningún otro CentroCosto con Visions sigue mapeando este artículo.
        // Evita que eliminar en CC1 desactive el artículo en CC2 y CC3.
        var filas = await connection.ExecuteAsync(@"
            UPDATE Catalogo.Tarjetas
            SET Estado = 0, FechaModificacion = GETDATE()
            WHERE Referencia = @Referencia AND Estado = 1
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma
                  JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                  WHERE ma.ArticuloID = (SELECT TOP 1 ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Referencia)
                    AND ma.Estado = 1
                    AND cc.TieneVisions = 1
                    AND cc.IdentificadorClienteVisions <> @CentroCostoVisions)",
            new { Referencia = referencia, CentroCostoVisions = centroCostoVisions });

        if (filas > 0)
            _logger.LogInformation("Articulo {Ref} inactivado en NEXO (eliminado en Visions CC {CC})", referencia, centroCostoVisions);
        else
            _logger.LogInformation("Articulo {Ref} eliminado en Visions CC {CC} pero sigue activo en otros centros — no inactivado en NEXO", referencia, centroCostoVisions);
    }

    public async Task SyncClienteDesdeVisionsAsync(SyncClienteDesdeVisionsRequest r)
    {
        using var connection = _db.CreateConnection();

        // Determinar nombre para mostrar y tipo
        var tipoPersona = string.IsNullOrWhiteSpace(r.TipoPersona)
            ? (string.IsNullOrWhiteSpace(r.NombreEmpresa) ? "Natural" : "Juridica")
            : r.TipoPersona;

        // Para Juridica: usar EMPRESA primero, luego NOMBRE1 (cuando Visions pone el nombre ahi,
        // como en CONSUMIDOR FINAL), luego NIT como ultimo recurso.
        // Para Natural: concatenar los campos de nombre; si todos vacíos, usar NIT.
        var nombre = tipoPersona == "Juridica"
            ? (!string.IsNullOrWhiteSpace(r.NombreEmpresa) ? r.NombreEmpresa
               : !string.IsNullOrWhiteSpace(r.PrimerNombre) ? r.PrimerNombre
               : null) ?? r.NIT
            : string.Join(" ", new[] { r.PrimerNombre, r.SegundoNombre, r.PrimerApellido, r.SegundoApellido }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(nombre)) nombre = r.NIT;

        var tipoCliente = tipoPersona == "Natural" ? "Persona Natural" : "Empresa";

        // Reverse-lookup código de tipo ID desde el tipo de persona
        var tipoIdCodigo = await connection.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 Codigo FROM Catalogo.TiposIdentificacion WHERE UPPER(Detalle) = UPPER(@Det)",
            new { Det = tipoPersona == "Juridica" ? "NIT" : "CEDULA DE CIUDADANIA" });

        // Si el agente ya envió los códigos (desde CIUDADCODIGO/DEPARTAMENTOCODIGO de Visions), los usamos directamente.
        // Si no, hacemos reverse-lookup por texto como fallback.
        string? codigoDept = r.CodigoDept;
        string? codigoMuni = r.CodigoMuni;
        if (string.IsNullOrWhiteSpace(codigoDept) && !string.IsNullOrWhiteSpace(r.Departamento))
        {
            var municipio = await connection.QuerySingleOrDefaultAsync<(string? CodigoDept, string? CodigoMuni)>(
                @"SELECT TOP 1 CodigoDept, CodigoMuni FROM Catalogo.Municipios
                  WHERE UPPER(NombreDept) = UPPER(@Dept) AND UPPER(NombreMuni) = UPPER(@Ciudad)",
                new { Dept = r.Departamento, Ciudad = r.Ciudad });
            codigoDept = municipio.CodigoDept;
            codigoMuni = municipio.CodigoMuni;
        }

        await connection.ExecuteAsync(@"
            MERGE Crm.Clientes AS dest
            USING (SELECT @NIT AS NIT) AS src ON dest.NIT = src.NIT
            WHEN MATCHED THEN UPDATE SET
                Nombre = @Nombre, Telefono = @Telefono, Email = @Email, Direccion = @Direccion,
                TipoPersona = @TipoPersona, TipoCliente = @TipoCliente,
                PrimerNombre = @PrimerNombre, SegundoNombre = @SegundoNombre,
                PrimerApellido = @PrimerApellido, SegundoApellido = @SegundoApellido,
                Departamento = @Departamento, Ciudad = @Ciudad,
                Pais = 'COLOMBIA', CodigoPais = 'CO',
                TipoIdentificacion = ISNULL(@TipoIdentificacion, TipoIdentificacion),
                CodigoDept = ISNULL(@CodigoDept, CodigoDept),
                CodigoMuni = ISNULL(@CodigoMuni, CodigoMuni),
                DigitoVerificacion = ISNULL(@DigitoVerificacion, DigitoVerificacion)
                -- NO se actualiza FechaModificacion: el cambio vino de Visions,
                -- si la actualizáramos el próximo ciclo NEXO→Visions lo re-enviaría (ping-pong).
            WHEN NOT MATCHED THEN INSERT
                (Nombre, NIT, Telefono, Email, Direccion, TipoPersona, TipoCliente,
                 PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                 Departamento, Ciudad, Pais, CodigoPais, TipoIdentificacion, CodigoDept, CodigoMuni,
                 DigitoVerificacion, Estado, FechaCreacion, FechaModificacion)
            VALUES
                (@Nombre, @NIT, @Telefono, @Email, @Direccion, @TipoPersona, @TipoCliente,
                 @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                 @Departamento, @Ciudad, 'COLOMBIA', 'CO', @TipoIdentificacion, @CodigoDept, @CodigoMuni,
                 @DigitoVerificacion, 1, GETDATE(), GETDATE());",
            new
            {
                r.NIT, Nombre = nombre, r.Telefono, r.Email, r.Direccion,
                TipoPersona = tipoPersona, TipoCliente = tipoCliente,
                r.PrimerNombre, r.SegundoNombre, r.PrimerApellido, r.SegundoApellido,
                r.Departamento, r.Ciudad,
                TipoIdentificacion = tipoIdCodigo,
                CodigoDept = codigoDept,
                CodigoMuni = codigoMuni,
                r.DigitoVerificacion
            });
    }

    public async Task<IEnumerable<ProveedorParaSyncDto>> ListarProveedoresParaSyncAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<ProveedorParaSyncDto>(
            @"SELECT ProveedorID, NIT, RazonSocial, Contacto, Telefono, Email, Direccion,
                     TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                     TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                     CodigoDept, CodigoMuni, Pais, CodigoPais,
                     CAST(Estado AS BIT) AS Estado
              FROM Catalogo.Proveedores
              WHERE Estado = 1
                 OR (Estado = 0 AND FechaModificacion >= DATEADD(DAY, -7, GETDATE()))
              ORDER BY Estado DESC, RazonSocial");
    }

    public async Task SyncProveedorDesdeVisionsAsync(SyncProveedorDesdeVisionsRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.RazonSocial))
            return;

        using var connection = _db.CreateConnection();

        var tipoPersona = string.IsNullOrWhiteSpace(r.TipoPersona)
            ? (string.IsNullOrWhiteSpace(r.PrimerNombre) && string.IsNullOrWhiteSpace(r.PrimerApellido)
               ? "Juridica" : "Natural")
            : r.TipoPersona;

        // Reverse-lookup código de tipo ID
        var tipoIdCodigo = await connection.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 Codigo FROM Catalogo.TiposIdentificacion WHERE UPPER(Detalle) = UPPER(@Det)",
            new { Det = tipoPersona == "Juridica" ? "NIT" : "CEDULA DE CIUDADANIA" });

        string? codigoDept = r.CodigoDept;
        string? codigoMuni = r.CodigoMuni;
        if (string.IsNullOrWhiteSpace(codigoDept) && !string.IsNullOrWhiteSpace(r.Departamento))
        {
            var muni = await connection.QuerySingleOrDefaultAsync<(string? CodigoDept, string? CodigoMuni)>(
                @"SELECT TOP 1 CodigoDept, CodigoMuni FROM Catalogo.Municipios
                  WHERE UPPER(NombreDept) = UPPER(@Dept) AND UPPER(NombreMuni) = UPPER(@Ciudad)",
                new { Dept = r.Departamento, Ciudad = r.Ciudad });
            codigoDept = muni.CodigoDept;
            codigoMuni = muni.CodigoMuni;
        }

        await connection.ExecuteAsync(@"
            MERGE Catalogo.Proveedores AS dest
            USING (SELECT @NIT AS NIT) AS src ON dest.NIT = src.NIT
            WHEN MATCHED THEN UPDATE SET
                RazonSocial        = @RazonSocial,
                Telefono           = ISNULL(@Telefono, Telefono),
                Email              = ISNULL(@Email, Email),
                Direccion          = ISNULL(@Direccion, Direccion),
                TipoPersona        = @TipoPersona,
                PrimerNombre       = @PrimerNombre,
                SegundoNombre      = @SegundoNombre,
                PrimerApellido     = @PrimerApellido,
                SegundoApellido    = @SegundoApellido,
                TipoIdentificacion = ISNULL(@TipoIdentificacion, TipoIdentificacion),
                DigitoVerificacion = @DigitoVerificacion,
                Departamento       = ISNULL(@Departamento, Departamento),
                Ciudad             = ISNULL(@Ciudad, Ciudad),
                CodigoDept         = ISNULL(@CodigoDept, CodigoDept),
                CodigoMuni         = ISNULL(@CodigoMuni, CodigoMuni),
                Pais               = 'COLOMBIA', CodigoPais = 'CO',
                Estado             = 1,
                FechaModificacion  = GETDATE()
            WHEN NOT MATCHED THEN INSERT
                (NIT, RazonSocial, Telefono, Email, Direccion,
                 TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                 TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                 CodigoDept, CodigoMuni, Pais, CodigoPais, Estado, FechaModificacion)
            VALUES
                (@NIT, @RazonSocial, @Telefono, @Email, @Direccion,
                 @TipoPersona, @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                 @TipoIdentificacion, @DigitoVerificacion, @Departamento, @Ciudad,
                 @CodigoDept, @CodigoMuni, 'COLOMBIA', 'CO', 1, GETDATE());",
            new
            {
                r.NIT, r.RazonSocial, r.Telefono, r.Email, r.Direccion,
                TipoPersona        = tipoPersona,
                r.PrimerNombre, r.SegundoNombre, r.PrimerApellido, r.SegundoApellido,
                TipoIdentificacion = tipoIdCodigo,
                r.DigitoVerificacion,
                r.Departamento, r.Ciudad,
                CodigoDept         = codigoDept,
                CodigoMuni         = codigoMuni
            });
    }

    public async Task ActualizarNumeroVisionsAsync(int facturaId, ActualizarNumeroVisionsRequest request)
    {
        using var connection = _db.CreateConnection();

        await connection.ExecuteAsync(
            @"UPDATE Facturacion.Facturas
              SET TipDoc = @TipDoc, NroDoc = @NroDoc, VisionsConfirmado = 1
              WHERE FacturaID = @FacturaId",
            new { FacturaId = facturaId, request.TipDoc, request.NroDoc });

        // Registrar pago automático si Visions confirmó y aún no hay pagos en NEXO.
        // Esto cambia el estado de la factura a PAGADA sin intervención manual.
        try
        {
            var totalFactura = await connection.ExecuteScalarAsync<decimal>(
                @"SELECT ISNULL(SUM(Cantidad * PrecioUnitario), 0)
                  FROM Facturacion.FacturaLineas WHERE FacturaID = @FacturaId",
                new { FacturaId = facturaId });

            if (totalFactura > 0)
            {
                var pagosExistentes = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM Facturacion.Pagos WHERE FacturaID = @FacturaId",
                    new { FacturaId = facturaId });

                if (pagosExistentes == 0)
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO Facturacion.Pagos (FacturaID, Monto, FechaPago, MetodoPago, Notas, UsuarioID)
                          VALUES (@FacturaID, @Monto, GETDATE(), 'VISIONS', 'Pago registrado automáticamente desde Visions (' + @NroDoc + ')', NULL)",
                        new { FacturaID = facturaId, Monto = totalFactura, request.NroDoc });
                }
            }
        }
        catch (Exception exPago) { _logger.LogError(exPago, "NEXO: pago automático falló para FacturaID={FacturaId}; confirmación Visions ya registrada", facturaId); }

        // Descontar stock automáticamente ahora que Visions confirmó.
        // Respetar el flag VentaDesdeReceta igual que en DescontarStockAsync de NEXO.
        try
        {
            var ventaDesdeReceta = await connection.ExecuteScalarAsync<bool>(
                "SELECT ISNULL(VentaDesdeReceta, 0) FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");
            var spDescuento = ventaDesdeReceta
                ? "Facturacion.sp_DescontarStockFactura_Receta"
                : "Facturacion.sp_DescontarStockFactura";
            var p = new Dapper.DynamicParameters();
            p.Add("FacturaID", facturaId);
            p.Add("UsuarioID", 0);
            await connection.ExecuteAsync(spDescuento, p, commandType: System.Data.CommandType.StoredProcedure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descontar stock para factura {FacturaID} desde VISIONS", facturaId);
        }
    }

    public async Task<SaludCatalogoResponse> ObtenerSaludCatalogoAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleAsync<SaludCatalogoResponse>(@"
            SELECT
                (SELECT COUNT(*) FROM Catalogo.Tarjetas)                                                      AS TotalArticulos,
                (SELECT COUNT(*) FROM Catalogo.Tarjetas WHERE MarcaCodigo IS NOT NULL AND IvaValor IS NOT NULL) AS ArticulosCompletos,
                (SELECT COUNT(*) FROM Catalogo.Tarjetas WHERE MarcaCodigo IS NULL OR IvaValor IS NULL)         AS ArticulosSinDatos,
                (SELECT COUNT(*) FROM Catalogo.Marca)                                                          AS TotalMarcas,
                (SELECT COUNT(*) FROM Catalogo.GrupoMayor)                                                     AS TotalGruposMayor,
                (SELECT COUNT(*) FROM Catalogo.GrupoMenor)                                                     AS TotalGruposMenor,
                (SELECT COUNT(*) FROM Catalogo.Presentacion)                                                   AS TotalPresentaciones,
                (SELECT COUNT(*) FROM Crm.Clientes WHERE Estado = 1)                                          AS TotalClientes");
    }

    private record VentaVisionsCruda(
        int TotalRegistros,
        int CentroCostoID, string TipDoc, string NroDoc, DateTime Fecha,
        string? NitCliente, string? NombreCliente,
        decimal TotalVenta, int Lineas
    );

    public async Task<VentasVisionsPaginadasResponse> ListarVentasVisionsAsync(
        int? centroCostoId, string? tipDoc, DateTime? desde, DateTime? hasta,
        int pagina = 1, int tamano = 50)
    {
        var offset = (pagina - 1) * tamano;
        const string sql = @"
            SELECT
                COUNT(*) OVER () AS TotalRegistros,
                ee.CentroCostoID,
                ee.TipDoc,
                ee.NroDoc,
                CAST(ee.FechaEventoOrigen AS DATE) AS Fecha,
                MAX(ee.NitCliente)    AS NitCliente,
                MAX(ee.NombreCliente) AS NombreCliente,
                SUM(ISNULL(ee.Cantidad, 0) * ISNULL(ee.PrecioArticuloVisions, 0)) AS TotalVenta,
                COUNT(*) AS Lineas
            FROM Integracion.EventosEntrantes ee
            WHERE ee.TipDoc IS NOT NULL
              AND (@CentroCostoId IS NULL OR ee.CentroCostoID = @CentroCostoId)
              AND (@TipDoc       IS NULL OR ee.TipDoc = @TipDoc)
              AND (@Desde        IS NULL OR ee.FechaEventoOrigen >= @Desde)
              AND (@Hasta        IS NULL OR ee.FechaEventoOrigen <  DATEADD(DAY, 1, @Hasta))
            GROUP BY ee.CentroCostoID, ee.TipDoc, ee.NroDoc, CAST(ee.FechaEventoOrigen AS DATE)
            ORDER BY Fecha DESC, ee.NroDoc
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY";

        using var conn = _db.CreateConnection();
        var rows = (await conn.QueryAsync<VentaVisionsCruda>(sql,
            new { CentroCostoId = centroCostoId, TipDoc = tipDoc, Desde = desde, Hasta = hasta, Offset = offset, Tamano = tamano })).ToList();

        var total = rows.FirstOrDefault()?.TotalRegistros ?? 0;
        var items = rows.Select(r => new VentaVisionsItem(
            r.CentroCostoID, r.TipDoc, r.NroDoc, r.Fecha,
            r.NitCliente, r.NombreCliente, r.TotalVenta, r.Lineas)).ToList();
        return new VentasVisionsPaginadasResponse(items, total, pagina, tamano);
    }

    public async Task<IEnumerable<VentaVisionsLineaItem>> ListarLineasVentaVisionsAsync(
        int centroCostoId, string tipDoc, string nroDoc)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT
                ee.CodigoArticuloVisions AS CodigoArticulo,
                ee.NombreArticuloVisions AS NombreArticulo,
                ISNULL(ee.Cantidad, 0) AS Cantidad,
                ISNULL(ee.PrecioArticuloVisions, 0) AS PrecioUnitario,
                CAST(ISNULL(ee.Cantidad, 0) * ISNULL(ee.PrecioArticuloVisions, 0) AS DECIMAL(18,0)) AS Subtotal
            FROM Integracion.EventosEntrantes ee
            WHERE ee.CentroCostoID = @CentroCostoID
              AND ee.TipDoc       = @TipDoc
              AND ee.NroDoc       = @NroDoc
            ORDER BY ee.EventoEntranteID";
        return await conn.QueryAsync<VentaVisionsLineaItem>(sql,
            new { CentroCostoID = centroCostoId, TipDoc = tipDoc, NroDoc = nroDoc });
    }

    public async Task<IEnumerable<PendienteLimpiezaVisions>> ListarPendientesLimpiezaVisionsAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<PendienteLimpiezaVisions>(
            "SELECT LimpiezaID, Tipo, EntidadID FROM Integracion.PendientesLimpiezaVisions ORDER BY LimpiezaID");
    }

    public async Task MarcarLimpiezaVisionsCompletadaAsync(int limpiezaId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM Integracion.PendientesLimpiezaVisions WHERE LimpiezaID = @LimpiezaID",
            new { LimpiezaID = limpiezaId });
    }

    public async Task<bool> ObtenerSincAntsaAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<bool>(
            "SELECT ISNULL(SincAntsaActivo, 0) FROM Integracion.AgentesSync WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });
    }

    public async Task SetSincAntsaAsync(int agenteSyncId, bool activo)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET SincAntsaActivo = @Activo WHERE AgenteSyncID = @AgenteSyncId",
            new { Activo = activo, AgenteSyncId = agenteSyncId });
        if (filas == 0)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");
    }

    public async Task SetSyncPausadaAsync(int agenteSyncId, bool pausada)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET SyncPausada = @Pausada WHERE AgenteSyncID = @AgenteSyncId",
            new { Pausada = pausada, AgenteSyncId = agenteSyncId });
        if (filas == 0)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");
    }

    public async Task<int> PurgarVentasTestAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        var centroCostoId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });
        if (centroCostoId is null)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        // Marcar para que el agente limpie las tablas de dedup en Visions en el próximo ciclo.
        await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET PurgaVisionsRequerida = 1 WHERE AgenteSyncID = @AgenteSyncId",
            new { AgenteSyncId = agenteSyncId });

        // Purgar en NEXO: EventosEntrantes + resetear stock a 0.
        var result = await connection.QuerySingleAsync<int>(
            "EXEC Integracion.sp_PurgarVentasTestVisions @CentroCostoID",
            new { CentroCostoID = centroCostoId.Value });

        return result;
    }

    public async Task ResetPurgaVisionsAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET PurgaVisionsRequerida = 0 WHERE CentroCostoID = @CcId AND Activo = 1",
            new { CcId = centroCostoId });
    }

    public async Task SolicitarReprocesoAsync(int agenteSyncId, string tipo)
    {
        using var connection = _db.CreateConnection();
        var centroCostoId = await connection.ExecuteScalarAsync<int?>(
            "SELECT CentroCostoID FROM Integracion.AgentesSync WHERE AgenteSyncID = @Id",
            new { Id = agenteSyncId });
        if (centroCostoId is null)
            throw new KeyNotFoundException($"No existe el agente {agenteSyncId}.");

        var reprocesarArticulos   = tipo is "Articulos"   or "Todo";
        var reprocesarClientes    = tipo is "Clientes"    or "Todo";
        var reprocesarProveedores = tipo is "Proveedores" or "Todo";

        await connection.ExecuteAsync(@"
            UPDATE Integracion.AgentesSync
            SET ReprocesarArticulos   = @RA,
                ReprocesarClientes    = @RC,
                ReprocesarProveedores = @RP
            WHERE AgenteSyncID = @Id",
            new { RA = reprocesarArticulos, RC = reprocesarClientes, RP = reprocesarProveedores, Id = agenteSyncId });

        await connection.ExecuteAsync(@"
            MERGE Integracion.EstadoReproceso AS d
            USING (SELECT @CC AS CentroCostoID) AS s ON d.CentroCostoID = s.CentroCostoID
            WHEN MATCHED THEN UPDATE SET
                TipoReproceso = @Tipo, Estado = 'EnProceso', TotalEstimado = NULL,
                Procesados = 0, FechaInicio = GETDATE(), FechaFin = NULL, Mensaje = NULL
            WHEN NOT MATCHED THEN INSERT
                (CentroCostoID, TipoReproceso, Estado, Procesados, FechaInicio)
                VALUES (@CC, @Tipo, 'EnProceso', 0, GETDATE());",
            new { CC = centroCostoId.Value, Tipo = tipo });
    }

    public async Task<EstadoReprocesoDto?> ObtenerEstadoReprocesoAsync(int agenteSyncId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EstadoReprocesoDto>(@"
            SELECT er.TipoReproceso, er.Estado, er.TotalEstimado, er.Procesados,
                   er.FechaInicio, er.FechaFin, er.Mensaje
            FROM Integracion.EstadoReproceso er
            JOIN Integracion.AgentesSync a ON a.CentroCostoID = er.CentroCostoID
            WHERE a.AgenteSyncID = @Id",
            new { Id = agenteSyncId });
    }

    public async Task ActualizarProgresoReprocesoAsync(int centroCostoId, int procesados, int totalEstimado, string estado)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE Integracion.EstadoReproceso
            SET Procesados = @P, TotalEstimado = @T, Estado = @E,
                FechaFin = CASE WHEN @E IN ('Completado','Error') THEN GETDATE() ELSE NULL END
            WHERE CentroCostoID = @CC",
            new { P = procesados, T = totalEstimado, E = estado, CC = centroCostoId });
    }

    public async Task ResetBanderasReprocesoAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE Integracion.AgentesSync
            SET ReprocesarArticulos = 0, ReprocesarClientes = 0, ReprocesarProveedores = 0
            WHERE CentroCostoID = @CC AND Activo = 1",
            new { CC = centroCostoId });
    }

    public async Task SolicitarSyncCatalogoCompletoAsync(int centroCostoId)
    {
        using var connection = _db.CreateConnection();

        // Paso 1: crear mapeos para este CC con todos los articulos ya mapeados en otros CC Visions.
        // MIN(CodigoArticuloVisions) garantiza un unico codigo por ArticuloID aunque el mismo articulo
        // tenga codigos distintos en diferentes CC origen (evita duplicados en el INSERT).
        await connection.ExecuteAsync(@"
            INSERT INTO Integracion.MapeoArticulos
                (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado, FechaCreacion)
            SELECT ma.ArticuloID, @CcId, MIN(ma.CodigoArticuloVisions), 1, GETDATE()
            FROM Integracion.MapeoArticulos ma
            JOIN Organizacion.CentrosCosto cc_origen ON cc_origen.CentroCostoID = ma.CentroCostoID
            WHERE cc_origen.TieneVisions = 1
              AND ma.Estado = 1
              AND ma.CentroCostoID <> @CcId
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma2
                  WHERE ma2.ArticuloID = ma.ArticuloID AND ma2.CentroCostoID = @CcId AND ma2.Estado = 1)
            GROUP BY ma.ArticuloID;",
            new { CcId = centroCostoId });

        // Paso 2: generar SINCRONIZAR_ARTICULO para articulos mapeados en este CC sin evento pendiente.
        var insertados = await connection.ExecuteAsync(@"
            INSERT INTO Integracion.EventosSalientes
                (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'SINCRONIZAR_ARTICULO', ma.CentroCostoID, ma.ArticuloID, 0, ISNULL(t.CostoPromedio, 0)
            FROM Integracion.MapeoArticulos ma
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
            WHERE ma.CentroCostoID = @CcId AND ma.Estado = 1
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.EventosSalientes e
                  WHERE e.ArticuloID    = ma.ArticuloID
                    AND e.CentroCostoID = ma.CentroCostoID
                    AND e.TipoEvento    = 'SINCRONIZAR_ARTICULO'
                    AND e.Estado IN ('PENDIENTE', 'CONFIRMADO'));",
            new { CcId = centroCostoId });

        _logger.LogInformation(
            "SolicitarSyncCatalogoCompleto CC {CC}: {N} eventos SINCRONIZAR_ARTICULO generados.",
            centroCostoId, insertados);
    }

    private static async Task RegistrarPreciosInicialesAsync(
        System.Data.IDbConnection connection,
        int articuloId,
        decimal? pPublico, decimal? costo, decimal? pBodega, decimal? pCredito)
    {
        var precios = new (string Campo, decimal? Valor)[]
        {
            ("PPublico", pPublico),
            ("Costo",    costo),
            ("PBodega",  pBodega),
            ("PCredito", pCredito),
        };
        foreach (var (campo, valor) in precios)
        {
            if ((valor ?? 0m) == 0m) continue;
            await connection.ExecuteAsync(
                @"INSERT INTO Auditoria.HistorialPrecios
                    (ArticuloID, UsuarioID, NombreUsuario, Campo, ValorAnterior, ValorNuevo)
                  VALUES (@ArticuloID, 0, 'Visions', @Campo, NULL, @Valor)",
                new { ArticuloID = articuloId, Campo = campo, Valor = valor });
        }
    }
}