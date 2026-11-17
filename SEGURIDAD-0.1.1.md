# Logística y Transporte 0.1.1 — correcciones de seguridad

Requiere SDK/runtime ASP.NET Core 10 LTS. Se corrigen SEG-01, SEG-02, SEG-03 y DEP-03 de la auditoría Denis Corp del 17/11/2026. No se desplegó ni se modificaron bases reales.

## Configuración e inicio

La conexión de ejemplo fue retirada de appsettings.json. El propietario confirmó que no era una credencial activa; no se rotó un acceso ni se reescribió el historial compartido. La configuración de desarrollo anterior se conserva en User Secrets, fuera del repositorio/publicación. El identificador del almacén está en el csproj; no es una clave. Para otra máquina usar User Secrets (Development) o ConnectionStrings__DefaultConnection en el entorno privado. Nunca escribir contraseñas en argumentos, documentación o Git. Program falla si falta la conexión.

`./tools/dotnet10.ps1 run --project Logistica_y_transporte.csproj --launch-profile https`

El iniciador busca SDK 10 instalado, DOTNET10_ROOT o una pista local ignorada en .verification-tools/dotnet.json. Esta máquina reutiliza el SDK oficial aislado en ZUNI; en otra debe instalarse .NET 10. Visual Studio también debe resolverlo. No se modificó la instalación global del IDE.

## Acceso a Personas

Por defecto se exige sesión. Personas exige además un permiso específico por acción: Personas.Leer, Personas.Crear, Personas.Editar o Personas.Eliminar, bajo el tipo de claim logistica:permiso. Una cuenta registrada no recibe estos permisos automáticamente.

Sin Personas.Todos, el servidor limita listados, detalles, edición y eliminación al UserId de la sesión. Los IDs ajenos responden 404. La creación toma siempre el propietario de la sesión y los formularios no permiten reasignarlo. Ni un campo UserId ni Inactivo inyectado modifica esos atributos. El permiso global requiere también el permiso de la acción. Las escrituras conservan antiforgery.

La herramienta local concede/revoca administración completa SOLO de una cuenta ya existente, identificada y confirmada explícitamente:

```
./tools/dotnet10.ps1 run --project Logistica_y_transporte.csproj -- --administrar-personas --usuario-id <ID_EXISTENTE> --confirmar-usuario <MISMO_ID> --accion conceder
```

Usar --accion revocar para retirar los permisos. El operador debe tener acceso autorizado a la conexión privada de su propia base. No se crean administradores al arrancar ni se concede nada por correo/rol enviado en un formulario. El comando actualiza SecurityStamp; cada petición revalida la sesión. No se otorgaron permisos a ninguna cuenta real durante esta corrección. Hasta concederlos explícitamente, las cuentas existentes tampoco entran a Personas.

## Pruebas y publicación

`./tools/dotnet10.ps1 run --project tests/Seguridad.Offline -c Release`

52 comprobaciones HTTP usando el Program real, proveedor SQLite temporal y usuarios ficticios: anónimo, cuenta sin permisos, lector, propietario, alcance global, sobreescritura, CSRF y comando de permisos. SQL Server no se abre. El proveedor SQLite/Mvc.Testing queda únicamente en el proyecto de pruebas.

El runtime de la aplicación conserva solo Identity/UI/SQL Server 10.0.15. Se retiraron SQLite, EF Tools y CodeGeneration no usados. NuGet no reporta vulnerabilidades en el proyecto principal. La publicación linux-arm64 no contiene SQLitePCLRaw, NuGet.Protocol/Packaging ni scaffolding ni secretos. Compila con las cuatro advertencias previas de nombres de migración y nulabilidad; no se reescriben migraciones históricas.

El binario ARM64 NO hace que SQL Server se ejecute de forma soportada en el DS223j: NAS-01 sigue abierto. Hace falta SQL Server en un host compatible o una migración de proveedor. No se ejecutaron imágenes en NAS ni se escaneó su sistema operativo. Los demás hallazgos de autenticación, CSP, idiomas/diseño y despliegue quedan para las siguientes etapas.