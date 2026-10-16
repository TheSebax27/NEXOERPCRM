-- Historial de cambios de precios por artículo
-- Ejecutar una vez en la BD de cada cliente (igual que las demás migraciones)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Auditoria')
    EXEC('CREATE SCHEMA Auditoria');

IF OBJECT_ID('Auditoria.HistorialPrecios', 'U') IS NULL
BEGIN
    CREATE TABLE Auditoria.HistorialPrecios (
        HistorialID     INT IDENTITY(1,1) PRIMARY KEY,
        ArticuloID      INT          NOT NULL,
        UsuarioID       INT          NOT NULL,
        NombreUsuario   NVARCHAR(100) NOT NULL,
        Campo           NVARCHAR(50)  NOT NULL,   -- 'Costo','PPublico','PBodega','PCredito'
        ValorAnterior   DECIMAL(18,2) NULL,
        ValorNuevo      DECIMAL(18,2) NULL,
        FechaCambio     DATETIME      NOT NULL
            CONSTRAINT DF_HistorialPrecios_Fecha DEFAULT GETDATE(),
        CONSTRAINT FK_HistorialPrecios_Articulo
            FOREIGN KEY (ArticuloID) REFERENCES Catalogo.Tarjetas(ArticuloID)
    );
    CREATE INDEX IX_HistorialPrecios_ArticuloFecha
        ON Auditoria.HistorialPrecios(ArticuloID, FechaCambio DESC);
    PRINT 'Tabla Auditoria.HistorialPrecios creada.';
END
ELSE
    PRINT 'Tabla Auditoria.HistorialPrecios ya existe, sin cambios.';
