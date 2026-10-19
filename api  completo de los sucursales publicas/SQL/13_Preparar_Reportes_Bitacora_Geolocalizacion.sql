/*
   Módulo 10: reportes, bitácora y geolocalización enriquecida.
   Ejecutar conectado directamente a la base de datos BioRed.
   El script es idempotente: puede ejecutarse nuevamente sin duplicar objetos.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.Ubicaciones_Repartidor', 'precision_metros') IS NULL
        ALTER TABLE dbo.Ubicaciones_Repartidor
            ADD precision_metros DECIMAL(8, 2) NULL;

    IF COL_LENGTH('dbo.Ubicaciones_Repartidor', 'velocidad_kmh') IS NULL
        ALTER TABLE dbo.Ubicaciones_Repartidor
            ADD velocidad_kmh DECIMAL(6, 2) NULL;

    IF COL_LENGTH('dbo.Ubicaciones_Repartidor', 'rumbo_grados') IS NULL
        ALTER TABLE dbo.Ubicaciones_Repartidor
            ADD rumbo_grados DECIMAL(6, 2) NULL;

    IF COL_LENGTH('dbo.Ubicaciones_Repartidor', 'fecha_dispositivo_utc') IS NULL
        ALTER TABLE dbo.Ubicaciones_Repartidor
            ADD fecha_dispositivo_utc DATETIME2 NULL;

    IF COL_LENGTH('dbo.Ubicaciones_Repartidor', 'fecha_recepcion_utc') IS NULL
        ALTER TABLE dbo.Ubicaciones_Repartidor
            ADD fecha_recepcion_utc DATETIME2 NULL;

    /*
       Se usa SQL dinámico porque SQL Server compila el lote completo antes
       de reconocer las columnas agregadas en las instrucciones anteriores.
    */
    EXEC sys.sp_executesql N'
        UPDATE dbo.Ubicaciones_Repartidor
        SET fecha_recepcion_utc = CONVERT(DATETIME2, fecha_registro)
        WHERE fecha_recepcion_utc IS NULL;

        ALTER TABLE dbo.Ubicaciones_Repartidor
            ALTER COLUMN fecha_recepcion_utc DATETIME2 NOT NULL;';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Ubicaciones_Repartidor')
          AND c.name = N'fecha_recepcion_utc'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Ubicaciones_Repartidor
                ADD CONSTRAINT DF_UbicacionesRepartidor_FechaRecepcionUtc
                DEFAULT SYSUTCDATETIME() FOR fecha_recepcion_utc;';

    IF OBJECT_ID(N'dbo.CK_UbicacionesRepartidor_Precision', N'C') IS NULL
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Ubicaciones_Repartidor WITH CHECK
                ADD CONSTRAINT CK_UbicacionesRepartidor_Precision
                CHECK (precision_metros IS NULL OR precision_metros BETWEEN 0 AND 10000);';

    IF OBJECT_ID(N'dbo.CK_UbicacionesRepartidor_Velocidad', N'C') IS NULL
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Ubicaciones_Repartidor WITH CHECK
                ADD CONSTRAINT CK_UbicacionesRepartidor_Velocidad
                CHECK (velocidad_kmh IS NULL OR velocidad_kmh BETWEEN 0 AND 400);';

    IF OBJECT_ID(N'dbo.CK_UbicacionesRepartidor_Rumbo', N'C') IS NULL
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.Ubicaciones_Repartidor WITH CHECK
                ADD CONSTRAINT CK_UbicacionesRepartidor_Rumbo
                CHECK (rumbo_grados IS NULL OR rumbo_grados BETWEEN 0 AND 359.99);';

    IF OBJECT_ID(N'dbo.BitacoraApi', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.BitacoraApi
        (
            id_bitacora     BIGINT IDENTITY(1, 1) NOT NULL,
            id_cuenta       INT NULL,
            tipo_usuario    VARCHAR(20) NULL,
            referencia_id   VARCHAR(50) NULL,
            modulo          VARCHAR(50) NOT NULL,
            metodo_http     VARCHAR(10) NOT NULL,
            ruta            VARCHAR(300) NOT NULL,
            codigo_estado   INT NOT NULL,
            duracion_ms     BIGINT NOT NULL,
            direccion_ip    VARCHAR(45) NULL,
            agente_usuario  VARCHAR(500) NULL,
            correlation_id  VARCHAR(100) NOT NULL,
            fecha_utc       DATETIME2 NOT NULL
                CONSTRAINT DF_BitacoraApi_FechaUtc DEFAULT SYSUTCDATETIME(),

            CONSTRAINT PK_BitacoraApi PRIMARY KEY (id_bitacora),
            CONSTRAINT CK_BitacoraApi_CodigoEstado
                CHECK (codigo_estado BETWEEN 100 AND 599),
            CONSTRAINT CK_BitacoraApi_Duracion
                CHECK (duracion_ms >= 0)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE cod_permiso = 'REPORTE_VER')
        INSERT INTO dbo.Permisos
            (cod_permiso, nombre_permiso, descripcion, modulo)
        VALUES
            ('REPORTE_VER', 'Ver reportes', 'Permite consultar reportes administrativos.', 'Reportes');

    IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE cod_permiso = 'BITACORA_VER')
        INSERT INTO dbo.Permisos
            (cod_permiso, nombre_permiso, descripcion, modulo)
        VALUES
            ('BITACORA_VER', 'Ver bitácora', 'Permite consultar la bitácora de la API.', 'Reportes');

    INSERT INTO dbo.Rol_Permisos (cod_rol, cod_permiso)
    SELECT 'ADMIN', permiso.cod_permiso
    FROM dbo.Permisos AS permiso
    WHERE permiso.cod_permiso IN ('REPORTE_VER', 'BITACORA_VER', 'UBICACION_VER')
      AND EXISTS (SELECT 1 FROM dbo.Rol WHERE cod_rol = 'ADMIN')
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.Rol_Permisos AS asignacion
          WHERE asignacion.cod_rol = 'ADMIN'
            AND asignacion.cod_permiso = permiso.cod_permiso
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ubicaciones_Repartidor')
      AND name = N'IX_UbicacionesRepartidor_Recepcion'
)
    CREATE INDEX IX_UbicacionesRepartidor_Recepcion
        ON dbo.Ubicaciones_Repartidor(cod_repartidor, fecha_recepcion_utc DESC)
        INCLUDE (id_pedido, latitud, longitud, precision_metros, velocidad_kmh, rumbo_grados);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BitacoraApi')
      AND name = N'IX_BitacoraApi_FechaUtc'
)
    CREATE INDEX IX_BitacoraApi_FechaUtc
        ON dbo.BitacoraApi(fecha_utc DESC);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BitacoraApi')
      AND name = N'IX_BitacoraApi_ModuloFecha'
)
    CREATE INDEX IX_BitacoraApi_ModuloFecha
        ON dbo.BitacoraApi(modulo, fecha_utc DESC)
        INCLUDE (referencia_id, metodo_http, codigo_estado, duracion_ms);
