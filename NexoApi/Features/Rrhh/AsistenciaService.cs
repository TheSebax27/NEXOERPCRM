using System.Security.Cryptography;
using System.Text;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Rrhh.Dtos;

namespace NexoApi.Features.Rrhh;

public interface IAsistenciaService
{
    Task<TokenQrResponse> ObtenerTokenActualAsync();
    Task<bool> ValidarTokenAsync(string token);
    Task<ConfigQrResponse> ObtenerModoQrAsync();
    Task ActualizarModoQrAsync(string modo);

    Task<IEnumerable<HorarioItem>> ListarHorariosAsync();
    Task<int> CrearHorarioAsync(CrearHorarioRequest request);
    Task AsignarHorarioEmpleadoAsync(int empleadoId, AsignarHorarioRequest request);

    Task<EstadoAsistenciaHoy> ObtenerEstadoHoyAsync(int usuarioId);
    Task MarcarQrAsync(int usuarioId, MarcarQrRequest request);
    Task MarcarManualAsync(int registradorUsuarioId, MarcarManualRequest request);

    Task<IEnumerable<RegistroAsistenciaItem>> ListarAsync(int? empleadoId, DateOnly? desde, DateOnly? hasta);

    Task ActualizarHorarioAsync(int horarioId, CrearHorarioRequest r);
    Task EliminarHorarioAsync(int horarioId);
    Task ToggleActivoHorarioAsync(int horarioId);
    Task<IEnumerable<EmpleadoSimpleItem>> ListarEmpleadosSinHorarioAsync();
    Task<IEnumerable<EmpleadoSimpleItem>> ObtenerEmpleadosAsignadosAsync(int horarioId);
    Task<int?> ObtenerEmpleadoIdAsync(int usuarioId);
}

public class AsistenciaService(IDbConnectionFactory db) : IAsistenciaService
{
    // ---- Config QR ----

    private record QrConfig(string Secreto, string ModoQr, string? TokenActual);

