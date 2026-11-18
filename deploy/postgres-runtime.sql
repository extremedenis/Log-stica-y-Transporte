-- psql -v logistica_app_role=logistica_app -f postgres-runtime.sql
\set ON_ERROR_STOP on
\if :{?logistica_app_role}
\else
\echo 'Define logistica_app_role'
\quit 3
\endif
BEGIN;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO :"logistica_app_role";
GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO :"logistica_app_role";
REVOKE INSERT,UPDATE,DELETE ON "__EFMigrationsHistory" FROM :"logistica_app_role";
GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO :"logistica_app_role";
COMMIT;
