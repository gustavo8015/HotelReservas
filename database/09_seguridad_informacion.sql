-- =====================================================================
-- Hotel Reservas · Script 09: seguridad de la informacion y proteccion de datos personales
-- Base normativa: Ley 1266 de 2008 (art. 4 seguridad y confidencialidad, art. 6 derechos del titular),
-- Ley 1581 de 2012 (art. 4 principios, art. 8 derechos, art. 9 autorizacion, art. 15 reclamos, art. 17 deberes),
-- Decreto 1377 de 2013 (arts. 5 y 9 autorizacion y revocatoria), Ley 1273 de 2009 (art. 269A acceso abusivo)
-- y la Politica de Seguridad y Privacidad de la Informacion de Hotel Reservas (docs/seguridad).
-- Ejecutar despues de 01 a 08 sobre la base HotelReservas.
-- =====================================================================
USE HotelReservas;
GO

-- 1) Autorizacion de tratamiento de datos del huesped (Ley 1581 art. 9; Decreto 1377 art. 5)
IF COL_LENGTH('HUESPED', 'autorizacion_datos') IS NULL
    ALTER TABLE HUESPED ADD autorizacion_datos BIT NOT NULL CONSTRAINT DF_HUESPED_autorizacion DEFAULT 0;
IF COL_LENGTH('HUESPED', 'fecha_autorizacion') IS NULL
    ALTER TABLE HUESPED ADD fecha_autorizacion DATETIME2(0) NULL;
IF COL_LENGTH('HUESPED', 'fecha_revocatoria') IS NULL
    ALTER TABLE HUESPED ADD fecha_revocatoria DATETIME2(0) NULL;          -- Decreto 1377 art. 9
GO

-- 2) Control de intentos de acceso (Ley 1273 art. 269A; RNF01): bloqueo de 15 minutos tras 5 intentos fallidos
IF COL_LENGTH('USUARIO', 'intentos_fallidos') IS NULL
    ALTER TABLE USUARIO ADD intentos_fallidos TINYINT NOT NULL CONSTRAINT DF_USUARIO_intentos DEFAULT 0;
IF COL_LENGTH('USUARIO', 'bloqueado_hasta') IS NULL
    ALTER TABLE USUARIO ADD bloqueado_hasta DATETIME2(0) NULL;
IF COL_LENGTH('USUARIO', 'ultimo_acceso') IS NULL
    ALTER TABLE USUARIO ADD ultimo_acceso DATETIME2(0) NULL;
IF COL_LENGTH('USUARIO', 'clave_actualizada') IS NULL
    ALTER TABLE USUARIO ADD clave_actualizada DATETIME2(0) NULL;         -- para exigir cambio cada 180 dias
GO