    private async Task<QrConfig> ObtenerConfigAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<QrConfig>(
            "SELECT TOP 1 Secreto, ModoQr, TokenActual FROM Rrhh.QrAsistenciaConfig")
            ?? new QrConfig("nexo-fallback", "TTL", null);
    }

    private static string Computar(string secreto, long ventana)
    {
        var input = Encoding.UTF8.GetBytes($"{secreto}:{ventana}");
        return Convert.ToHexString(SHA256.HashData(input))[..16].ToLower();
    }

    private static string GenerarTokenAleatorio()
        => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLower();

    public async Task<TokenQrResponse> ObtenerTokenActualAsync()
    {
        var cfg = await ObtenerConfigAsync();

        if (cfg.ModoQr == "SINGLE_USE")
        {
            var token = cfg.TokenActual;
            if (string.IsNullOrEmpty(token))
            {
                // Solo actualiza si sigue NULL — primera escritura gana, las demás leen lo que quedó
                var candidato = GenerarTokenAleatorio();
                using var conn = db.CreateConnection();
                await conn.ExecuteAsync(
                    "UPDATE Rrhh.QrAsistenciaConfig SET TokenActual = @candidato WHERE TokenActual IS NULL",
                    new { candidato });
                token = await conn.ExecuteScalarAsync<string>(
                    "SELECT TokenActual FROM Rrhh.QrAsistenciaConfig") ?? candidato;
            }
            return new TokenQrResponse(token, 0, "SINGLE_USE");
        }

        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tokenTtl = Computar(cfg.Secreto, ahora / 300);
        return new TokenQrResponse(tokenTtl, (int)(300 - ahora % 300), "TTL");
    }

    public async Task<bool> ValidarTokenAsync(string token)
    {
        var cfg = await ObtenerConfigAsync();

        if (cfg.ModoQr == "SINGLE_USE")
            return cfg.TokenActual == token;

        var ventana = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 300;
        return Computar(cfg.Secreto, ventana) == token || Computar(cfg.Secreto, ventana - 1) == token;
    }

    public async Task<ConfigQrResponse> ObtenerModoQrAsync()
    {
        var cfg = await ObtenerConfigAsync();
        return new ConfigQrResponse(cfg.ModoQr);
    }

    public async Task ActualizarModoQrAsync(string modo)
    {
        if (modo != "TTL" && modo != "SINGLE_USE")
            throw new ArgumentException("Modo invalido. Usa TTL o SINGLE_USE.");

        using var conn = db.CreateConnection();
        await conn.ExecuteAsync("""
            IF EXISTS (SELECT 1 FROM Rrhh.QrAsistenciaConfig)
                UPDATE Rrhh.QrAsistenciaConfig SET ModoQr = @modo, TokenActual = NULL
            ELSE
                INSERT INTO Rrhh.QrAsistenciaConfig (Secreto, ModoQr, TokenActual)
                VALUES (CONVERT(varchar(64), NEWID()), @modo, NULL)
            """, new { modo });
    }

    // ---- Horarios ----

    private record HorarioRow(int HorarioID, string Nombre, int ToleranciaTardanzaMin, string TipoCiclo, bool Activo, bool RegistraSalida);
    private record HorarioDiaRow(int HorarioID, byte DiaSemana, string? Semana, TimeSpan HoraEntrada, TimeSpan HoraSalida, bool TieneAlmuerzo, TimeSpan? HoraInicioAlmuerzo, TimeSpan? HoraFinAlmuerzo);

    public async Task<IEnumerable<HorarioItem>> ListarHorariosAsync()
    {
        using var conn = db.CreateConnection();
        var horarios = (await conn.QueryAsync<HorarioRow>(
            "SELECT HorarioID, Nombre, ToleranciaTardanzaMin, TipoCiclo, Activo, RegistraSalida FROM Rrhh.Horarios ORDER BY Nombre"
        )).ToList();

        if (horarios.Count == 0) return [];

        var ids = horarios.Select(h => h.HorarioID).ToList();
        var diasRaw = await conn.QueryAsync<HorarioDiaRow>("""
            SELECT HorarioID, DiaSemana, Semana, HoraEntrada, HoraSalida,
                   TieneAlmuerzo, HoraInicioAlmuerzo, HoraFinAlmuerzo
            FROM Rrhh.HorarioDias
            WHERE HorarioID IN @ids
            ORDER BY HorarioID, Semana, DiaSemana
            """, new { ids });

        var diasPorHorario = diasRaw.GroupBy(d => d.HorarioID)
            .ToDictionary(g => g.Key, g => g.Select(d => new HorarioDiaItem(
                d.DiaSemana, d.Semana,
                TimeOnly.FromTimeSpan(d.HoraEntrada), TimeOnly.FromTimeSpan(d.HoraSalida),
                d.TieneAlmuerzo,
                d.HoraInicioAlmuerzo.HasValue ? TimeOnly.FromTimeSpan(d.HoraInicioAlmuerzo.Value) : null,
                d.HoraFinAlmuerzo.HasValue    ? TimeOnly.FromTimeSpan(d.HoraFinAlmuerzo.Value)    : null
            )).ToList());

        return horarios.Select(h => new HorarioItem(
            h.HorarioID, h.Nombre, h.ToleranciaTardanzaMin, h.TipoCiclo, h.Activo, h.RegistraSalida,
            diasPorHorario.TryGetValue(h.HorarioID, out var ds) ? ds : []
        ));
    }

    public async Task<int> CrearHorarioAsync(CrearHorarioRequest r)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var horarioId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Rrhh.Horarios (Nombre, ToleranciaTardanzaMin, TipoCiclo, RegistraSalida, Activo)
            OUTPUT INSERTED.HorarioID
            VALUES (@Nombre, @ToleranciaTardanzaMin, @TipoCiclo, @RegistraSalida, 1)
            """, new { r.Nombre, r.ToleranciaTardanzaMin, r.TipoCiclo, r.RegistraSalida }, transaction: tx);

        foreach (var dia in r.Dias)
        {
            await conn.ExecuteAsync("""
                INSERT INTO Rrhh.HorarioDias
                    (HorarioID, DiaSemana, Semana, HoraEntrada, HoraSalida,
                     TieneAlmuerzo, HoraInicioAlmuerzo, HoraFinAlmuerzo)
                VALUES (@horarioId, @DiaSemana, @Semana, @HoraEntrada, @HoraSalida,
                        @TieneAlmuerzo, @HoraInicioAlmuerzo, @HoraFinAlmuerzo)
                """, new { horarioId, dia.DiaSemana, dia.Semana,
                    dia.HoraEntrada, dia.HoraSalida, dia.TieneAlmuerzo,
                    dia.HoraInicioAlmuerzo, dia.HoraFinAlmuerzo }, transaction: tx);
        }

        tx.Commit();
        return horarioId;
    }

    public async Task AsignarHorarioEmpleadoAsync(int empleadoId, AsignarHorarioRequest r)
    {
        using var conn = db.CreateConnection();
        var desdeDate = r.Desde.Date;

        var tieneHorario = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Rrhh.EmpleadoHorario WHERE EmpleadoID=@EmpleadoID AND Hasta IS NULL",
            new { EmpleadoID = empleadoId }) > 0;

        if (tieneHorario)
            throw new InvalidOperationException("Este empleado ya tiene un horario activo asignado. Para cambiarlo, primero desactiva el actual.");

        await conn.ExecuteAsync("""
            INSERT INTO Rrhh.EmpleadoHorario (EmpleadoID,HorarioID,Desde)
            VALUES (@EmpleadoID,@HorarioID,@Desde)
            """, new { EmpleadoID = empleadoId, r.HorarioID, Desde = desdeDate });
    }

    // ---- Estado y marcado ----

    private async Task<int?> EmpleadoDeUsuarioAsync(int usuarioId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT EmpleadoID FROM Seguridad.Usuarios WHERE UsuarioID=@usuarioId", new { usuarioId });
    }

    public async Task<EstadoAsistenciaHoy> ObtenerEstadoHoyAsync(int usuarioId)
    {
        var empleadoId = await EmpleadoDeUsuarioAsync(usuarioId)
            ?? throw new InvalidOperationException("Tu usuario no esta vinculado a ningun empleado. Contacta al administrador.");

        using var conn = db.CreateConnection();
        var fila = await conn.QueryFirstOrDefaultAsync<FilaEstado>("""
            SELECT e.EmpleadoID, e.Nombres+' '+e.Apellidos AS Empleado,
                   r.HoraEntrada, r.MetodoEntrada, r.HoraSalida, r.MetodoSalida,
                   r.HoraEntrada2, r.MetodoEntrada2, r.HoraSalida2, r.MetodoSalida2,
                   ISNULL(h.RegistraSalida, 1) AS RegistraSalida
            FROM Rrhh.Empleados e
            LEFT JOIN Rrhh.RegistroAsistencia r
                ON r.EmpleadoID=e.EmpleadoID AND r.Fecha=CAST(GETDATE() AS DATE)
            LEFT JOIN Rrhh.EmpleadoHorario eh
                ON eh.EmpleadoID=e.EmpleadoID
                AND eh.Desde <= CAST(GETDATE() AS DATE)
                AND (eh.Hasta IS NULL OR eh.Hasta >= CAST(GETDATE() AS DATE))
            LEFT JOIN Rrhh.Horarios h ON h.HorarioID=eh.HorarioID AND h.Activo=1
            WHERE e.EmpleadoID=@empleadoId
            """, new { empleadoId }) ?? throw new InvalidOperationException("Empleado no encontrado.");

        var hoy = DateOnly.FromDateTime(NexoApi.Common.ColombiaClock.Today);
        var tieneAlmuerzo = await TieneAlmuerzoDiaAsync(conn, empleadoId, hoy);

        return new EstadoAsistenciaHoy(
            fila.EmpleadoID, fila.Empleado,
            fila.HoraEntrada is not null, fila.HoraEntrada, fila.MetodoEntrada,
            fila.HoraSalida is not null, fila.HoraSalida, fila.MetodoSalida,
            fila.RegistraSalida, tieneAlmuerzo,
            fila.HoraEntrada2 is not null, fila.HoraEntrada2, fila.MetodoEntrada2,
            fila.HoraSalida2 is not null, fila.HoraSalida2, fila.MetodoSalida2);
    }

    public async Task MarcarQrAsync(int usuarioId, MarcarQrRequest request)
    {
        var cfg = await ObtenerConfigAsync();

        if (cfg.ModoQr == "SINGLE_USE")
        {
            // Validación y rotación atómica: solo 1 request concurrente puede hacer el UPDATE
            var nuevoToken = GenerarTokenAleatorio();
            using var connRotar = db.CreateConnection();
            var rows = await connRotar.ExecuteAsync(
                "UPDATE Rrhh.QrAsistenciaConfig SET TokenActual = @nuevoToken WHERE TokenActual = @token",
                new { nuevoToken, token = request.Token });
            if (rows == 0)
                throw new InvalidOperationException("El codigo QR no es valido o ya fue usado. Escanea el nuevo codigo.");
        }
        else
        {
            if (!await ValidarTokenAsync(request.Token))
                throw new InvalidOperationException("El codigo QR no es valido o ya expiro. Escanea el codigo nuevamente.");
        }

        var empleadoId = await EmpleadoDeUsuarioAsync(usuarioId)
            ?? throw new InvalidOperationException("Tu usuario no esta vinculado a ningun empleado.");

        await MarcarInternoAsync(empleadoId, request.Tipo, NexoApi.Common.ColombiaClock.Now, "QR", adminId: null, nota: null);
    }

    public async Task MarcarManualAsync(int registradorId, MarcarManualRequest request)
        => await MarcarInternoAsync(request.EmpleadoID, request.Tipo, request.Hora, "MANUAL", registradorId, request.Nota);

    private async Task MarcarInternoAsync(int empleadoId, string tipo, DateTime hora, string metodo, int? adminId, string? nota)
    {
        using var conn = db.CreateConnection();
        var fecha = hora.Date;
        var fechaDate = DateOnly.FromDateTime(hora);

        var estado = await conn.QueryFirstOrDefaultAsync<(DateTime? HoraEntrada, DateTime? HoraSalida, DateTime? HoraEntrada2, DateTime? HoraSalida2)>(
            "SELECT HoraEntrada, HoraSalida, HoraEntrada2, HoraSalida2 FROM Rrhh.RegistroAsistencia WHERE EmpleadoID=@empleadoId AND Fecha=@fecha",
            new { empleadoId, fecha });

        if (tipo == "ENTRADA")
        {
            if (estado.HoraEntrada is null)
            {
                // 1ra entrada
                var tardanza = await CalcularTardanzaAsync(conn, empleadoId, fechaDate, TimeOnly.FromDateTime(hora));
                await conn.ExecuteAsync("""
                    MERGE Rrhh.RegistroAsistencia AS t
                    USING (SELECT @empleadoId AS EmpleadoID, @fecha AS Fecha) AS s
                    ON t.EmpleadoID=s.EmpleadoID AND t.Fecha=s.Fecha
                    WHEN MATCHED THEN
                        UPDATE SET HoraEntrada=@hora, MetodoEntrada=@metodo,
                                   EntradaRegistradaPor=@adminId, EntradaNota=@nota, MinutosTardanza=@tardanza
                    WHEN NOT MATCHED THEN
                        INSERT (EmpleadoID,Fecha,HoraEntrada,MetodoEntrada,EntradaRegistradaPor,EntradaNota,MinutosTardanza)
                        VALUES (@empleadoId,@fecha,@hora,@metodo,@adminId,@nota,@tardanza);
                    """, new { empleadoId, fecha, hora, metodo, adminId, nota, tardanza });
            }
            else if (estado.HoraEntrada2 is null)
            {
                // 2da entrada (regreso de almuerzo): solo si el horario tiene almuerzo.
                // Se permite aunque no haya salida 1 registrada (operador que solo marca entradas).
                var tieneAlmuerzo = await TieneAlmuerzoDiaAsync(conn, empleadoId, fechaDate);
                if (!tieneAlmuerzo)
                    throw new InvalidOperationException("Ya existe una entrada registrada para ese empleado en esa fecha.");

                await conn.ExecuteAsync("""
                    UPDATE Rrhh.RegistroAsistencia
                    SET HoraEntrada2=@hora, MetodoEntrada2=@metodo,
                        Entrada2RegistradaPor=@adminId, Entrada2Nota=@nota
                    WHERE EmpleadoID=@empleadoId AND Fecha=@fecha
                    """, new { empleadoId, fecha, hora, metodo, adminId, nota });
            }
            else
            {
                throw new InvalidOperationException("Ya existe una entrada registrada para ese empleado en esa fecha.");
            }
        }
        else // SALIDA
        {
            if (estado.HoraEntrada is not null && estado.HoraSalida is null)
            {
                if (hora <= estado.HoraEntrada)
                    throw new InvalidOperationException("La hora de salida no puede ser anterior o igual a la hora de entrada.");
                // 1ra salida
                await conn.ExecuteAsync("""
                    UPDATE Rrhh.RegistroAsistencia
                    SET HoraSalida=@hora, MetodoSalida=@metodo,
                        SalidaRegistradaPor=@adminId, SalidaNota=@nota
                    WHERE EmpleadoID=@empleadoId AND Fecha=@fecha
                    """, new { empleadoId, fecha, hora, metodo, adminId, nota });
            }
            else if (estado.HoraEntrada2 is not null && estado.HoraSalida2 is null)
            {
                if (hora <= estado.HoraEntrada2)
                    throw new InvalidOperationException("La hora de salida no puede ser anterior o igual a la hora de la segunda entrada.");
                // 2da salida (salida final tras almuerzo)
                await conn.ExecuteAsync("""
                    UPDATE Rrhh.RegistroAsistencia
                    SET HoraSalida2=@hora, MetodoSalida2=@metodo,
                        Salida2RegistradaPor=@adminId, Salida2Nota=@nota
                    WHERE EmpleadoID=@empleadoId AND Fecha=@fecha
                    """, new { empleadoId, fecha, hora, metodo, adminId, nota });
            }
            else
            {
                throw new InvalidOperationException("No hay entrada registrada para esa fecha o ya existe una salida.");
            }
        }
    }

    private static string ComputarSemana(int isoWeek, string? tipoCiclo) => tipoCiclo switch {
        "SEMANA_AB" => isoWeek % 2 == 1 ? "A" : "B",
        "SEMANA_4"  => (isoWeek % 4) switch { 0 => "A", 1 => "B", 2 => "C", _ => "D" },
        _           => "A"  // FIJO: días tienen Semana=NULL en BD; valor no afecta el match IS NULL
    };

    private static async Task<bool> TieneAlmuerzoDiaAsync(System.Data.IDbConnection conn, int empleadoId, DateOnly fecha)
    {
        var dow = fecha.DayOfWeek;
        var diaSemana = dow == DayOfWeek.Sunday ? 7 : (int)dow;
        var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(fecha.ToDateTime(TimeOnly.MinValue));

        var rows = (await conn.QueryAsync<DiaHorarioRow>("""
            SELECT hd.TieneAlmuerzo, hd.Semana, h.TipoCiclo
            FROM Rrhh.EmpleadoHorario eh
            JOIN Rrhh.Horarios h ON h.HorarioID=eh.HorarioID
            JOIN Rrhh.HorarioDias hd ON hd.HorarioID=h.HorarioID
                AND hd.DiaSemana=@diaSemana
            WHERE eh.EmpleadoID=@empleadoId
              AND eh.Desde <= @fecha AND (eh.Hasta IS NULL OR eh.Hasta >= @fecha)
              AND h.Activo=1
            """, new { empleadoId, fecha = fecha.ToDateTime(TimeOnly.MinValue), diaSemana })).ToList();

        if (rows.Count == 0) return false;
        var semana = ComputarSemana(isoWeek, rows[0].TipoCiclo);
        return rows.FirstOrDefault(r => r.Semana == null || r.Semana == semana)?.TieneAlmuerzo ?? false;
    }

    private static async Task<int?> CalcularTardanzaAsync(
        System.Data.IDbConnection conn, int empleadoId, DateOnly fecha, TimeOnly horaReal)
    {
        var dow = fecha.DayOfWeek;
        var diaSemana = dow == DayOfWeek.Sunday ? 7 : (int)dow;
        var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(fecha.ToDateTime(TimeOnly.MinValue));

        var rows = (await conn.QueryAsync<DiaScheduleRow>("""
            SELECT hd.HoraEntrada, h.ToleranciaTardanzaMin, hd.Semana, h.TipoCiclo
            FROM Rrhh.EmpleadoHorario eh
            JOIN Rrhh.Horarios h ON h.HorarioID=eh.HorarioID
            JOIN Rrhh.HorarioDias hd ON hd.HorarioID=h.HorarioID
                AND hd.DiaSemana=@diaSemana
            WHERE eh.EmpleadoID=@empleadoId
              AND eh.Desde <= @fecha AND (eh.Hasta IS NULL OR eh.Hasta >= @fecha)
              AND h.Activo=1
            """, new { empleadoId, fecha = fecha.ToDateTime(TimeOnly.MinValue), diaSemana })).ToList();

        if (rows.Count == 0) return null;
        var semana = ComputarSemana(isoWeek, rows[0].TipoCiclo);
        var horario = rows.FirstOrDefault(r => r.Semana == null || r.Semana == semana);
        if (horario is null) return null;

        var entradaHorario = TimeOnly.FromTimeSpan(horario.HoraEntrada);
        var limite = entradaHorario.AddMinutes(horario.ToleranciaTardanzaMin);
        if (horaReal <= limite) return 0;
        return (int)(horaReal - entradaHorario).TotalMinutes;
    }

    public async Task<IEnumerable<RegistroAsistenciaItem>> ListarAsync(int? empleadoId, DateOnly? desde, DateOnly? hasta)
    {
        using var conn = db.CreateConnection();
        DateTime? desdeDate = desde.HasValue ? desde.Value.ToDateTime(TimeOnly.MinValue) : null;
        DateTime? hastaDate = hasta.HasValue  ? hasta.Value.ToDateTime(new TimeOnly(23, 59, 59)) : null;

        return await conn.QueryAsync<RegistroAsistenciaItem>("""
            SELECT r.RegistroID, r.EmpleadoID,
                   e.Nombres+' '+e.Apellidos AS Empleado,
                   r.Fecha, r.HoraEntrada, r.MetodoEntrada,
                   COALESCE(ur.Nombres+' '+ur.Apellidos, NULL) AS EntradaRegistradaPor,
                   r.EntradaNota, r.HoraSalida, r.MetodoSalida,
                   COALESCE(us.Nombres+' '+us.Apellidos, NULL) AS SalidaRegistradaPor,
                   r.SalidaNota, r.MinutosTardanza,
                   CASE
                     WHEN r.HoraEntrada IS NOT NULL AND r.HoraSalida2 IS NOT NULL
                          THEN CAST(DATEDIFF(MINUTE,r.HoraEntrada,r.HoraSalida) + DATEDIFF(MINUTE,r.HoraEntrada2,r.HoraSalida2) AS FLOAT)/60.0
                     WHEN r.HoraEntrada IS NOT NULL AND r.HoraSalida IS NOT NULL
                          THEN CAST(DATEDIFF(MINUTE,r.HoraEntrada,r.HoraSalida) AS FLOAT)/60.0
                     ELSE NULL
                   END AS HorasEfectivas,
                   r.HoraEntrada2, r.HoraSalida2
            FROM Rrhh.RegistroAsistencia r
            JOIN Rrhh.Empleados e ON e.EmpleadoID=r.EmpleadoID
            LEFT JOIN Seguridad.Usuarios ur ON ur.UsuarioID=r.EntradaRegistradaPor
            LEFT JOIN Seguridad.Usuarios us ON us.UsuarioID=r.SalidaRegistradaPor
            WHERE (@empleadoId IS NULL OR r.EmpleadoID=@empleadoId)
              AND (@desdeDate IS NULL OR r.Fecha>=@desdeDate)
              AND (@hastaDate IS NULL OR r.Fecha<=@hastaDate)
            ORDER BY r.Fecha DESC, e.Apellidos
            """, new { empleadoId, desdeDate, hastaDate });
    }

    public async Task ActualizarHorarioAsync(int horarioId, CrearHorarioRequest r)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var rows = await conn.ExecuteAsync(
            "UPDATE Rrhh.Horarios SET Nombre=@Nombre, ToleranciaTardanzaMin=@ToleranciaTardanzaMin, TipoCiclo=@TipoCiclo, RegistraSalida=@RegistraSalida WHERE HorarioID=@horarioId",
            new { r.Nombre, r.ToleranciaTardanzaMin, r.TipoCiclo, r.RegistraSalida, horarioId }, transaction: tx);
        if (rows == 0)
        {
            tx.Rollback();
            throw new KeyNotFoundException($"Horario {horarioId} no encontrado.");
        }

        await conn.ExecuteAsync("DELETE FROM Rrhh.HorarioDias WHERE HorarioID=@horarioId",
            new { horarioId }, transaction: tx);

        foreach (var dia in r.Dias)
        {
            await conn.ExecuteAsync("""
                INSERT INTO Rrhh.HorarioDias
                    (HorarioID, DiaSemana, Semana, HoraEntrada, HoraSalida,
                     TieneAlmuerzo, HoraInicioAlmuerzo, HoraFinAlmuerzo)
                VALUES (@horarioId, @DiaSemana, @Semana, @HoraEntrada, @HoraSalida,
                        @TieneAlmuerzo, @HoraInicioAlmuerzo, @HoraFinAlmuerzo)
                """, new { horarioId, dia.DiaSemana, dia.Semana,
                    dia.HoraEntrada, dia.HoraSalida, dia.TieneAlmuerzo,
                    dia.HoraInicioAlmuerzo, dia.HoraFinAlmuerzo }, transaction: tx);
        }

        tx.Commit();
    }

    public async Task EliminarHorarioAsync(int horarioId)
    {
        using var conn = db.CreateConnection();
        var tieneAsignados = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Rrhh.EmpleadoHorario WHERE HorarioID=@horarioId AND Hasta IS NULL",
            new { horarioId });
        if (tieneAsignados > 0)
            throw new InvalidOperationException("No se puede eliminar el horario porque tiene empleados asignados actualmente.");

        await conn.ExecuteAsync(
            "DELETE FROM Rrhh.HorarioDias WHERE HorarioID=@horarioId; DELETE FROM Rrhh.Horarios WHERE HorarioID=@horarioId",
            new { horarioId });
    }

    public async Task ToggleActivoHorarioAsync(int horarioId)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Rrhh.Horarios SET Activo = CASE WHEN Activo=1 THEN 0 ELSE 1 END WHERE HorarioID=@horarioId",
            new { horarioId });
    }

    // Clase (no record positional) para compatibilidad con Dapper en .NET 10
    public async Task<IEnumerable<EmpleadoSimpleItem>> ListarEmpleadosSinHorarioAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<EmpleadoSimpleItem>("""
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos
            FROM Rrhh.Empleados e
            WHERE e.Estado = 1
              AND NOT EXISTS (
                SELECT 1 FROM Rrhh.EmpleadoHorario eh
                WHERE eh.EmpleadoID = e.EmpleadoID AND eh.Hasta IS NULL
              )
            ORDER BY e.Apellidos, e.Nombres
            """);
    }

    public async Task<IEnumerable<EmpleadoSimpleItem>> ObtenerEmpleadosAsignadosAsync(int horarioId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<EmpleadoSimpleItem>("""
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos
            FROM Rrhh.EmpleadoHorario eh
            JOIN Rrhh.Empleados e ON e.EmpleadoID = eh.EmpleadoID
            WHERE eh.HorarioID = @horarioId AND eh.Hasta IS NULL
            ORDER BY e.Apellidos, e.Nombres
            """, new { horarioId });
    }

    private class FilaEstado
    {
        public int EmpleadoID { get; set; }
        public string Empleado { get; set; } = "";
        public DateTime? HoraEntrada { get; set; }
        public string? MetodoEntrada { get; set; }
        public DateTime? HoraSalida { get; set; }
        public string? MetodoSalida { get; set; }
        public DateTime? HoraEntrada2 { get; set; }
        public string? MetodoEntrada2 { get; set; }
        public DateTime? HoraSalida2 { get; set; }
        public string? MetodoSalida2 { get; set; }
        public bool RegistraSalida { get; set; }
    }

    private record DiaHorarioRow(bool TieneAlmuerzo, string? Semana, string TipoCiclo);
    private record DiaScheduleRow(TimeSpan HoraEntrada, int ToleranciaTardanzaMin, string? Semana, string TipoCiclo);

    public async Task<int?> ObtenerEmpleadoIdAsync(int usuarioId) => await EmpleadoDeUsuarioAsync(usuarioId);
}