GO

IF SCHEMA_ID(N'Reportes') IS NULL
    EXEC(N'CREATE SCHEMA Reportes AUTHORIZATION dbo;');
GO

CREATE OR ALTER VIEW Reportes.vw_GeoposicionPedidos
AS
    SELECT
        pedido.id_pedido,
        pedido.cod_pedido,
        pedido.estado_pedido,
        pedido.cod_sucursal,
        sucursal.nombre_sucursal,
        sucursal.direccion_sucursal,
        sucursal.latitud AS sucursal_latitud,
        sucursal.longitud AS sucursal_longitud,
        direccion.nombre_direccion AS destino_nombre,
        direccion.direccion_completa AS destino_direccion,
        direccion.referencia AS destino_referencia,
        direccion.latitud AS destino_latitud,
        direccion.longitud AS destino_longitud,
        pedido.cod_repartidor,
        CONCAT(repartidor.nombre_repartidor, ' ', repartidor.apellidos_repartidor)
            AS repartidor_nombre,
        repartidor.estado_disponibilidad,
        ultima.id_ubicacion,
        ultima.latitud AS repartidor_latitud,
        ultima.longitud AS repartidor_longitud,
        ultima.precision_metros,
        ultima.velocidad_kmh,
        ultima.rumbo_grados,
        ultima.fecha_dispositivo_utc,
        ultima.fecha_recepcion_utc,
        CASE
            WHEN ultima.fecha_recepcion_utc IS NULL THEN CAST(1 AS BIT)
            WHEN ultima.fecha_recepcion_utc < DATEADD(MINUTE, -5, SYSUTCDATETIME())
                THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END AS ubicacion_desactualizada,
        CASE
            WHEN sucursal.latitud IS NULL OR sucursal.longitud IS NULL THEN NULL
            ELSE CAST
            (
                geography::Point(sucursal.latitud, sucursal.longitud, 4326)
                    .STDistance(geography::Point(direccion.latitud, direccion.longitud, 4326))
                / 1000.0 AS DECIMAL(10, 2)
            )
        END AS sucursal_destino_km,
        CASE
            WHEN ultima.latitud IS NULL OR ultima.longitud IS NULL THEN NULL
            ELSE CAST
            (
                geography::Point(ultima.latitud, ultima.longitud, 4326)
                    .STDistance(geography::Point(direccion.latitud, direccion.longitud, 4326))
                / 1000.0 AS DECIMAL(10, 2)
            )
        END AS repartidor_destino_km
    FROM dbo.Pedidos AS pedido
    INNER JOIN dbo.Sucursal AS sucursal
        ON sucursal.cod_sucursal = pedido.cod_sucursal
    INNER JOIN dbo.Direcciones_Cliente AS direccion
        ON direccion.id_direccion = pedido.id_direccion_entrega
    LEFT JOIN dbo.Repartidores AS repartidor
        ON repartidor.cod_repartidor = pedido.cod_repartidor
    OUTER APPLY
    (
        SELECT TOP (1)
            ubicacion.id_ubicacion,
            ubicacion.latitud,
            ubicacion.longitud,
            ubicacion.precision_metros,
            ubicacion.velocidad_kmh,
            ubicacion.rumbo_grados,
            ubicacion.fecha_dispositivo_utc,
            ubicacion.fecha_recepcion_utc
        FROM dbo.Ubicaciones_Repartidor AS ubicacion
        WHERE ubicacion.id_pedido = pedido.id_pedido
           OR
           (
               ubicacion.id_pedido IS NULL
               AND ubicacion.cod_repartidor = pedido.cod_repartidor
           )
        ORDER BY ubicacion.fecha_recepcion_utc DESC, ubicacion.id_ubicacion DESC
    ) AS ultima;
GO

SELECT
    DB_NAME() AS base_datos,
    CASE WHEN OBJECT_ID(N'dbo.BitacoraApi', N'U') IS NOT NULL THEN 1 ELSE 0 END
        AS tabla_bitacora,
    CASE WHEN OBJECT_ID(N'Reportes.vw_GeoposicionPedidos', N'V') IS NOT NULL THEN 1 ELSE 0 END
        AS vista_geoposicion,
    (SELECT COUNT(*) FROM dbo.Rol_Permisos
     WHERE cod_rol = 'ADMIN'
       AND cod_permiso IN ('REPORTE_VER', 'BITACORA_VER', 'UBICACION_VER'))
        AS permisos_admin;
GO
