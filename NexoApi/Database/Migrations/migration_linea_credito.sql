-- Línea de crédito por cliente (NEXO ERP)
-- Idempotente. No toca tablas de Visions ni del pipeline de integración.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Crm.LineasCredito'))
BEGIN
    CREATE TABLE Crm.LineasCredito (
        ClienteID      INT            NOT NULL,
        CupoCredito    DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Observaciones  NVARCHAR(500)  NULL,
        FechaCreacion  DATETIME2      NOT NULL DEFAULT GETDATE(),
        FechaActualiza DATETIME2      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_LineasCredito PRIMARY KEY (ClienteID),
        CONSTRAINT FK_LineasCredito_Cliente
            FOREIGN KEY (ClienteID) REFERENCES Crm.Clientes(ClienteID)
    );
    PRINT 'Tabla Crm.LineasCredito creada.';
END
ELSE
    PRINT 'Crm.LineasCredito ya existe — sin cambios.';
