# Logistica 0.3.0 — interfaz y guías

Cierre documental del 18/11/2026; pruebas locales del 17/11/2026, sin despliegue. POS excluido.

Shell corporativo arena/rojo, modo día/noche, idioma persistente con POST/CSRF/cookie por prefijo y rechazo de retorno externo. Blog de funciones reales; portada sin indicadores ficticios. Formularios, 2FA, errores y validaciones localizados. 139 pruebas HTTP aprobadas con SQLite ficticio. Antes de producción sigue siendo obligatorio SQL externo compatible, TLS y correo real. No hay datos migrados ni conexiones a SQL productivo.

Las pruebas de navegador muestrean cuatro anchos, dos idiomas y dos temas en Edge/Chromium. No certifican todos los roles/correos/PDF ni dispositivos físicos. UI-03 permanece parcial; infraestructura, NAS y publicación conservan las condiciones de la auditoría. No usar el paquete 1.4.0 para desplegar esta entrega.
