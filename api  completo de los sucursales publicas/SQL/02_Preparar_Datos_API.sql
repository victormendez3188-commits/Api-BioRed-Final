/*
   Ejecutar conectado directamente a la base BioRed de Azure SQL.
   No crea ni elimina tablas. Agrega datos iniciales e índices para la API.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM Permisos WHERE cod_permiso = 'PRODUCTO_VER')
        INSERT INTO Permisos (cod_permiso, nombre_permiso, descripcion, modulo)
        VALUES ('PRODUCTO_VER', 'Ver productos', 'Permite consultar productos.', 'Productos');

    IF NOT EXISTS (SELECT 1 FROM Permisos WHERE cod_permiso = 'PEDIDO_VER')
        INSERT INTO Permisos (cod_permiso, nombre_permiso, descripcion, modulo)
        VALUES ('PEDIDO_VER', 'Ver pedidos', 'Permite consultar pedidos.', 'Delivery');

    IF NOT EXISTS (SELECT 1 FROM Permisos WHERE cod_permiso = 'UBICACION_VER')
        INSERT INTO Permisos (cod_permiso, nombre_permiso, descripcion, modulo)
        VALUES ('UBICACION_VER', 'Ver ubicaciones', 'Permite consultar el seguimiento.', 'Delivery');

    INSERT INTO Rol_Permisos (cod_rol, cod_permiso)
    SELECT 'ADMIN', permiso.cod_permiso
    FROM Permisos AS permiso
    WHERE permiso.cod_permiso IN ('PRODUCTO_VER', 'PEDIDO_VER', 'UBICACION_VER')
      AND EXISTS (SELECT 1 FROM Rol WHERE cod_rol = 'ADMIN')
      AND NOT EXISTS
      (
          SELECT 1
          FROM Rol_Permisos AS asignacion
          WHERE asignacion.cod_rol = 'ADMIN'
            AND asignacion.cod_permiso = permiso.cod_permiso
      );

    INSERT INTO Configuracion_Entrega_Sucursal
    (
        cod_sucursal,
        radio_maximo_km,
        tarifa_base,
        tarifa_por_km_extra,
        tiempo_estimado_min,
        activo
    )
    SELECT
        sucursal.cod_sucursal,
        10.00,
        15.00,
        3.00,
        30,
        1
    FROM Sucursal AS sucursal
    WHERE sucursal.estado = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM Configuracion_Entrega_Sucursal AS configuracion
          WHERE configuracion.cod_sucursal = sucursal.cod_sucursal
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
    WHERE object_id = OBJECT_ID('Cuentas_Acceso')
      AND name = 'UX_CuentasAcceso_TipoReferencia'
)
    CREATE UNIQUE INDEX UX_CuentasAcceso_TipoReferencia
        ON Cuentas_Acceso(tipo_usuario, referencia_id);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('Refresh_Tokens')
      AND name = 'IX_RefreshTokens_CuentaVigencia'
)
    CREATE INDEX IX_RefreshTokens_CuentaVigencia
        ON Refresh_Tokens(id_cuenta, fecha_expiracion, revocado);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('Ubicaciones_Repartidor')
      AND name = 'IX_UbicacionesRepartidor_RepartidorFecha'
)
    CREATE INDEX IX_UbicacionesRepartidor_RepartidorFecha
        ON Ubicaciones_Repartidor(cod_repartidor, fecha_registro DESC);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('Ubicaciones_Repartidor')
      AND name = 'IX_UbicacionesRepartidor_PedidoFecha'
)
    CREATE INDEX IX_UbicacionesRepartidor_PedidoFecha
        ON Ubicaciones_Repartidor(id_pedido, fecha_registro DESC)
        WHERE id_pedido IS NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('Seguimiento_Pedido')
      AND name = 'IX_SeguimientoPedido_PedidoFecha'
)
    CREATE INDEX IX_SeguimientoPedido_PedidoFecha
        ON Seguimiento_Pedido(id_pedido, fecha_hora DESC);
GO

SELECT
    DB_NAME() AS base_datos,
    (SELECT COUNT(*) FROM Permisos) AS permisos,
    (SELECT COUNT(*) FROM Configuracion_Entrega_Sucursal) AS sucursales_configuradas;
GO
