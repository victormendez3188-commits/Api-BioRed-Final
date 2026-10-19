/* Verificación del módulo 10. Ejecutar conectado a BioRed. */

SET NOCOUNT ON;
GO

SELECT
    CASE WHEN OBJECT_ID(N'dbo.BitacoraApi', N'U') IS NOT NULL THEN 1 ELSE 0 END
        AS tabla_bitacora,
    CASE WHEN OBJECT_ID(N'Reportes.vw_GeoposicionPedidos', N'V') IS NOT NULL THEN 1 ELSE 0 END
        AS vista_geoposicion,
    CASE WHEN COL_LENGTH('dbo.Ubicaciones_Repartidor', 'precision_metros') IS NOT NULL
              AND COL_LENGTH('dbo.Ubicaciones_Repartidor', 'velocidad_kmh') IS NOT NULL
              AND COL_LENGTH('dbo.Ubicaciones_Repartidor', 'rumbo_grados') IS NOT NULL
              AND COL_LENGTH('dbo.Ubicaciones_Repartidor', 'fecha_dispositivo_utc') IS NOT NULL
              AND COL_LENGTH('dbo.Ubicaciones_Repartidor', 'fecha_recepcion_utc') IS NOT NULL
         THEN 1 ELSE 0 END AS columnas_geoposicion;
GO

SELECT
    permiso.cod_permiso,
    permiso.nombre_permiso,
    asignacion.cod_rol
FROM dbo.Permisos AS permiso
LEFT JOIN dbo.Rol_Permisos AS asignacion
    ON asignacion.cod_permiso = permiso.cod_permiso
   AND asignacion.cod_rol = 'ADMIN'
WHERE permiso.cod_permiso IN ('REPORTE_VER', 'BITACORA_VER', 'UBICACION_VER')
ORDER BY permiso.cod_permiso;
GO

SELECT TOP (20)
    id_pedido,
    cod_pedido,
    estado_pedido,
    nombre_sucursal,
    sucursal_latitud,
    sucursal_longitud,
    destino_direccion,
    destino_latitud,
    destino_longitud,
    cod_repartidor,
    repartidor_nombre,
    repartidor_latitud,
    repartidor_longitud,
    precision_metros,
    velocidad_kmh,
    rumbo_grados,
    fecha_recepcion_utc,
    ubicacion_desactualizada,
    sucursal_destino_km,
    repartidor_destino_km
FROM Reportes.vw_GeoposicionPedidos
ORDER BY id_pedido DESC;
GO

SELECT TOP (20)
    id_bitacora,
    id_cuenta,
    tipo_usuario,
    referencia_id,
    modulo,
    metodo_http,
    ruta,
    codigo_estado,
    duracion_ms,
    fecha_utc
FROM dbo.BitacoraApi
ORDER BY id_bitacora DESC;
GO
