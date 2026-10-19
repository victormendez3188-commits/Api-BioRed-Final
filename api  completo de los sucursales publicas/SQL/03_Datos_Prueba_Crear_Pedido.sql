/*
   Datos mínimos para probar POST /api/v1/orders.
   No crea ni elimina tablas. Solo agrega un producto de prueba y su inventario.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Productos
        WHERE cod_producto = 'PROD-TEST-01'
    )
    BEGIN
        INSERT INTO Productos
        (
            cod_producto,
            cod_empresa,
            nombre_producto,
            descripcion_producto,
            precio_costo,
            precio_venta,
            estado,
            creado_por
        )
        VALUES
        (
            'PROD-TEST-01',
            'EMP-ADEPH',
            'Producto farmacéutico de prueba',
            'Registro utilizado para validar la creación de pedidos por API.',
            20.00,
            35.00,
            1,
            'USR-ADMIN'
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Inventario
        WHERE cod_sucursal = 'SUC-ADE-01'
          AND cod_producto = 'PROD-TEST-01'
    )
    BEGIN
        INSERT INTO Inventario
        (
            cod_sucursal,
            cod_producto,
            cantidad_actual,
            stock_minimo,
            stock_maximo,
            ultima_actualizacion
        )
        VALUES
        (
            'SUC-ADE-01',
            'PROD-TEST-01',
            50,
            5,
            100,
            GETUTCDATE()
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT
    p.cod_producto,
    p.nombre_producto,
    p.precio_venta,
    i.cod_sucursal,
    i.cantidad_actual
FROM Productos AS p
INNER JOIN Inventario AS i
    ON i.cod_producto = p.cod_producto
WHERE p.cod_producto = 'PROD-TEST-01'
  AND i.cod_sucursal = 'SUC-ADE-01';
GO
