-- =====================================================================
-- Hotel Reservas · Script 07: soporte del servicio inteligente (RF19, RF21) y del perfil CLIENTE
-- Ejecutar despues de 01 a 06 sobre la base HotelReservas.
-- 1) Columna riesgo_cancelacion en RESERVA (probabilidad calculada al confirmar, RF19).
-- 2) Perfil CLIENTE en USUARIO y vinculo con HUESPED (usuario normal de la app v0.5).
-- 3) Vistas que exportan el historial con el que se entrenan los modelos (misma estructura
--    que datos/cancelaciones.csv y datos/ocupacion.csv del servicio_inteligente).
-- =====================================================================
USE HotelReservas;
GO

IF COL_LENGTH('RESERVA', 'riesgo_cancelacion') IS NULL
    ALTER TABLE RESERVA ADD riesgo_cancelacion DECIMAL(5,4) NULL
        CONSTRAINT CK_RESERVA_riesgo CHECK (riesgo_cancelacion IS NULL OR riesgo_cancelacion BETWEEN 0 AND 1);
GO

-- Perfil CLIENTE (huesped que reserva y paga desde la app) y relacion con su registro de HUESPED
ALTER TABLE USUARIO DROP CONSTRAINT CK_USUARIO_rol;
ALTER TABLE USUARIO ADD CONSTRAINT CK_USUARIO_rol CHECK (rol IN ('RECEPCIONISTA', 'ADMINISTRADOR', 'CLIENTE'));
IF COL_LENGTH('USUARIO', 'id_huesped') IS NULL
    ALTER TABLE USUARIO ADD id_huesped INT NULL CONSTRAINT FK_USUARIO_HUESPED REFERENCES HUESPED (id_huesped);
GO

-- Historial para el modelo de cancelaciones: una fila por reserva terminada (cumplida o cancelada)
CREATE OR ALTER VIEW vw_historial_cancelaciones AS
SELECT
    DATEDIFF(DAY, CAST(r.fecha_creacion AS DATE), r.fecha_entrada)            AS anticipacion_dias,
    DATEDIFF(DAY, r.fecha_entrada, r.fecha_salida)                              AS noches,
    r.numero_personas                                                           AS personas,
    t.id_tipo                                                                   AS tipo,
    ISNULL((SELECT TOP 1 p.medio FROM PAGO p WHERE p.id_reserva = r.id_reserva ORDER BY p.id_pago DESC), 'TRANSFERENCIA') AS medio_pago,
    CASE WHEN (SELECT COUNT(*) FROM RESERVA r2 WHERE r2.id_huesped = r.id_huesped AND r2.id_reserva < r.id_reserva
               AND r2.estado IN ('CONFIRMADA','EN_CASA','FINALIZADA')) >= 2 THEN 1 ELSE 0 END AS huesped_frecuente,
    CASE WHEN DATEPART(WEEKDAY, r.fecha_entrada) IN (6, 7) THEN 1 ELSE 0 END    AS fin_de_semana,   -- viernes y sabado (DATEFIRST 7)
    CASE WHEN r.estado = 'CANCELADA' THEN 1 ELSE 0 END                          AS cancelada
FROM RESERVA r
JOIN HABITACION h ON h.id_habitacion = r.id_habitacion
JOIN TIPO_HABITACION t ON t.id_tipo = h.id_tipo
WHERE r.estado IN ('FINALIZADA', 'CANCELADA');
GO

-- Historial para el modelo de ocupacion: una fila por dia con las reservas anticipadas y la ocupacion observada
CREATE OR ALTER VIEW vw_ocupacion_diaria AS
WITH dias AS (
    SELECT CAST(MIN(fecha_entrada) AS DATE) AS d, CAST(MAX(fecha_salida) AS DATE) AS fin FROM RESERVA
    UNION ALL SELECT DATEADD(DAY, 1, d), fin FROM dias WHERE d < fin
)
SELECT d AS fecha,
    DATEPART(WEEKDAY, d) - 1                                                     AS dia_semana,     -- 0 = domingo
    CASE WHEN DATEPART(WEEKDAY, d) IN (6, 7) THEN 1 ELSE 0 END                   AS fin_de_semana,
    CASE WHEN MONTH(d) IN (12, 1, 6, 7) THEN 1 ELSE 0 END                        AS temporada_alta,
    (SELECT COUNT(*) FROM RESERVA r WHERE r.fecha_entrada <= d AND r.fecha_salida > d
        AND CAST(r.fecha_creacion AS DATE) <= DATEADD(DAY, -7, d))               AS reservas_anticipadas,   -- registradas con 7 o mas dias
    (SELECT COUNT(*) FROM RESERVA r WHERE r.fecha_entrada <= d AND r.fecha_salida > d
        AND r.estado IN ('EN_CASA', 'FINALIZADA'))                               AS habitaciones_ocupadas
FROM dias
OPTION (MAXRECURSION 0);
GO

-- Exportacion (desde SSMS: resultados → guardar como CSV, o con bcp):
-- bcp "SELECT * FROM HotelReservas.dbo.vw_historial_cancelaciones" queryout datos\cancelaciones.csv -c -t, -T
-- bcp "SELECT * FROM HotelReservas.dbo.vw_ocupacion_diaria"        queryout datos\ocupacion.csv     -c -t, -T
SELECT 'Script 07 aplicado: riesgo_cancelacion, perfil CLIENTE, vistas de historial' AS resultado;
