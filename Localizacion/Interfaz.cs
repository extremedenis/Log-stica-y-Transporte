using System.Globalization;
namespace Logistica_y_transporte.Localizacion;
public static class Interfaz
{
 public static bool English=>CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en";
 public static string Codigo=>English?"en":"es";
 public static string T(string es,string en)=>English?en:es;

 public static string Required=>T("El campo {0} es obligatorio.","The {0} field is required.");
 public static string Length=>T("El campo {0} debe tener entre {2} y {1} caracteres.","The {0} field must contain between {2} and {1} characters.");
 public static string Mismatch=>T("Las contraseñas no coinciden.","The passwords do not match.");
 public static string Email=>T("Escribe un correo electrónico válido.","Enter a valid email address.");
 public static string Phone=>T("Escribe un número de teléfono válido.","Enter a valid phone number.");

 public static string Error(Microsoft.AspNetCore.Identity.IdentityError error)=>error.Code switch
 {
  "PasswordTooShort"=>T("La contraseña es demasiado corta.","The password is too short."),
  "PasswordRequiresDigit"=>T("La contraseña requiere un número.","The password requires a number."),
  "PasswordRequiresLower"=>T("La contraseña requiere una minúscula.","The password requires a lowercase letter."),
  "PasswordRequiresUpper"=>T("La contraseña requiere una mayúscula.","The password requires an uppercase letter."),
  "PasswordRequiresNonAlphanumeric"=>T("La contraseña requiere un símbolo.","The password requires a symbol."),
  "PasswordRequiresUniqueChars"=>T("La contraseña necesita más caracteres diferentes.","The password needs more distinct characters."),
  "PasswordMismatch"=>T("Contraseña incorrecta.","Incorrect password."),
  "DuplicateEmail" or "DuplicateUserName"=>T("Ya existe una cuenta con esos datos.","An account with those details already exists."),
  "InvalidEmail"=>T("Escribe un correo válido.","Enter a valid email address."),
  "InvalidUserName"=>T("El nombre de usuario no es válido.","The username is invalid."),
  "InvalidToken"=>T("El enlace o código es incorrecto o venció.","The link or code is invalid or expired."),
  "ConcurrencyFailure"=>T("Los datos cambiaron. Recarga antes de continuar.","Data changed. Reload before continuing."),
  _=>T("No se pudo completar la operación. Revisa los datos o consulta al responsable.","The operation could not be completed. Check the details or contact the operator.")
 };
}
