using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using GamesIT.Api.Datos;
using GamesIT.Api.Modelos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Servicios;

// Solo se devuelve a la API un identificador y los datos públicos.
// La palabra secreta queda persistida en HistorialDesafiosAhorcado.
public sealed record DesafioAhorcadoGenerado(Guid PartidaId, string Pista, int Longitud);

public sealed class ServicioGeneracionIA
{
    private const int MaximoIntentosIA = 3;
    private const int MaximoExclusionesEnPrompt = 40;
    private const string IndiceUnicoHistorial =
        "UX_HistorialAhorcado_Jugador_Palabra";

    private static readonly string[] Tematicas =
    [
        "redes informáticas y protocolos",
        "bases de datos y SQL",
        "seguridad informática",
        "sistemas operativos",
        "hardware y arquitectura de computadoras",
        "programación orientada a objetos",
        "desarrollo web",
        "algoritmos y estructuras de datos",
        "ingeniería de software y pruebas",
        "sistemas distribuidos y servicios",
        "almacenamiento y sistemas de archivos",
        "virtualización y computación en la nube"
    ];

    private readonly ContextoGamesIT _db;
    private readonly IHttpClientFactory _fabricaHttp;
    private readonly IConfiguration _configuracion;
    private readonly ILogger<ServicioGeneracionIA> _logger;

    public ServicioGeneracionIA(
        ContextoGamesIT db,
        IHttpClientFactory fabricaHttp,
        IConfiguration configuracion,
        ILogger<ServicioGeneracionIA> logger)
    {
        _db = db;
        _fabricaHttp = fabricaHttp;
        _configuracion = configuracion;
        _logger = logger;
    }

