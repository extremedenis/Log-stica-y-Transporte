# Historial de Logística

## 0.5.0 — 2026-11-19

- Diseño común de controles y acceso; paleta propia y revisión móvil.
- Requisitos de contraseña visibles y controles accesibles.

## 0.4.2 — 19/11/2026

- Inicio público del producto sin exponer registros; Personas conserva permisos y 2FA.
- by Denis Corp regresa al portal corporativo local y pie de página adaptable.
- Versión del pie obtenida del ensamblado para evitar etiquetas antiguas.


## 0.4.1 — 18/11/2026

- Claves Identity de 128 caracteres coherentes entre diseño, UI y mantenimiento; corrige PendingModelChanges al migrar en producción.
- Migración adicional con guard que rechaza claves demasiado largas sin truncarlas ni borrar datos.
- Verificación de publicación productiva con PostgreSQL y prefijo /deniscorp/logistica-transporte/.


## 0.4.0 — 18/11/2026

- PostgreSQL/Npgsql sustituye SQL Server; migraciones y snapshot propios, historial SQL Server conservado sin compilar.
- Base local nueva: el responsable confirmó que SQL Server no contiene datos pendientes de transferencia.
- Roles separados, migración explícita de mantenimiento y configuración privada fuera de Git/publicación.
- 139 controles HTTP existentes y 17 verificaciones PostgreSQL aprobadas. Inicio, login y blog locales HTTP 200.
- Preparación para futuro NAS; no hay despliegue físico del DS223j ni cambios en Linode.

## 0.3.0

Interfaz corporativa, localización, temas, navegación y guías. Véase docs/ACTUALIZACION-0.3.0.md.