-- 3) Auditoria de seguridad: cada evento relevante queda registrado (Ley 1581 art. 17 lit. d y k; politica, numeral 10)
IF OBJECT_ID('AUDITORIA_SEGURIDAD') IS NULL
CREATE TABLE AUDITORIA_SEGURIDAD (
    id_evento       BIGINT IDENTITY(1,1) CONSTRAINT PK_AUDITORIA_SEGURIDAD PRIMARY KEY,
    fecha           DATETIME2(0) NOT NULL CONSTRAINT DF_AUDSEG_fecha DEFAULT SYSDATETIME(),
    tipo            VARCHAR(40)  NOT NULL,      -- LOGIN_OK, LOGIN_FALLIDO, BLOQUEO, LOGOUT, SESION_EXPIRADA, HABEAS_CONSULTA,
                                                -- HABEAS_RECTIFICACION, HABEAS_REVOCATORIA, HABEAS_SUPRESION, DATO_FILTRADO_IA,
                                                -- ACCESO_DENEGADO, CAMBIO_CLAVE, EXPORTACION
    id_usuario      INT NULL CONSTRAINT FK_AUDSEG_USUARIO REFERENCES USUARIO (id_usuario),
    correo          VARCHAR(120) NULL,          -- correo intentado cuando no hay usuario
    id_huesped      INT NULL CONSTRAINT FK_AUDSEG_HUESPED REFERENCES HUESPED (id_huesped),
    detalle         VARCHAR(400) NULL,          -- nunca contiene documentos, telefonos ni correos de huespedes
    direccion_ip    VARCHAR(45)  NULL,
    estado          VARCHAR(20)  NOT NULL CONSTRAINT DF_AUDSEG_estado DEFAULT 'REGISTRADO',   -- REGISTRADO, EN_TRAMITE, RESUELTO
    CONSTRAINT CK_AUDSEG_tipo CHECK (tipo IN ('LOGIN_OK','LOGIN_FALLIDO','BLOQUEO','LOGOUT','SESION_EXPIRADA','HABEAS_CONSULTA',
        'HABEAS_RECTIFICACION','HABEAS_REVOCATORIA','HABEAS_SUPRESION','DATO_FILTRADO_IA','ACCESO_DENEGADO','CAMBIO_CLAVE','EXPORTACION'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AUDSEG_fecha')
    CREATE INDEX IX_AUDSEG_fecha ON AUDITORIA_SEGURIDAD (fecha DESC, tipo);
GO

-- 4) Solicitudes de habeas data (Ley 1581 arts. 14 y 15; Ley 1266 art. 16): consulta, rectificacion, revocatoria y supresion
IF OBJECT_ID('SOLICITUD_HABEAS_DATA') IS NULL
CREATE TABLE SOLICITUD_HABEAS_DATA (
    id_solicitud    INT IDENTITY(1,1) CONSTRAINT PK_SOLICITUD_HABEAS PRIMARY KEY,
    id_huesped      INT NOT NULL CONSTRAINT FK_HABEAS_HUESPED REFERENCES HUESPED (id_huesped),
    tipo            VARCHAR(20) NOT NULL CONSTRAINT CK_HABEAS_tipo CHECK (tipo IN ('CONSULTA','RECTIFICACION','REVOCATORIA','SUPRESION')),
    fecha_solicitud DATETIME2(0) NOT NULL CONSTRAINT DF_HABEAS_fecha DEFAULT SYSDATETIME(),
    detalle         VARCHAR(400) NULL,
    estado          VARCHAR(20) NOT NULL CONSTRAINT DF_HABEAS_estado DEFAULT 'EN_TRAMITE'
                    CONSTRAINT CK_HABEAS_estado CHECK (estado IN ('EN_TRAMITE','RESUELTA','RECHAZADA')),
    fecha_limite    AS DATEADD(DAY, CASE WHEN tipo = 'CONSULTA' THEN 10 ELSE 15 END, CAST(fecha_solicitud AS DATE)) PERSISTED,  -- dias de la ley (habiles, aproximados)
    fecha_respuesta DATETIME2(0) NULL,
    respuesta       VARCHAR(400) NULL,
    id_usuario_resuelve INT NULL CONSTRAINT FK_HABEAS_USUARIO REFERENCES USUARIO (id_usuario)
);
GO

-- 5) Vista para el personal: datos del huesped enmascarados (principio de circulacion restringida, Ley 1581 art. 4 lit. f)
CREATE OR ALTER VIEW vw_huesped_enmascarado AS
SELECT h.id_huesped, h.nombres, h.apellidos, h.tipo_documento,
       CONCAT(REPLICATE('*', CASE WHEN LEN(h.numero_documento) > 4 THEN LEN(h.numero_documento) - 4 ELSE 0 END), RIGHT(h.numero_documento, 4)) AS documento_enmascarado,
       CASE WHEN h.telefono IS NULL THEN NULL ELSE CONCAT('***', RIGHT(h.telefono, 4)) END AS telefono_enmascarado,
       CASE WHEN h.correo IS NULL THEN NULL ELSE CONCAT(LEFT(h.correo, 2), '***', SUBSTRING(h.correo, CHARINDEX('@', h.correo), 120)) END AS correo_enmascarado,
       h.extranjero_no_residente, h.autorizacion_datos, h.fecha_autorizacion
FROM HUESPED h;
GO

-- 6) Procedimiento de supresion: anonimiza al huesped que lo solicita cuando no tiene reservas vigentes ni obligaciones tributarias pendientes
CREATE OR ALTER PROCEDURE sp_SuprimirDatosHuesped @id_huesped INT, @id_usuario INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM RESERVA WHERE id_huesped = @id_huesped AND estado IN ('PENDIENTE','CONFIRMADA','EN_CASA'))
    BEGIN
        RAISERROR('50030: el huesped tiene reservas vigentes; la supresion se atendera al terminar la estadia.', 16, 1); RETURN;
    END
    BEGIN TRANSACTION;
        -- Se conserva el registro (obligacion tributaria y de archivo sobre comprobantes), pero sin datos identificables
        UPDATE HUESPED SET nombres = 'SUPRIMIDO', apellidos = CONCAT('HUESPED-', id_huesped), numero_documento = CONCAT('SUPR', id_huesped),
               telefono = NULL, correo = NULL, autorizacion_datos = 0, fecha_revocatoria = SYSDATETIME()
        WHERE id_huesped = @id_huesped;
        UPDATE USUARIO SET activo = 0 WHERE id_huesped = @id_huesped;
        UPDATE SOLICITUD_HABEAS_DATA SET estado = 'RESUELTA', fecha_respuesta = SYSDATETIME(), respuesta = 'Datos suprimidos (anonimizados)', id_usuario_resuelve = @id_usuario
        WHERE id_huesped = @id_huesped AND tipo = 'SUPRESION' AND estado = 'EN_TRAMITE';
        INSERT INTO AUDITORIA_SEGURIDAD (tipo, id_usuario, id_huesped, detalle) VALUES ('HABEAS_SUPRESION', @id_usuario, @id_huesped, 'Supresion ejecutada por sp_SuprimirDatosHuesped');
    COMMIT TRANSACTION;
END
GO

-- 7) Datos de prueba: los huespedes existentes quedan con autorizacion registrada en la fecha de su primera reserva
UPDATE h SET autorizacion_datos = 1, fecha_autorizacion = COALESCE((SELECT MIN(r.fecha_creacion) FROM RESERVA r WHERE r.id_huesped = h.id_huesped), SYSDATETIME())
FROM HUESPED h WHERE h.autorizacion_datos = 0;
GO
PRINT 'Script 09 aplicado: autorizacion de datos, control de intentos, AUDITORIA_SEGURIDAD, SOLICITUD_HABEAS_DATA, vista enmascarada y sp_SuprimirDatosHuesped.';
