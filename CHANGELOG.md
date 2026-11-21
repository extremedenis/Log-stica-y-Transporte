# Historial de Logística

## 1.11.1 — 2026-11-21

- Acceso visible a la configuración de 2FA desde iniciar sesión; conserva la política de autenticador de los módulos protegidos.

## 1.11.0 — 2026-11-20

- Alineación excepcional solicitada: cada programa seguirá aumentando su versión individualmente. POS excluido.
- Identidad fija, menú con desplazamiento independiente, versión y asistente centrados al pie; navegación móvil por permisos.
- Recuperación con regreso, V centrada, ayuda de audio, nuevas portadas y preparación de accesos temporales.

## 0.6.0 — 2026-11-19

- Siete avisos iniciales, repetición desde ajustes y ayuda por cuenta con fechas y tolerancia de escritura.
- Controles minimalistas y cierre de sesión en el home de cada hijo.
- Blog raíz ampliado con piezas clave, incluido POS sin modificar su aplicación.

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
