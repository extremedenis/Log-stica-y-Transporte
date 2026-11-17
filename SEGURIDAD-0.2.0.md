# Logística 0.2.0 — acceso y preparación de despliegue

Entrega local del 17/11/2026; no publicada ni conectada a SQL real. Conserva las correcciones 0.1.1.

## Acceso

Cinco contraseñas incorrectas bloquean la cuenta durante 15 minutos. El acceso Identity tiene además 20 peticiones por cliente/minuto y 100 globales/minuto, sin colas. Solo los proxies de confianza pueden informar la IP: configurar `Hosting__KnownProxies__0` con el peer real y sustituir X-Forwarded-For en el gateway. No ampliar la confianza a Internet. Los eventos 7101/7102 y el bloqueo omiten contraseñas y datos de cuenta. Los límites en memoria exigen un único proceso hasta resolver ESC-02.

Personas exige permiso por acción y 2FA activado. Primero ingresar sin permisos, abrir `/Identity/Account/Manage/TwoFactorAuthentication`, configurar autenticador y guardar códigos de recuperación fuera del dispositivo; verificar un código. Después el operador concede permisos con el comando documentado en SEGURIDAD-0.1.1.md. Desactivar 2FA impide acceder a Personas incluso si conserva claims. Cada código de recuperación se consume una sola vez. No se configuró ninguna cuenta real.

Producción exige cuenta confirmada y no publica las páginas de alta/recuperación por correo de demostración mientras no exista remitente real. RegisterConfirmation nunca muestra tokens. Recuperación disponible para MFA: códigos previamente guardados; si se pierden, procedimiento administrativo de identidad verificada, nunca quitar 2FA solo con acceso al login. La cuenta inicial confirmada debe provisionarse por un operador autorizado; no se habilita una autoalta privilegiada.

## URL y cookies

`Hosting__PathBase=/deniscorp/logistica-transporte` o un prefijo minúsculo alternativo sin barra final; vacío en desarrollo raíz. Configurar AllowedHosts con el host de esta aplicación, por ejemplo logistica.zuni.lat. Sesión y antiforgery son host-only, con Path del prefijo y Secure en producción. El gateway conserva el prefijo; no se acepta la raíz fuera de él.

## NAS con base externa

El NAS DS223j ejecutaría solo ASP.NET ARM64. SQL Server permanece en un host x64 soportado, en red privada; no hay emulación ni servicio SQL ARM64 en Compose. `deploy/env.example` es una plantilla sin credenciales reales. Configurar de forma privada la conexión con Encrypt=True o Strict y TrustServerCertificate=False, certificado confiable del servidor, usuario de mínimo privilegio y cortafuegos restringido al cliente. No abrir el puerto SQL a Internet. Configurar/redimensionar el host externo y ensayar latencia/restauración antes de activar.

La imagen se construye con `deploy/Dockerfile`; Compose tiene proceso sin root, filesystem de solo lectura, tmpfs, límites y rotación de logs. El volumen `/keys` debe pertenecer al UID de APP_UID de la imagen, tener permisos restrictivos, respaldo y custodia. Los XML de Data Protection requieren protección del volumen; no se afirma cifrado automático de esos XML. Nunca poner keys ni secretos dentro de la imagen. `--comprobar-db` prueba disponibilidad sin migrar ni inicializar datos y devuelve estado 0/1 genérico; el healthcheck usa ese comando. Respaldo y recuperación de SQL se ejecutan en el host externo con su procedimiento verificado, no con una copia de archivos vivos del NAS.

No se ejecutó Docker Linux ni se configuró el servidor SQL externo. NAS-01 queda preparado, pendiente de host/base, certificado, conexión TLS y ensayo físico. El límite 256 MiB es configuración provisional, no memoria disponible demostrada; NAS-02 sigue abierto. No ejecutar `database update`, EnsureCreated ni semillas sobre una base real sin staging/restauración previos.

## Pruebas

`./tools/dotnet10.ps1 run --project tests/Seguridad.Offline -c Release`: fixtures SQLite, HTTP de Identity real, TOTP válido/inválido, desafío antes de sesión, recuperación de un uso, bloqueo, permisos y dos prefijos. No abre SQL Server. Publicar ARM64 no prueba el SQL externo ni el rendimiento del DS223j.
