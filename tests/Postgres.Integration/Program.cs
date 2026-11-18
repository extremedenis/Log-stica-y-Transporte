using System.Security.Claims;
using Logistica_y_transporte.Models;
using Logistica_y_transporte.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if(Environment.GetEnvironmentVariable("DENIS_PG_TEST_PORT")!="57420")throw new Exception("Only the explicit local PostgreSQL lab is accepted.");
var suffix=Guid.NewGuid().ToString("N")[..10];
var database="logistica_migracion_prueba_"+suffix;var runtime="logistica_app_prueba_"+suffix;
var connection=new NpgsqlConnectionStringBuilder{Host="127.0.0.1",Port=57420,Database="postgres",Username="auditoria",Pooling=false};
await using var admin=new NpgsqlConnection(connection.ConnectionString);await admin.OpenAsync();
async Task<object?> Command(string text){await using var cmd=new NpgsqlCommand(text,admin);return await cmd.ExecuteScalarAsync();}
var data=(string)(await Command("SHOW data_directory"))!;
if(!data.Replace('\\','/').EndsWith("/.visual-check/remediacion-01-06/pg-upgrade/data"))throw new Exception("Unknown laboratory.");
var created=false;var roleCreated=false;var checks=0;
void Check(bool condition){if(!condition)throw new Exception("PostgreSQL fixture check failed.");checks++;}
try{
 await Command($"CREATE DATABASE \"{database}\"");created=true;
 connection.Database=database;
 var options=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
 await using(var db=new AppDbContext(options)){
  await db.GetService<IMigrator>().MigrateAsync("20261118182214_PostgreSqlInicial");
  await db.Database.ExecuteSqlRawAsync("INSERT INTO \"AspNetUsers\"(\"Id\",\"AccessFailedCount\",\"EmailConfirmed\",\"LockoutEnabled\",\"PhoneNumberConfirmed\",\"TwoFactorEnabled\") VALUES('migration-guard',0,false,false,false,false); INSERT INTO \"AspNetUserTokens\"(\"UserId\",\"LoginProvider\",\"Name\",\"Value\") VALUES('migration-guard','fixture',repeat('x',129),'preserve-me')");
  try{await db.Database.MigrateAsync();throw new Exception("Long key silently truncated");}catch(PostgresException error){Check(error.SqlState=="P0001");}
  await using(var probe=new NpgsqlConnection(connection.ConnectionString)){
   await probe.OpenAsync();await using var value=new NpgsqlCommand("SELECT length(\"Name\") FROM \"AspNetUserTokens\" WHERE \"UserId\"='migration-guard'",probe);Check((int)(await value.ExecuteScalarAsync())! ==129);
  }
  Check((await db.Database.GetAppliedMigrationsAsync()).Count()==1);
  await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUserTokens\" WHERE \"UserId\"='migration-guard'; DELETE FROM \"AspNetUsers\" WHERE \"Id\"='migration-guard'");
  await db.Database.MigrateAsync();Check(!(await db.Database.GetPendingMigrationsAsync()).Any());Check(!db.Database.HasPendingModelChanges());
  await db.Database.MigrateAsync();Check((await db.Database.GetAppliedMigrationsAsync()).Count()==2);
 }
 var services=new ServiceCollection();services.AddLogging();services.AddDataProtection().UseEphemeralDataProtectionProvider();
 services.AddDbContext<AppDbContext>(o=>o.UseNpgsql(connection.ConnectionString));
 services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
 await using(var provider=services.BuildServiceProvider()){
  using var scope=provider.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
  var user=new IdentityUser{Id="fixture-a",UserName="first@fixture.local",Email="first@fixture.local",EmailConfirmed=true};
  var other=new IdentityUser{Id="fixture-b",UserName="second@fixture.local",Email="second@fixture.local",EmailConfirmed=true};
  Check((await manager.CreateAsync(user,"Fixture123!")).Succeeded);Check((await manager.CreateAsync(other,"Fixture123!")).Succeeded);
  Check(await manager.CheckPasswordAsync(user,"Fixture123!"));Check(!await manager.CheckPasswordAsync(user,"invalid"));
  Check((await manager.FindByNameAsync("FIRST@FIXTURE.LOCAL"))?.Id==user.Id);
  var token=await manager.GeneratePasswordResetTokenAsync(user);Check((await manager.ResetPasswordAsync(user,token,"Changed456!")).Succeeded);Check(await manager.CheckPasswordAsync(user,"Changed456!"));
  await manager.SetLockoutEnabledAsync(user,true);for(var i=0;i<5;i++)await manager.AccessFailedAsync(user);Check(await manager.IsLockedOutAsync(user));
  db.Personas.AddRange(new Persona{personaId=Guid.NewGuid(),Nombre="Fixture A",UserId=user.Id},new Persona{personaId=Guid.NewGuid(),Nombre="Fixture B",UserId=other.Id});await db.SaveChangesAsync();
  var controller=new PersonasController(db){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id)],"fixture"))}}};
  var view=(ViewResult)await controller.Index();Check(((IEnumerable<Persona>)view.Model!).Single().UserId==user.Id);
  var stranger=await db.Personas.AsNoTracking().SingleAsync(p=>p.UserId==other.Id);Check(await controller.Details(stranger.personaId) is NotFoundResult);
  await using(var transaction=await db.Database.BeginTransactionAsync()){db.Personas.Add(new Persona{personaId=Guid.NewGuid(),Nombre="Rollback",UserId=user.Id});await db.SaveChangesAsync();await transaction.RollbackAsync();}
  Check(await db.Personas.CountAsync()==2);
  db.ChangeTracker.Clear();db.Personas.Add(new Persona{personaId=Guid.NewGuid(),UserId="missing"});try{await db.SaveChangesAsync();throw new Exception("FK accepted orphan.");}catch(DbUpdateException error){Check(error.InnerException is PostgresException{SqlState:"23503"});}db.ChangeTracker.Clear();
 }
 await Command($"CREATE ROLE \"{runtime}\" LOGIN");roleCreated=true;
 await using(var db=new AppDbContext(options))await db.Database.ExecuteSqlRawAsync($"REVOKE ALL ON SCHEMA public FROM PUBLIC; GRANT USAGE ON SCHEMA public TO \"{runtime}\"; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO \"{runtime}\"; REVOKE INSERT,UPDATE,DELETE ON \"__EFMigrationsHistory\" FROM \"{runtime}\"; GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO \"{runtime}\"");
 connection.Username=runtime;await using(var limited=new NpgsqlConnection(connection.ConnectionString)){await limited.OpenAsync();await using var read=new NpgsqlCommand("SELECT count(*) FROM \"Personas\"",limited);Check((long)(await read.ExecuteScalarAsync())! ==2);await using var ddl=new NpgsqlCommand("CREATE TABLE forbidden(id integer)",limited);try{await ddl.ExecuteNonQueryAsync();throw new Exception("Runtime DDL permitted.");}catch(PostgresException error){Check(error.SqlState=="42501");}}
 Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{checks,postgresql=true,identity=true,scopeIsolation=true,atomicRollback=true,runtimeDdlDenied=true,fixtureOnly=true}));
}finally{
 if(created)await Command($"DROP DATABASE \"{database}\"");
 if(roleCreated)await Command($"DROP ROLE \"{runtime}\"");
}
