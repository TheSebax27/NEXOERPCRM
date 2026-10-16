-- Variantes de artículos: relación padre-hijo dentro de Catalogo.Tarjetas
-- Idempotente: se puede ejecutar varias veces sin error.
-- NO afecta Visions: las variantes son artículos normales con su propio SKU.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Catalogo.Tarjetas') AND name = 'ArticuloPadreID'
)
BEGIN
    ALTER TABLE Catalogo.Tarjetas ADD ArticuloPadreID INT NULL;
    ALTER TABLE Catalogo.Tarjetas ADD CONSTRAINT FK_Tarjetas_ArticuloPadre
        FOREIGN KEY (ArticuloPadreID) REFERENCES Catalogo.Tarjetas(ArticuloID);
    PRINT 'Columna ArticuloPadreID agregada a Catalogo.Tarjetas';
END
ELSE
    PRINT 'ArticuloPadreID ya existe - sin cambios';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Catalogo.Tarjetas') AND name = 'NombreVariante'
)
BEGIN
    ALTER TABLE Catalogo.Tarjetas ADD NombreVariante NVARCHAR(100) NULL;
    PRINT 'Columna NombreVariante agregada a Catalogo.Tarjetas';
END
ELSE
    PRINT 'NombreVariante ya existe - sin cambios';