    // usuarioId proviene de la cookie de Identity, NO del cuerpo de la petición.
    public async Task<DesafioAhorcadoGenerado> GenerarAhorcadoAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        string apiKey = _configuracion["OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException(
                "La clave de OpenRouter no está configurada.");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "La clave de OpenRouter está vacía.");
        }

        string modelo = _configuracion["OpenRouter:Modelo"]
            ?? "openai/gpt-4.1-mini";

        // IMPORTANTE: HistorialDesafioAhorcado.JugadorId referencia
        // Jugadores.Id; NO referencia AspNetUsers.Id.
        Guid jugadorId = await _db.Jugadores
            .AsNoTracking()
            .Where(j => j.UsuarioId == usuarioId)
            .Select(j => j.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (jugadorId == Guid.Empty)
        {
            throw new KeyNotFoundException(
                "El usuario autenticado no posee un jugador asociado.");
        }

        int primerTema = Random.Shared.Next(Tematicas.Length);

        for (int intento = 1; intento <= MaximoIntentosIA; intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string tematica = Tematicas[
                (primerTema + intento - 1) % Tematicas.Length];

            // Enviamos al modelo algunas palabras recientes como contexto.
            // La comprobación real no depende del prompt ni de esta lista.
            string[] anteriores = await _db.HistorialDesafiosAhorcado
                .AsNoTracking()
                .Where(h => h.JugadorId == jugadorId)
                .OrderByDescending(h => h.FechaGeneracionUtc)
                .ThenByDescending(h => h.Id)
                .Take(MaximoExclusionesEnPrompt)
                .Select(h => h.PalabraNormalizada)
                .ToArrayAsync(cancellationToken);

            RespuestaInternaIA? desafio = await SolicitarDesafioAsync(
                apiKey,
                modelo,
                tematica,
                anteriores,
                intento,
                cancellationToken);

            if (desafio is null)
            {
                _logger.LogWarning(
                    "Ahorcado: desafío inválido en intento {Intento}.", intento);
                continue;
            }

            // Consulta rápida antes de guardar: evita la mayor parte de
            // colisiones, pero el índice único sigue siendo indispensable.
            bool utilizada = await _db.HistorialDesafiosAhorcado
                .AsNoTracking()
                .AnyAsync(
                    h => h.JugadorId == jugadorId &&
                         h.PalabraNormalizada == desafio.Palabra,
                    cancellationToken);

            if (utilizada)
            {
                _logger.LogInformation(
                    "Ahorcado: palabra repetida para el jugador {JugadorId} " +
                    "en intento {Intento}; se solicitará otra.",
                    jugadorId, intento);
                continue;
            }

            var registro = new HistorialDesafioAhorcado
            {
                Id = Guid.NewGuid(),
                JugadorId = jugadorId,
                PalabraNormalizada = desafio.Palabra,
                Pista = desafio.Pista,
                FechaGeneracionUtc = DateTime.UtcNow
            };

            // Una partida comienza al generarse su desafío.
            // Ambas filas se insertan juntas; si falla alguna, no se
            // entrega al frontend ninguna partida huérfana.
            var partida = new Partida
            {
                Id = Guid.NewGuid(),
                JugadorId = jugadorId,
                TipoJuego = TipoJuegoPartida.Ahorcado,
                Estado = EstadoPartida.EnCurso,
                DesafioAhorcadoId = registro.Id,
                FechaInicioUtc = DateTime.UtcNow,
                LetrasIntentadas = string.Empty,
                CantidadIntentos = 0,
                CantidadErrores = 0
            };
            _db.HistorialDesafiosAhorcado.Add(registro);
            _db.Partidas.Add(partida);

            try
            {
                // Una palabra se devuelve solo si SQL Server la reservó.
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Ahorcado: desafío {DesafioId} registrado para " +
                    "jugador {JugadorId} en intento {Intento}.",
                    registro.Id, jugadorId, intento);

                return new DesafioAhorcadoGenerado(
                    partida.Id, desafio.Pista, desafio.Palabra.Length);
            }
            catch (DbUpdateException ex) when (EsDuplicadoDelHistorial(ex))
            {
                // Dos solicitudes concurrentes pueden recibir la misma
                // palabra. SQL Server aceptará una sola gracias al índice.
                // Desvinculamos el insert fallido antes de reintentar.
                _db.Entry(partida).State = EntityState.Detached;
                _db.Entry(registro).State = EntityState.Detached;

                _logger.LogInformation(
                    "Ahorcado: conflicto de palabra ya reservada para " +
                    "jugador {JugadorId} en intento {Intento}.",
                    jugadorId, intento);
            }
        }

        throw new InvalidOperationException(
            "No fue posible obtener un desafío nuevo y válido tras tres intentos.");
    }

    private static bool EsDuplicadoDelHistorial(DbUpdateException ex)
    {
        // 2601: índice UNIQUE; 2627: restricción UNIQUE/PRIMARY KEY.
        // Verificamos también el nombre del índice para no ocultar otros
        // errores de integridad como si fueran palabras repetidas.
        return ex.InnerException is SqlException sql &&
               sql.Errors.Cast<SqlError>().Any(e =>
                   (e.Number == 2601 || e.Number == 2627) &&
                   e.Message.Contains(
                       IndiceUnicoHistorial,
                       StringComparison.OrdinalIgnoreCase));
    }

    private sealed record RespuestaInternaIA(string Palabra, string Pista);

    private async Task<RespuestaInternaIA?> SolicitarDesafioAsync(
        string apiKey,
        string modelo,
        string tematica,
        IReadOnlyCollection<string> palabrasAnteriores,
        int intento,
        CancellationToken cancellationToken)
    {
        const string instrucciones = """
            Sos el generador de desafíos del juego educativo Ahorcado de GamesIT.
            Elegí un concepto REAL de informática o tecnología de la temática pedida.
            Respondé únicamente un objeto JSON con las propiedades "palabra" y "pista",
            ambas de tipo string.

            Reglas:
            - Palabra en mayúsculas, de 4 a 15 letras, sin espacios, números, símbolos ni guiones.
            - Pista educativa en español, precisa y de 12 a 200 caracteres.
            - La pista no debe contener la palabra secreta.
            - Elegí vocabulario variado y no repitas palabras anteriores.
            - No copies ejemplos ni agregues bloques Markdown.
            """;

        string exclusiones = palabrasAnteriores.Count > 0
            ? $"No uses estas palabras ya enviadas al jugador: {string.Join(", ", palabrasAnteriores)}."
            : "El jugador aún no tiene desafíos registrados.";

        string mensajeUsuario =
            $"Generá UN desafío nuevo. Temática: {tematica}. " +
            $"Variación: {Random.Shared.Next(1000, 999999)}. " +
            $"Intento: {intento}. {exclusiones} " +
            "Devolvé solamente el objeto JSON válido.";

        var cuerpo = new
        {
            model = modelo,
            messages = new[]
            {
                new { role = "system", content = instrucciones },
                new { role = "user", content = mensajeUsuario }
            },
            temperature = 0.95,
            max_tokens = 230,
            response_format = new { type = "json_object" }
        };

        HttpClient cliente = _fabricaHttp.CreateClient("OpenRouter");

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, "chat/completions");
        solicitud.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        solicitud.Content = JsonContent.Create(cuerpo);

        using HttpResponseMessage respuesta = await cliente.SendAsync(
            solicitud, cancellationToken);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenRouter devolvió HTTP {(int)respuesta.StatusCode}.",
                null, respuesta.StatusCode);
        }

        try
        {
            using JsonDocument documento = await JsonDocument.ParseAsync(
                await respuesta.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            JsonElement raiz = documento.RootElement;
            if (!raiz.TryGetProperty("choices", out JsonElement choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return null;

            JsonElement primera = choices[0];
            if (!primera.TryGetProperty("message", out JsonElement mensaje) ||
                !mensaje.TryGetProperty("content", out JsonElement contenido) ||
                contenido.ValueKind != JsonValueKind.String)
                return null;

            string? json = contenido.GetString();
            if (string.IsNullOrWhiteSpace(json)) return null;

            using JsonDocument resultado = JsonDocument.Parse(json);
            JsonElement datos = resultado.RootElement;

            if (datos.ValueKind != JsonValueKind.Object ||
                !datos.TryGetProperty("palabra", out JsonElement campoPalabra) ||
                !datos.TryGetProperty("pista", out JsonElement campoPista) ||
                campoPalabra.ValueKind != JsonValueKind.String ||
                campoPista.ValueKind != JsonValueKind.String)
                return null;

            string palabra = Normalizar(campoPalabra.GetString() ?? "");
            string pista = (campoPista.GetString() ?? "").Trim();

            if (!Regex.IsMatch(
                    palabra, "^[A-ZÑ]{4,15}$", RegexOptions.CultureInvariant) ||
                pista.Length is < 12 or > 200 ||
                Normalizar(pista).Contains(palabra, StringComparison.Ordinal))
                return null;

            return new RespuestaInternaIA(palabra, pista);
        }
        catch (JsonException ex)
        {
            // Un JSON mal formado cuenta como intento inválido, no como
            // un desafío que se guarda en el historial.
            _logger.LogWarning(ex,
                "OpenRouter devolvió JSON inválido en el intento {Intento}.",
                intento);
            return null;
        }
    }

    private static string Normalizar(string texto) => texto.Trim()
        .ToUpperInvariant()
        .Replace('Á', 'A')
        .Replace('É', 'E')
        .Replace('Í', 'I')
        .Replace('Ó', 'O')
        .Replace('Ú', 'U')
        .Replace('Ü', 'U');
}
