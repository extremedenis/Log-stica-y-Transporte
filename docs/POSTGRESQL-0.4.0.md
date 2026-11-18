# Logística 0.4.0 — PostgreSQL

18/11/2026. El usuario confirmó que no existe una base SQL Server con datos que conservar. Esta entrega cambia el proveedor de Entity Framework a Npgsql 10.0.3, crea migraciones PostgreSQL independientes y provisiona una base nueva de desarrollo. No se simula una transferencia de registros inexistentes.

## Inicio local

PostgreSQL 18 en 127.0.0.1:55432, base logistica_dev. El coordinador incluye `scripts/postgres/Preparar-PostgresLocal.ps1` con Estado/Iniciar/Detener. `appsettings.Postgres.local.json` contiene conexiones privadas separadas para logistica_app y logistica_migrador; solo se lee en Development y se excluye de Git y publicación.

1. Iniciar el PostgreSQL local.
2. Desde la raíz del proyecto, con ASPNETCORE_ENVIRONMENT=Development, ejecutar `dotnet run --no-launch-profile -- --migrar-db`. Usa MigrationConnection explícita; el arranque normal no migra.
3. Aplicar deploy/postgres-runtime.sql con el migrador y psql -v logistica_app_role=logistica_app. El usuario de ejecución no tiene DDL ni escritura de historial EF.
4. Abrir Visual Studio y ejecutar el proyecto; en Development lee la configuración privada. `dotnet run --no-launch-profile -- --comprobar-db` verifica conexión y ausencia de migraciones pendientes.

Las migraciones SQL Server históricas quedan conservadas pero excluidas de compilación. Las vigentes son MigrationsPostgres; no aplicar las antiguas a PostgreSQL. Si aparece una base SQL Server con datos posteriormente, detener y preparar una transferencia con verificación antes de usarla: esta entrega no incorpora un importador SQL Server.

## Futuro despliegue NAS

El compose usa imagen 0.4.0 y PostgreSQL provisionado por separado. Configurar ConnectionStrings__DefaultConnection con rol de ejecución y ConnectionStrings__MigrationConnection solo en el proceso de mantenimiento. Remoto requiere SSL Mode=VerifyFull; loopback/socket privado admite la configuración local. Conservar DataProtection__KeysPath y la identidad Denis.Logistica para no invalidar tokens. El prefijo /deniscorp/logistica-transporte sigue configurable. No publicar archivos de configuración local ni el puerto de la base al exterior.

SQL Server ya no es una dependencia del runtime ni se necesita un host x64 para ese motor. La capacidad del NAS, los límites de memoria y la restauración física todavía requieren pruebas; no se declara despliegue del DS223j en esta entrega.

Pruebas: 139 comprobaciones HTTP existentes y pruebas reales de PostgreSQL/Identity/Personas, transacciones, claves foráneas y rol sin DDL. No equivalen a una certificación de todas las funciones logísticas; rutas y entregas aún están en preparación.

Resultado local: esquema aplicado en logistica_dev con logistica_migrador; conexión e historial verificados usando logistica_app. Inicio, login y blog responden HTTP 200 en http://127.0.0.1:5105. Pruebas PostgreSQL: 17 comprobaciones, incluyendo ausencia de diferencias entre modelo y snapshot. El motor permanece en loopback; las configuraciones privadas no se publican. No se modificó Linode ni se activó el NAS.
