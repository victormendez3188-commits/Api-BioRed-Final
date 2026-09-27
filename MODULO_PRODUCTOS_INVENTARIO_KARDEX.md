# Módulo de productos, inventario, lotes y Kardex

Este módulo administra los productos y existencias que pertenecen directamente a BioRed. No reemplaza la integración con farmacias asociadas: BioRed seguirá consultando a esas farmacias exclusivamente mediante sus APIs públicas.

Todos los endpoints de este documento requieren iniciar sesión como **ADMIN** y enviar el Bearer Token.

## Preparación de la base de datos

Las cuatro tablas ya existen. Ejecute una sola vez:

`SQL/05_Preparar_Productos_Inventario.sql`

El script no agrega productos ni existencias de prueba. Únicamente valida las tablas y evita registros duplicados de inventario y lotes.

## Endpoints de productos

| Acción | Método | Endpoint |
|---|---|---|
| Listar y buscar | GET | `/api/v1/management/products` |
| Consultar uno | GET | `/api/v1/management/products/{productCode}` |
| Crear | POST | `/api/v1/management/products` |
| Actualizar o reactivar | PUT | `/api/v1/management/products/{productCode}` |
| Desactivar | DELETE | `/api/v1/management/products/{productCode}` |

Ejemplo para crear un producto:

```json
{
  "productCode": "VIT-001",
  "companyCode": "EMP-ADEPH",
  "productName": "Vitamina C 500 mg",
  "description": "Tabletas de vitamina C",
  "costPrice": 8.50,
  "salePrice": 12.00
}
```

Ejemplo para actualizarlo:

```json
{
  "productName": "Vitamina C 500 mg",
  "description": "Frasco de tabletas de vitamina C",
  "costPrice": 8.75,
  "salePrice": 12.50,
  "active": true
}
```

El código del producto y la empresa no cambian mediante el endpoint de actualización. El borrado es lógico: cambia `estado` a `false` y conserva el historial.

## Endpoints de inventario, lotes y Kardex

| Acción | Método | Endpoint |
|---|---|---|
| Ver inventario de una sucursal | GET | `/api/v1/inventory-management/branches/{branchCode}` |
| Consultar lotes de un producto | GET | `/api/v1/inventory-management/products/{productCode}/lots` |
| Registrar entrada o salida | POST | `/api/v1/inventory-management/movements` |
| Consultar Kardex | GET | `/api/v1/inventory-management/kardex` |

Ejemplo de entrada que crea un lote y el inventario inicial:

```json
{
  "branchCode": "SUC-001",
  "productCode": "VIT-001",
  "movementType": "Entrada",
  "quantity": 40,
  "minimumStock": 5,
  "maximumStock": 100,
  "lotNumber": "LOTE-VIT-001",
  "expirationDate": "2027-12-31",
  "referenceId": null,
  "observation": "Compra inicial para la sucursal"
}
```

Ejemplo de salida:

```json
{
  "branchCode": "SUC-001",
  "productCode": "VIT-001",
  "movementType": "Salida",
  "quantity": 2,
  "lotNumber": "LOTE-VIT-001",
  "observation": "Ajuste de inventario"
}
```

Cada movimiento modifica `Inventario`, actualiza el lote cuando se envía `lotNumber` y agrega un registro a `Kardex` dentro de una sola transacción. Si no hay existencias suficientes, ninguna tabla se modifica.

## Consultas y filtros

- Productos: `companyCode`, `search`, `includeInactive`, `page` y `pageSize`.
- Inventario: `search`, `onlyLowStock`, `page` y `pageSize`.
- Kardex: `branchCode`, `productCode`, `fromUtc`, `toUtc`, `page` y `pageSize`.
- La respuesta de lotes indica si cada lote está vencido y cuántos días faltan para su vencimiento.

## Orden recomendado para probar en Swagger

1. Iniciar sesión con una cuenta ADMIN y autorizar Swagger con el Bearer Token.
2. Crear un producto con un `companyCode` que ya tenga una sucursal en BioRed.
3. Registrar una entrada con el código real de esa sucursal.
4. Consultar inventario, lotes y Kardex.
5. Registrar una salida pequeña y volver a consultar los tres endpoints.

La tabla actual `Lotes` no tiene una columna de sucursal; por eso los lotes quedan asociados al producto, mientras que las existencias de `Inventario` sí se controlan por sucursal. La API respeta exactamente ese diseño existente de la base de datos.
