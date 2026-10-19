# Corrección del endpoint de rutas

Esta copia usa OSRM público sin API key. La solicitud se construye con una URL
absoluta para evitar que `HttpClient` concatene dos veces `/route/v1`.

## Configuración

En `BioRed/appsettings.Development.json` debe permanecer únicamente la URL base:

```json
"OpenStreetMapRouting": {
  "Enabled": true,
  "BaseUrl": "https://router.project-osrm.org/",
  "UserAgent": "BioRed/1.0 (contacto@biored.com)"
}
```

No coloques aquí `/route/v1/driving`, coordenadas, `?overview` ni una API key.
En Azure se puede configurar con estas variables:

```text
OpenStreetMapRouting__Enabled=true
OpenStreetMapRouting__BaseUrl=https://router.project-osrm.org/
```

## Compilación limpia

Abre una terminal en la carpeta que contiene `BioRed/BioRed.sln`, cierra la API
que esté ejecutándose y ejecuta:

```powershell
dotnet restore BioRed/BioRed.sln
dotnet build BioRed/BioRed.sln --configuration Release --no-incremental
dotnet run --project BioRed/BioRed.csproj --launch-profile https
```

Este ZIP no incluye `bin`, `obj` ni `.vs`. No abras una segunda copia del
proyecto ni pruebes una API antigua que haya quedado ejecutándose en el puerto
7248.

## Prueba

El pedido debe estar en `En Camino` y tener un repartidor asignado. Con el JWT
del repartidor prueba una sola vez:

```text
GET https://localhost:7248/api/v1/orders/36/route?originLatitude=14.704000&originLongitude=-91.867500&travelMode=TWO_WHEELER
```

Si OSRM devuelve `429`, espera un minuto y vuelve a intentar una sola vez; es el
límite temporal del servicio público gratuito, no un error de la base de datos.
