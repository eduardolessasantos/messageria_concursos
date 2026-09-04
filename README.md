# ConcursosTI - Pipeline de Mensageria & Notificação (Padrão NotificaFlow)

Sistema desacoplado e resiliente para coleta de editais de concursos públicos focados na área de **Tecnologia da Informação (TI)**, publicação de eventos de domínio em broker assíncrono (**RabbitMQ** via **MassTransit 8.x**), persistência relacional com controle de deduplicação, envio real de e-mails transacionais com templates HTML modernos via **Resend** e painel web interativo em tempo real construído com **Blazor Server** e **MudBlazor**.

---

## 🏛️ Arquitetura

```
[ Navegador Web ]
       |
       v (http://localhost:5001)
[ Concurso.Web (Blazor + MudBlazor) ] --(HTTP /api/concursos)--> [ Concurso.Api ] -> SQLite (concursos.db)
       ^                                                                |
       | (Consome fila concurso-web-queue)                              v (Publica ConcursoPublicadoEvent)
       |                                                           [ RabbitMQ ]
       |                                                           /          \
       |--- NotificacaoEnviadaEvent <-----------------------------+            \
                                                                 |              \
                                                                 v               v
                                                    [ Concurso.Notification ] [ Concurso.Consumer ]
                                                    (Passo 1/2/3)             (Persistência com Deduplicação)
                                                    [ IEmailSender ]
                                                    (Resend / Mailpit)
```

---

## 📦 Projetos da Solução

- **`Concurso.Web`**: Painel visual com **MudBlazor**, timeline ao vivo via SignalR, tabela de editais e botões de disparo/coleta imediata.
- **`Concurso.Api`**: Minimal API com Swagger interativo ([http://localhost:5000](http://localhost:5000)) para consulta e publicação de eventos.
- **`Concurso.Notification`**: Worker de e-mail nos moldes do `Worker.Email` do NotificaFlow, com `IEmailSender` (Resend e Mailpit), template HTML responsivo estilizado e controle de retry exponencial.
- **`Concurso.Consumer`**: Worker consumidor responsável pela persistência em SQLite com deduplicação por chave hash.
- **`Concurso.Producer`**: Background Worker com HttpClient Polly resiliente que realiza o parsing de editais de TI e publica novos concursos no RabbitMQ.
- **`Concurso.Messaging`**: Contratos de eventos e envelopes (`IEvent`, `ConcursoPublicadoEvent`, `NotificacaoEnviadaEvent`).
- **`Concurso.Shared`**: Health checks, métricas e opções de infraestrutura compartilhadas.

---

## ⚡ Execução Rápida (Comando Único)

Para subir todo o ambiente de uma só vez (Docker, API, Worker de E-mail, Worker de Banco e Painel Web), basta executar no PowerShell:

```powershell
.\run-all.ps1
```

Esse script irá:
1. Subir o **RabbitMQ** e o **Mailpit** no Docker.
2. Iniciar a **API** (`http://localhost:5000`).
3. Iniciar o **Worker de E-mail** (`Concurso.Notification`).
4. Iniciar o **Worker de Banco de Dados** (`Concurso.Consumer`).
5. Iniciar o **Painel Web** (`Concurso.Web`) e abrir automaticamente no navegador em **[http://localhost:5001](http://localhost:5001)**!

Para encerrar todos os serviços de uma só vez:
```powershell
.\stop-all.ps1
```

### 🧪 Testes Automatizados e Validação E2E

Para executar a suíte automatizada de testes de integração, verificação de infraestrutura Docker, mensageria e endpoints:
```powershell
.\test-pipeline.ps1
```

Consulte a [Documentação Técnica Completa](DOCUMENTACAO_TECNICA.md) para detalhes da arquitetura, especificações de logs e fluxo de coleta de editais de TI.

---

## 🖥️ Interfaces de Acesso e Monitoramento

| Interface | URL | Descrição |
|---|---|---|
| **Painel Web (Blazor UI)** | [http://localhost:5001](http://localhost:5001) | Dashboard com timeline ao vivo, tabela de concursos de TI e botões operacionais |
| **Swagger UI (Concurso.Api)** | [http://localhost:5000](http://localhost:5000) | Documentação e testes interativos da API REST |
| **RabbitMQ Management** | [http://localhost:15672](http://localhost:15672) | Monitoramento das filas (`guest` / `guest`) |
| **Mailpit (Webmail Local)** | [http://localhost:8025](http://localhost:8025) | Visualização instantânea de e-mails em desenvolvimento |

---

## 📧 Configurando o Provedor de E-mail (Resend)

No projeto `Concurso.Notification`, o provedor padrão é o **Resend** (`"Email:Provider": "Resend"`).

Para configurar sua chave de API e o seu e-mail de destino de forma segura:

```bash
cd Concurso.Notification

# 1. Defina a sua API Key gerada no Resend (https://resend.com/api-keys)
dotnet user-secrets set "Resend:ApiKey" "re_sua_chave_aqui"

# 2. Defina o seu e-mail cadastrado na conta do Resend
dotnet user-secrets set "Email:To" "seu_email_cadastrado@dominio.com"
```

*(Para testes offline no Mailpit local sem gastar cota, basta definir `"Email:Provider": "Mailpit"` no `Concurso.Notification/appsettings.json` ou via User Secrets).*
