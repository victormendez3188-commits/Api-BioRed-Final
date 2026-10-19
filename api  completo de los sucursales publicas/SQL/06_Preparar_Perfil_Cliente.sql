USE BioRed;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Sucursal', N'U') IS NULL OR
   OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    THROW 51010, 'Falta una tabla requerida: Clientes, Cuentas_Acceso, Sucursal o Usuarios.', 1;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE estado = 1)
BEGIN
    THROW 51015, 'Debe existir al menos un usuario interno activo para registrar clientes.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Clientes
    GROUP BY CUI_NIT
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51011, 'Existen documentos duplicados en Clientes. Corríjalos antes de continuar.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Clientes
    WHERE email_cliente IS NOT NULL
    GROUP BY email_cliente
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51012, 'Existen correos duplicados en Clientes. Corríjalos antes de continuar.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Cuentas_Acceso
    GROUP BY correo
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51013, 'Existen correos duplicados en Cuentas_Acceso. Corríjalos antes de continuar.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.Cuentas_Acceso
    GROUP BY tipo_usuario, referencia_id
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51014, 'Existen referencias de cuenta duplicadas. Corríjalas antes de continuar.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Clientes')
      AND name = N'UX_Clientes_CUI_NIT'
)
BEGIN
    CREATE UNIQUE INDEX UX_Clientes_CUI_NIT
        ON dbo.Clientes (CUI_NIT);

    PRINT 'Índice único de documentos creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de documentos ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Clientes')
      AND name = N'UX_Clientes_Email'
)
BEGIN
    CREATE UNIQUE INDEX UX_Clientes_Email
        ON dbo.Clientes (email_cliente)
        WHERE email_cliente IS NOT NULL;

    PRINT 'Índice único de correo del cliente creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de correo del cliente ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cuentas_Acceso')
      AND name = N'UX_Cuentas_Acceso_Correo'
)
BEGIN
    CREATE UNIQUE INDEX UX_Cuentas_Acceso_Correo
        ON dbo.Cuentas_Acceso (correo);

    PRINT 'Índice único de correo de acceso creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de correo de acceso ya existe.';
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cuentas_Acceso')
      AND name IN
      (
          N'UX_Cuentas_Acceso_Tipo_Referencia',
          N'UX_CuentasAcceso_TipoReferencia'
      )
)
BEGIN
    CREATE UNIQUE INDEX UX_Cuentas_Acceso_Tipo_Referencia
        ON dbo.Cuentas_Acceso (tipo_usuario, referencia_id);

    PRINT 'Índice único de tipo y referencia creado correctamente.';
END
ELSE
BEGIN
    PRINT 'El índice único de tipo y referencia ya existe.';
END;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.Clientes) AS clientes,
    (SELECT COUNT(*) FROM dbo.Cuentas_Acceso WHERE tipo_usuario = 'Cliente') AS cuentas_cliente,
    (SELECT COUNT(*) FROM dbo.Sucursal WHERE estado = 1) AS sucursales_activas,
    (SELECT COUNT(*) FROM dbo.Usuarios WHERE estado = 1) AS usuarios_internos_activos;
GO
