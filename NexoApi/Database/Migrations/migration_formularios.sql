-- Migration: Schema Formularios (formularios públicos con token)
-- Ejecutar en cada BD de cliente si no existe el schema

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Formularios')
BEGIN
    EXEC('CREATE SCHEMA [Formularios]')
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Formularios.Formularios'))
BEGIN
    CREATE TABLE [Formularios].[Formularios](
        [FormularioID]       [int] IDENTITY(1,1) NOT NULL,
        [Titulo]             [nvarchar](300)     NOT NULL,
        [Descripcion]        [nvarchar](1000)    NULL,
        [Token]              [nvarchar](64)      NOT NULL,
        [Activo]             [bit]               NOT NULL CONSTRAINT DF_Form_Activo            DEFAULT (1),
        [AceptaRespuestas]   [bit]               NOT NULL CONSTRAINT DF_Form_AceptaRespuestas  DEFAULT (1),
        [MensajeExito]       [nvarchar](500)     NULL,
        [CreadoPor]          [int]               NOT NULL,
        [FechaCreacion]      [datetime2](7)      NOT NULL CONSTRAINT DF_Form_FechaCreacion     DEFAULT (GETUTCDATE()),
        [FechaActualizacion] [datetime2](7)      NOT NULL CONSTRAINT DF_Form_FechaActualizacion DEFAULT (GETUTCDATE()),
        CONSTRAINT PK_Formularios PRIMARY KEY CLUSTERED ([FormularioID] ASC),
        CONSTRAINT UQ_Formularios_Token UNIQUE NONCLUSTERED ([Token] ASC)
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Formularios.Campos'))
BEGIN
    CREATE TABLE [Formularios].[Campos](
        [CampoID]       [int] IDENTITY(1,1) NOT NULL,
        [FormularioID]  [int]               NOT NULL,
        [Tipo]          [nvarchar](30)      NOT NULL,
        [Etiqueta]      [nvarchar](300)     NOT NULL,
        [Placeholder]   [nvarchar](300)     NULL,
        [Requerido]     [bit]               NOT NULL CONSTRAINT DF_Campos_Requerido DEFAULT (0),
        [Orden]         [int]               NOT NULL CONSTRAINT DF_Campos_Orden     DEFAULT (0),
        [Opciones]      [nvarchar](max)     NULL,
        CONSTRAINT PK_Campos PRIMARY KEY CLUSTERED ([CampoID] ASC),
        CONSTRAINT FK_Campos_Formularios FOREIGN KEY ([FormularioID])
            REFERENCES [Formularios].[Formularios] ([FormularioID]) ON DELETE CASCADE
    )
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Formularios.Respuestas'))
BEGIN
    CREATE TABLE [Formularios].[Respuestas](
        [RespuestaID]   [int] IDENTITY(1,1) NOT NULL,
        [FormularioID]  [int]               NOT NULL,
        [Datos]         [nvarchar](max)     NOT NULL,
        [IPOrigen]      [nvarchar](45)      NULL,
        [FechaRespuesta][datetime2](7)      NOT NULL CONSTRAINT DF_Respuestas_FechaRespuesta DEFAULT (GETUTCDATE()),
        CONSTRAINT PK_Respuestas PRIMARY KEY CLUSTERED ([RespuestaID] ASC),
        CONSTRAINT FK_Respuestas_Formularios FOREIGN KEY ([FormularioID])
            REFERENCES [Formularios].[Formularios] ([FormularioID]) ON DELETE CASCADE
    )

    CREATE NONCLUSTERED INDEX IX_Respuestas_FormularioID
        ON [Formularios].[Respuestas] ([FormularioID] ASC)
END
GO
