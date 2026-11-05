-- =====================================================================
-- Hotel Reservas · Script 08 · Tipos de habitación por cantidad de personas
-- Entrega S14 (APK v0.9): se elimina el tipo Suite y se agregan Triple (3) y Cuádruple (4).
-- Ejecutar después de los scripts 01 a 07. Es idempotente.
-- =====================================================================
USE HotelReservas;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. El tipo 3 (antes Suite, capacidad 4) pasa a llamarse Cuádruple: conserva su id, sus habitaciones y sus reservas.
UPDATE TIPO_HABITACION SET nombre = N'Cuádruple', capacidad = 4, descripcion = N'Cuatro camas sencillas, baño privado'
WHERE id_tipo = 3;

-- 2. Nuevo tipo Triple (capacidad 3) con su tarifa vigente.
IF NOT EXISTS (SELECT 1 FROM TIPO_HABITACION WHERE nombre = N'Triple')
BEGIN
    INSERT INTO TIPO_HABITACION (nombre, capacidad, descripcion) VALUES (N'Triple', 3, N'Tres camas sencillas, baño privado');
END
DECLARE @triple INT = (SELECT id_tipo FROM TIPO_HABITACION WHERE nombre = N'Triple');
IF NOT EXISTS (SELECT 1 FROM TARIFA WHERE id_tipo = @triple AND vigencia_hasta IS NULL)
    INSERT INTO TARIFA (id_tipo, valor_noche, vigencia_desde, vigencia_hasta) VALUES (@triple, 220000.00, '2026-11-01', NULL);

-- 3. La tarifa del Cuádruple se ajusta a 260.000 (se cierra la anterior para conservar el precio de las reservas ya confirmadas).
UPDATE TARIFA SET vigencia_hasta = '2026-10-31' WHERE id_tipo = 3 AND vigencia_hasta IS NULL AND valor_noche <> 260000.00;
IF NOT EXISTS (SELECT 1 FROM TARIFA WHERE id_tipo = 3 AND vigencia_hasta IS NULL)
    INSERT INTO TARIFA (id_tipo, valor_noche, vigencia_desde, vigencia_hasta) VALUES (3, 260000.00, '2026-11-01', NULL);

-- 4. Una habitación Triple por piso: la segunda sencilla de cada piso (102, 202, 302, 402, 502).
UPDATE HABITACION SET id_tipo = @triple WHERE numero IN ('102', '202', '302', '402', '502');

COMMIT TRANSACTION;
GO
-- Verificación
SELECT t.id_tipo, t.nombre, t.capacidad, COUNT(h.id_habitacion) AS habitaciones,
       (SELECT valor_noche FROM TARIFA WHERE id_tipo = t.id_tipo AND vigencia_hasta IS NULL) AS tarifa_vigente
FROM TIPO_HABITACION t LEFT JOIN HABITACION h ON h.id_tipo = t.id_tipo
GROUP BY t.id_tipo, t.nombre, t.capacidad ORDER BY t.capacidad;
GO
