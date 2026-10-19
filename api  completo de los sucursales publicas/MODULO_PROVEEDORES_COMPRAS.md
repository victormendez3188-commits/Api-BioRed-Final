# Módulo de proveedores y compras

## Preparación

1. Ejecute `SQL/09_Preparar_Proveedores_Compras.sql` una sola vez.
2. Recompile la solución.
3. Inicie sesión con la cuenta administrativa y autorice el token Bearer.

El script crea los estados `Registrada` y `Anulada`, protege CUI/NIT, correo y
documentos duplicados, agrega lote y vencimiento al detalle de compra y crea
índices de consulta. Es idempotente: puede ejecutarse de nuevo sin duplicar
objetos.

## Proveedores

- `GET /api/v1/management/suppliers`
- `GET /api/v1/management/suppliers/{supplierCode}`
- `POST /api/v1/management/suppliers`
- `PUT /api/v1/management/suppliers/{supplierCode}`
- `DELETE /api/v1/management/suppliers/{supplierCode}`

### Proveedor de prueba

```json
{
  "supplierCode": "PROV-TEST-01",
  "companyCode": "EMP-ADEPH",
  "supplierName": "Distribuidora Farmacéutica de Prueba",
  "taxId": "NIT-PROV-001",
  "businessName": "Distribuidora Farmacéutica de Prueba, S.A.",
  "phone": "22223333",
  "email": "proveedor.prueba@biored.com"
}
```

## Compras

- `GET /api/v1/management/purchases`
- `GET /api/v1/management/purchases/{purchaseId}`
- `POST /api/v1/management/purchases`
- `POST /api/v1/management/purchases/{purchaseId}/cancel`

### Registrar compra de prueba

```json
{
  "supplierCode": "PROV-TEST-01",
  "branchCode": "SUC-ADE-01",
  "paymentTypeCode": "PAGO-EFECTIVO",
  "documentSeries": "FAC",
  "documentNumber": "COMP-TEST-001",
  "purchaseDateUtc": null,
  "observations": "Compra de prueba del módulo de proveedores.",
  "items": [
    {
      "productCode": "PROD-TEST-01",
      "quantity": 10,
      "costPrice": 20,
      "salePrice": 35,
      "lotNumber": "LOTE-COMPRA-001",
      "expirationDate": "2027-12-31"
    },
    {
      "productCode": "VIT-001",
      "quantity": 20,
      "costPrice": 8.75,
      "salePrice": 12.50,
      "lotNumber": "LOTE-COMPRA-002",
      "expirationDate": "2027-10-31"
    }
  ]
}
```

El total esperado es `375.00`. La API lo calcula; no acepta un total enviado
por el cliente.

Al registrar la compra, la misma transacción:

- guarda encabezado y detalles;
- actualiza los precios actuales de los productos;
- aumenta el inventario de la sucursal;
- crea o actualiza los lotes;
- registra las entradas en Kardex.

### Anular compra

```json
{
  "reason": "Documento ingresado únicamente para comprobar la anulación."
}
```

La anulación revierte inventario y lotes y genera movimientos
`AnulacionEntrada` en Kardex. Se rechaza si ya no existe stock suficiente para
revertir lo recibido.

## Reglas importantes

- Todos los endpoints requieren rol `ADMIN`.
- El proveedor, sucursal y productos deben pertenecer a la misma empresa.
- No se permite repetir un producto en la misma compra.
- El precio de venta no puede ser menor que el costo.
- Lote y vencimiento se envían juntos; el vencimiento debe ser futuro.
- Un documento no puede repetirse para el mismo proveedor y serie.
- Proveedores se desactivan lógicamente; las compras no se eliminan.
