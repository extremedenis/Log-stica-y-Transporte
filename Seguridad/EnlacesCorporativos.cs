namespace Logistica_y_transporte.Seguridad;
public static class EnlacesCorporativos {
 public static string Inicio(IConfiguration config,IHostEnvironment env){var value=config["DenisCorp:HomeUrl"];var fallback=env.IsDevelopment()?"http://localhost:5001/":"https://zuni.lat/";if(!Uri.TryCreate(value,UriKind.Absolute,out var url)||url.UserInfo.Length>0||url.AbsolutePath!="/"||url.Query.Length>0||url.Fragment.Length>0)return fallback;return url.Scheme=="https"||url.Scheme=="http"&&url.IsLoopback?url.AbsoluteUri:fallback;}
}
