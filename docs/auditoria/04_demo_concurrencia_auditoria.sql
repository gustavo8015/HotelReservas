/* =====================================================================
   Hotel Reservas - Demostración de concurrencia en SSMS (auditoría)
   Requiere haber ejecutado 01_crear_base_de_datos.sql, 02_transaccion_confirmar_reserva.sql
   y 03_datos_de_prueba.sql. Abrir DOS ventanas de consulta en SSMS.
   ===================================================================== */

-- ---------------------------------------------------------------
-- VENTANA 1 (sesión A): inicia la transacción y se detiene antes de COMMIT
-- ---------------------------------------------------------------
USE HotelReservas;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
    -- verificación con bloqueo (la misma del procedimiento)
    SELECT COUNT(*) AS reservas_solapadas
    FROM RESERVA WITH (UPDLOCK, HOLDLOCK)
    WHERE id_habitacion = 3
      AND estado IN ('CONFIRMADA', 'EN_CASA')
      AND fecha_entrada < '2026-10-18' AND fecha_salida > '2026-10-16';

    INSERT INTO RESERVA (codigo, id_huesped, id_habitacion, id_usuario, fecha_entrada, fecha_salida,
                         numero_personas, valor_total, estado)
    VALUES ('RSV000000101', 1, 3, 2, '2026-10-16', '2026-10-18', 2, 360000, 'CONFIRMADA');
    -- NO ejecutar COMMIT todavía: pasar a la ventana 2 y mostrar que queda en espera.

-- ---------------------------------------------------------------
-- VENTANA 2 (sesión B): intenta la misma habitación y fechas
-- Queda BLOQUEADA (en ejecución) hasta que la sesión A termine.
-- ---------------------------------------------------------------
USE HotelReservas;
DECLARE @id INT, @cod NVARCHAR(12);
EXEC sp_ConfirmarReserva
     @id_huesped = 2, @id_habitacion = 3, @id_usuario = 3,
     @fecha_entrada = '2026-10-16', @fecha_salida = '2026-10-18',
     @numero_personas = 2, @medio_pago = 'EFECTIVO',
     @id_reserva_out = @id OUTPUT, @codigo_out = @cod OUTPUT;
SELECT @id AS id_reserva, @cod AS codigo;

-- ---------------------------------------------------------------
-- VENTANA 1: confirmar. En ese instante la ventana 2 se desbloquea,
-- encuentra el solapamiento y falla con el error 50001 (Habitación no disponible).
-- ---------------------------------------------------------------
COMMIT TRANSACTION;

-- ---------------------------------------------------------------
-- Verificación: solo existe UNA reserva para la habitación 3 en esas fechas
-- y ningún pago, comprobante ni bitácora huérfanos.
-- ---------------------------------------------------------------
SELECT codigo, id_habitacion, fecha_entrada, fecha_salida, estado, version_fila
FROM RESERVA WHERE id_habitacion = 3 ORDER BY id_reserva;

SELECT 'PAGO sin reserva' AS verificacion, COUNT(*) AS filas FROM PAGO p
 WHERE NOT EXISTS (SELECT 1 FROM RESERVA r WHERE r.id_reserva = p.id_reserva)
UNION ALL
SELECT 'COMPROBANTE sin reserva', COUNT(*) FROM COMPROBANTE c
 WHERE NOT EXISTS (SELECT 1 FROM RESERVA r WHERE r.id_reserva = c.id_reserva);

-- Camino feliz (sin competencia): ejecutar en cualquier ventana
DECLARE @id2 INT, @cod2 NVARCHAR(12);
EXEC sp_ConfirmarReserva
     @id_huesped = 3, @id_habitacion = 8, @id_usuario = 2,
     @fecha_entrada = '2026-10-20', @fecha_salida = '2026-10-22',
     @numero_personas = 2, @medio_pago = 'TARJETA',
     @id_reserva_out = @id2 OUTPUT, @codigo_out = @cod2 OUTPUT;
SELECT r.codigo, r.valor_total, p.medio, c.numero AS comprobante, b.estado_nuevo, b.fecha
FROM RESERVA r JOIN PAGO p ON p.id_reserva = r.id_reserva
JOIN COMPROBANTE c ON c.id_reserva = r.id_reserva
JOIN BITACORA_RESERVA b ON b.id_reserva = r.id_reserva
WHERE r.id_reserva = @id2;

-- Limpieza opcional para repetir la demostración
-- DELETE FROM BITACORA_RESERVA WHERE id_reserva IN (SELECT id_reserva FROM RESERVA WHERE codigo IN ('RSV000000101'));
-- DELETE FROM RESERVA WHERE codigo = 'RSV000000101';
