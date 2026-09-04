using System.Net;
using Concurso.Messaging.Events;

namespace Concurso.Notification.Services;

public static class EmailTemplateBuilder
{
    public static string BuildHtml(ConcursoPublicadoEvent concurso)
    {
        var tituloSafe = WebUtility.HtmlEncode(concurso.Titulo);
        var orgaoSafe = WebUtility.HtmlEncode(concurso.Orgao);
        var cargoSafe = WebUtility.HtmlEncode(concurso.Cargo);
        var salarioSafe = WebUtility.HtmlEncode(concurso.Salario);
        var fonteSafe = WebUtility.HtmlEncode(concurso.Fonte);
        var linkSafe = WebUtility.HtmlEncode(concurso.Link);
        var descricaoSafe = WebUtility.HtmlEncode(concurso.Descricao ?? "Detalhes disponíveis no edital oficial.");
        var dataFormatada = concurso.DataPublicacao.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

        return $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Novo Concurso de TI Detectado: {cargoSafe}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background-color: #0f172a; color: #f8fafc;"">
    <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #0f172a; padding: 30px 15px;"">
        <tr>
            <td align=""center"">
                <!-- Card Container -->
                <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; background-color: #1e293b; border-radius: 12px; overflow: hidden; border: 1px solid #334155; box-shadow: 0 10px 25px rgba(0,0,0,0.5);"">
                    
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #6366f1 0%, #06b6d4 100%); padding: 25px 30px; text-align: left;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 800; letter-spacing: -0.5px;"">
                                Concursos<span style=""color: #38bdf8;"">TI</span>
                            </h1>
                            <p style=""margin: 5px 0 0 0; color: rgba(255,255,255,0.9); font-size: 14px;"">
                                Novo Edital Detectado &bull; Notificação Automática
                            </p>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 30px;"">
                            <h2 style=""margin-top: 0; color: #ffffff; font-size: 20px;"">🚀 Oportunidade de TI Publicada!</h2>
                            <p style=""color: #cbd5e1; font-size: 15px; line-height: 1.6;"">
                                Um novo concurso relevante para a área de Tecnologia foi identificado:
                            </p>

                            <!-- Data Table -->
                            <div style=""margin: 25px 0; background-color: #0f172a; border-radius: 8px; border: 1px solid #334155; overflow: hidden;"">
                                <table role=""presentation"" border=""0"" cellpadding=""10"" cellspacing=""0"" width=""100%"" style=""border-collapse: collapse; font-size: 14px; text-align: left;"">
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; width: 30%; padding: 12px 15px; font-weight: 600;"">Órgão:</td>
                                        <td style=""color: #f8fafc; font-weight: bold; padding: 12px 15px;"">{orgaoSafe}</td>
                                    </tr>
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Cargo:</td>
                                        <td style=""color: #38bdf8; font-weight: bold; padding: 12px 15px;"">{cargoSafe}</td>
                                    </tr>
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Remuneração:</td>
                                        <td style=""color: #10b981; font-weight: bold; font-size: 16px; padding: 12px 15px;"">{salarioSafe}</td>
                                    </tr>
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Fonte:</td>
                                        <td style=""color: #f8fafc; padding: 12px 15px;"">{fonteSafe}</td>
                                    </tr>
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Data:</td>
                                        <td style=""color: #f8fafc; padding: 12px 15px;"">{dataFormatada}</td>
                                    </tr>
                                    <tr style=""border-bottom: 1px solid #1e293b;"">
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Descrição:</td>
                                        <td style=""color: #cbd5e1; font-size: 13px; line-height: 1.4; padding: 12px 15px;"">{descricaoSafe}</td>
                                    </tr>
                                    <tr>
                                        <td style=""color: #94a3b8; padding: 12px 15px; font-weight: 600;"">Chave Única:</td>
                                        <td style=""color: #a855f7; font-family: monospace; font-size: 11px; padding: 12px 15px;"">{concurso.DeduplicationKey}</td>
                                    </tr>
                                </table>
                            </div>

                            <!-- Action Button -->
                            <div style=""text-align: center; margin: 30px 0 20px 0;"">
                                <a href=""{linkSafe}"" target=""_blank"" style=""background: linear-gradient(135deg, #3b82f6 0%, #06b6d4 100%); color: #ffffff; text-decoration: none; padding: 14px 28px; border-radius: 8px; font-weight: 700; font-size: 15px; display: inline-block; box-shadow: 0 4px 15px rgba(6, 182, 212, 0.4);"">
                                    Ver Edital Completo &rarr;
                                </a>
                            </div>

                            <p style=""color: #64748b; font-size: 12px; text-align: center; margin-bottom: 0;"">
                                Evento rastreado via RabbitMQ + MassTransit &bull; EventId: {concurso.EventId}
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #0f172a; padding: 20px 30px; text-align: center; border-top: 1px solid #334155;"">
                            <p style=""margin: 0; color: #64748b; font-size: 12px;"">
                                &copy; {DateTime.UtcNow.Year} Concursos TI &bull; Pipeline Resiliente de Notificações com Resend
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string BuildPlainText(ConcursoPublicadoEvent concurso)
    {
        return $@"NOVO CONCURSO DE TI IDENTIFICADO

Órgão: {concurso.Orgao}
Cargo: {concurso.Cargo}
Remuneração: {concurso.Salario}
Fonte: {concurso.Fonte}
Data de Detecção: {concurso.DataPublicacao.ToLocalTime():dd/MM/yyyy HH:mm:ss}

Descrição:
{concurso.Descricao}

Link Oficial do Edital:
{concurso.Link}

Chave de Deduplicação: {concurso.DeduplicationKey}
EventId: {concurso.EventId}

Equipe Concursos TI - Notificações Automáticas";
    }
}
