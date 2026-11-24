
using GamesIT.Api.Servicios;
using MailKit;
using MailKit.Security;
using MimeKit;
using System.Net;
using System.Text;

// Evitamos confundir el cliente de MailKit
// con System.Net.Mail.SmtpClient.
using ClienteSmtp = MailKit.Net.Smtp.SmtpClient;

namespace GamesIT.Api.Servicios;

public class ServicioCorreoSmtp
{
    private readonly IConfiguration _config;
    private readonly ILogger<ServicioCorreoSmtp> _logger;

    public ServicioCorreoSmtp(
        IConfiguration config,
        ILogger<ServicioCorreoSmtp> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarCodigoAsync(
        string destino,
        string codigo)
    {
        // ==================================
        // CONFIGURACIÓN
        // ==================================

        string host = _config["Correo:Host"]
            ?? throw new InvalidOperationException(
                "Falta Correo:Host.");

        string remitente = _config["Correo:Remitente"]
            ?? throw new InvalidOperationException(
                "Falta Correo:Remitente.");

        string usuario = _config["Correo:Usuario"]
            ?? throw new InvalidOperationException(
                "Falta Correo:Usuario.");

        string claveConfigurada = _config["Correo:Clave"]
            ?? throw new InvalidOperationException(
                "Falta Correo:Clave.");

        string clave = string.Concat(
            claveConfigurada.Where(
                c => !char.IsWhiteSpace(c))
        );

        if (clave.Length != 16)
        {
            throw new InvalidOperationException(
                "La contraseña de aplicación de Gmail debe tener 16 caracteres."
            );
        }

        int puerto = int.TryParse(
            _config["Correo:Puerto"],
            out int valor)
                ? valor
                : 587;

        // ==================================
        // VALIDACIONES
        // ==================================

        if (codigo.Length != 6 ||
            !codigo.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "El código debe contener seis dígitos.",
                nameof(codigo)
            );
        }

        // ==================================
        // CONTENIDO DE TEXTO
        // ==================================

        string contenidoTexto = $"""
            GAMESIT - Aprendé jugando

            Verificá tu correo para crear tu cuenta.

            Tu código es: {codigo}

            Vence en 10 minutos.
            No lo compartas con nadie.

            Si no solicitaste esta cuenta,
            ignorá este mensaje.

            Equipo GamesIT
            """;

        // ==================================
        // CONTENIDO HTML
        // ==================================

        string codigoHtml = WebUtility.HtmlEncode(codigo);

        string contenidoHtml = $$"""
            <!doctype html>
            <html lang="es">
            <head>
                <meta charset="utf-8">
                <meta name="viewport"
                      content="width=device-width,initial-scale=1">
            </head>

            <body style="
                margin:0;
                padding:0;
                background-color:#f3f0fa;
                font-family:Arial,Helvetica,sans-serif;
                color:#1c1650;
            ">

            <table
                role="presentation"
                cellpadding="0"
                cellspacing="0"
                width="100%"
                style="background-color:#f3f0fa;"
            >
            <tr>
            <td align="center" style="padding:36px 16px;">

            <table
                role="presentation"
                cellpadding="0"
                cellspacing="0"
                width="100%"
                style="
                    max-width:540px;
                    border-radius:24px;
                    overflow:hidden;
                    background-color:#ffffff;
                "
            >

            <tr>
            <td align="center" style="
                padding:28px 24px;
                background-color:#1c1650;
            ">
                <div style="
                    font-size:30px;
                    font-weight:900;
                    color:#ffffff;
                ">
                    Games<span style="color:#b39aff;">IT</span>
                </div>

                <div style="
                    margin-top:7px;
                    font-size:13px;
                    color:#d5c9ff;
                ">
                    Tu aventura tecnológica comienza acá
                </div>
            </td>
            </tr>

            <tr>
            <td style="padding:36px 30px 16px;">

                <div style="
                    font-size:13px;
                    font-weight:700;
                    color:#6f53c5;
                ">
                    SEGURIDAD DE LA CUENTA
                </div>

                <h1 style="
                    margin:12px 0;
                    font-size:28px;
                    color:#1c1650;
                ">
                    ¡Verificá tu correo!
                </h1>

                <p style="
                    font-size:16px;
                    line-height:1.6;
                    color:#4b456d;
                ">
                    Ingresá este código de seis dígitos
                    para completar la creación de tu
                    cuenta en GamesIT.
                </p>

            </td>
            </tr>

            <tr>
            <td align="center" style="padding:10px 30px 16px;">

                <div style="
                    padding:22px 8px;
                    border:2px dashed #b39aff;
                    border-radius:18px;
                    background-color:#f4efff;
                ">

                    <div style="
                        font-size:38px;
                        letter-spacing:8px;
                        font-weight:900;
                        color:#1c1650;
                    ">
                        {{codigoHtml}}
                    </div>

                    <div style="
                        margin-top:10px;
                        font-size:13px;
                        color:#6b608d;
                    ">
                        Válido durante 10 minutos
                    </div>

                </div>

            </td>
            </tr>

            <tr>
            <td style="padding:8px 30px 36px;">

                <p style="
                    font-size:14px;
                    line-height:1.6;
                    color:#4b456d;
                ">
                    Nunca compartas este código.
                    El equipo de GamesIT no te lo
                    pedirá por mensajes ni por correo.
                </p>

                <p style="
                    font-size:13px;
                    line-height:1.6;
                    color:#6b608d;
                ">
                    Si no solicitaste crear una cuenta,
                    podés ignorar este mensaje.
                </p>

            </td>
            </tr>

            <tr>
            <td align="center" style="
                padding:20px 24px;
                background-color:#f6f4fc;
                border-top:1px solid #e9e3f4;
            ">

                <div style="
                    font-weight:700;
                    font-size:13px;
                    color:#1c1650;
                ">
                    GamesIT · Aprendé jugando
                </div>

                <div style="
                    margin-top:5px;
                    font-size:12px;
                    color:#796e98;
                ">
                    Mensaje automático de verificación
                </div>

            </td>
            </tr>

            </table>
            </td>
            </tr>
            </table>

            </body>
            </html>
            """;

        // ==================================
        // CONSTRUIR MENSAJE MIME
        // ==================================

        var mensaje = new MimeMessage();

        mensaje.From.Add(
            new MailboxAddress("GamesIT", remitente)
        );

        mensaje.To.Add(
            MailboxAddress.Parse(destino)
        );

        mensaje.Subject =
            "GamesIT · Tu código de verificación";

        var cuerpo = new BodyBuilder
        {
            TextBody = contenidoTexto,
            HtmlBody = contenidoHtml
        };

        mensaje.Body = cuerpo.ToMessageBody();

        // ==================================
        // CONFIGURACIÓN TLS
        // ==================================

        // Puerto 587: STARTTLS.
        // Puerto 465: TLS desde la conexión.

        SecureSocketOptions seguridad =
            puerto == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

        // ==================================
        // ENVÍO CON MAILKIT
        // ==================================

        using var smtp = new ClienteSmtp();

        smtp.Timeout = 30000;

        string fase = "conexión";

        try
        {
            _logger.LogInformation(
                "GamesIT SMTP: conectando con {Host}:{Puerto}.",
                host,
                puerto
            );

            await smtp.ConnectAsync(
                host,
                puerto,
                seguridad
            );

            // ==================================
            // AUTENTICACIÓN
            // ==================================

            fase = "autenticación";

            _logger.LogInformation(
                "GamesIT SMTP: conexión establecida. Autenticando."
            );

            await smtp.AuthenticateAsync(
                usuario,
                clave
            );

            // ==================================
            // ENVIAR CORREO
            // ==================================

            fase = "envío";

            _logger.LogInformation(
                "GamesIT SMTP: autenticado. Enviando mensaje."
            );

            await smtp.SendAsync(mensaje);

            _logger.LogInformation(
                "GamesIT SMTP: mensaje aceptado por el servidor."
            );

            // ==================================
            // CERRAR CONEXIÓN
            // ==================================

            fase = "desconexión";

            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "GamesIT SMTP: fallo durante la fase de {Fase}.",
                fase
            );

            throw;
        }
    }
}
