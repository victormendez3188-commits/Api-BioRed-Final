# Módulo 10: reportes, bitácora y geolocalización

Este módulo completa la API BioRed con reportes administrativos, bitácora automática y consultas enriquecidas de geoposición.

## 1. Preparar la base de datos

En SQL Server Management Studio, conectado directamente a la base `BioRed`:

1. Abrir `SQL/13_Preparar_Reportes_Bitacora_Geolocalizacion.sql`.
2. Ejecutar todo el archivo.
3. El resultado final esperado es:
   - `tabla_bitacora = 1`
   - `vista_geoposicion = 1`
   - `permisos_admin = 3`
4. Abrir y ejecutar `SQL/13B_Verificar_Reportes_Bitacora_Geolocalizacion.sql`.

La vista SQL `Reportes.vw_GeoposicionPedidos` es la consulta directa que muestra la geolocalización completa de cada pedido.

## 2. Compilar y renovar el token

1. Recompilar toda la solución en Visual Studio.
2. Iniciar la API.
3. Volver a iniciar sesión como administrador.
4. Copiar el nuevo token Bearer.

Es necesario iniciar sesión nuevamente porque el token anterior todavía no contiene los permisos `REPORTE_VER` y `BITACORA_VER`.

## 3. Consultar la geoposición de un pedido

Usar:

`GET /api/v1/geolocation/orders/{orderId}/position`

Ejemplo para el pedido utilizado en las pruebas anteriores:

`GET /api/v1/geolocation/orders/5/position`

La respuesta muestra:

- coordenadas y enlace de mapa de la sucursal;
- dirección, coordenadas y enlace de mapa del cliente;
- repartidor asignado y su última posición;
- precisión, velocidad, rumbo y horas del GPS;
- distancia sucursal-destino y repartidor-destino;
- `isStale`, que será `true` si la ubicación tiene más de 5 minutos;
- enlace `routeMapUrl` con la ruta en Google Maps.

## 4. Consultar todos los repartidores

Usar:

`GET /api/v1/geolocation/drivers/positions`

Filtro opcional:

`GET /api/v1/geolocation/drivers/positions?branchCode=SUC-ADE-01`

Este endpoint requiere el permiso `UBICACION_VER`.

## 5. Enviar una ubicación GPS enriquecida

Este endpoint se prueba con un token de tipo `REPARTIDOR`:

`POST /api/v1/geolocation/drivers/location`

Ejemplo de cuerpo JSON:

```json
{
  "latitude": 14.634915,
  "longitude": -90.506882,
  "orderId": 5,
  "accuracyMeters": 8.5,
  "speedKmh": 22.4,
  "headingDegrees": 145.2,
  "deviceRecordedAtUtc": "2026-09-08T06:00:00Z"
}
```

`accuracyMeters`, `speedKmh`, `headingDegrees` y `deviceRecordedAtUtc` son opcionales para mantener compatibilidad con las pruebas anteriores.

## 6. Reportes administrativos

Todos requieren el permiso `REPORTE_VER`.

- Resumen general:
  `GET /api/v1/reports/dashboard`
- Ventas agrupadas por día:
  `GET /api/v1/reports/sales/daily`
- Productos con existencias bajas:
  `GET /api/v1/reports/inventory/low-stock`

Filtros opcionales del resumen y ventas:

- `fromUtc`
- `toUtc`
- `branchCode`

Ejemplo:

`GET /api/v1/reports/dashboard?branchCode=SUC-ADE-01`

## 7. Consultar la bitácora

Usar:

`GET /api/v1/reports/audit?page=1&pageSize=20`

Filtros opcionales:

- `fromUtc` y `toUtc`;
- `module`, por ejemplo `payments`, `orders` o `geolocation`;
- `referenceId`;
- `statusCode`;
- `httpMethod`.

La bitácora registra ruta, método, usuario, estado HTTP, duración, IP, identificador de correlación y fecha UTC. Por seguridad, no almacena contraseñas, tokens, encabezados de autorización ni cuerpos JSON.

## Orden recomendado de prueba

1. Ejecutar los scripts `13` y `13B`.
2. Recompilar sin errores.
3. Reiniciar la API e iniciar sesión nuevamente.
4. Probar `GET /api/v1/reports/dashboard`.
5. Probar `GET /api/v1/geolocation/orders/5/position`.
6. Probar `GET /api/v1/geolocation/drivers/positions`.
7. Probar `GET /api/v1/reports/audit` al final, para ver las solicitudes anteriores registradas.
