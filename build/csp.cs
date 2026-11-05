// Añade la política de seguridad de contenido (CSP) al index.html publicado.
//
// GitHub Pages no deja configurar cabeceras HTTP, así que la política va en una etiqueta
// <meta http-equiv="Content-Security-Policy">. Los scripts en línea del index.html (el que
// ajusta la base, el mapa de importación que genera la compilación y el registro del
// service worker) se autorizan uno a uno por su hash SHA-256: cualquier otro script en
// línea, por ejemplo uno inyectado, el navegador no lo ejecuta.
//
// Uso: dotnet run build/csp.cs -- <ruta a index.html> [más archivos .html]

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: dotnet run build/csp.cs -- <index.html> [...]");
    return 1;
}

foreach (var path in args)
{
    var html = File.ReadAllText(path);
    if (html.Contains("Content-Security-Policy", StringComparison.Ordinal))
    {
        Console.WriteLine($"{path}: ya tiene CSP, no se toca.");
        continue;
    }

    // El hash se calcula sobre el texto exacto entre <script> y </script>.
    var hashes = Regex.Matches(html, @"<script(?![^>]*\bsrc=)[^>]*>(.*?)</script>", RegexOptions.Singleline)
        .Select(m => m.Groups[1].Value)
        .Where(body => body.Trim().Length > 0)
        .Select(body => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)))}'")
        .Distinct()
        .ToList();

    string[] policy =
    [
        "default-src 'self'",
        // 'wasm-unsafe-eval' deja compilar WebAssembly (lo necesita .NET) sin permitir eval() de JavaScript.
        $"script-src 'self' 'wasm-unsafe-eval' {string.Join(' ', hashes)}",
        // Los componentes usan atributos style; las hojas externas solo de aquí y de Google Fonts.
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
        "font-src 'self' https://fonts.gstatic.com",
        // data: y blob: para las fotos guardadas y el encuadre; la tienda oficial para las fotos de las miniaturas.
        "img-src 'self' data: blob: https://www.badrollgames.com",
        "media-src 'self' blob:",
        "connect-src 'self'",
        "worker-src 'self'",
        "manifest-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
    ];

    var meta = $"""<meta http-equiv="Content-Security-Policy" content="{string.Join("; ", policy)}" />""";
    var charset = Regex.Match(html, @"<meta charset=[^>]*>");
    if (!charset.Success)
    {
        Console.Error.WriteLine($"{path}: no encuentro <meta charset>, no sé dónde ponerla.");
        return 1;
    }

    // Justo después del charset, antes de cualquier script o estilo.
    html = html.Insert(charset.Index + charset.Length, "\n    " + meta);
    File.WriteAllText(path, html);
    Console.WriteLine($"{path}: CSP añadida ({hashes.Count} scripts en línea autorizados).");
}

return 0;
