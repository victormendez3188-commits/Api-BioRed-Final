SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Cuentas_Acceso', N'U') IS NULL
    BEGIN
        THROW 51900, 'No existe la tabla dbo.Cuentas_Acceso.', 1;
    END;

    DECLARE @correo varchar(100) = 'bot-soporte@biored.invalid';
    DECLARE @referencia varchar(15) = 'BOT-SOPORTE';
    DECLARE @tipoUsuario varchar(20) = 'Empleado';

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Cuentas_Acceso
        WHERE correo = @correo
          AND (tipo_usuario <> @tipoUsuario OR referencia_id <> @referencia)
    )
    BEGIN
        THROW 51901, 'El correo reservado del bot ya pertenece a otra cuenta.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Cuentas_Acceso
        WHERE tipo_usuario = @tipoUsuario
          AND referencia_id = @referencia
          AND correo <> @correo
    )
    BEGIN
        THROW 51902, 'La referencia reservada del bot ya pertenece a otra cuenta.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Cuentas_Acceso
        WHERE tipo_usuario = @tipoUsuario
          AND referencia_id = @referencia
    )
    BEGIN
        INSERT INTO dbo.Cuentas_Acceso
        (
            correo,
            password_hash,
            tipo_usuario,
            referencia_id,
            estado
        )
        VALUES
        (
            @correo,
            '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
            @tipoUsuario,
            @referencia,
            0
        );
    END;

    COMMIT TRANSACTION;

    SELECT
        id_cuenta AS bot_account_id,
        correo,
        tipo_usuario,
        referencia_id,
        estado AS login_habilitado
    FROM dbo.Cuentas_Acceso
    WHERE tipo_usuario = @tipoUsuario
      AND referencia_id = @referencia;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
