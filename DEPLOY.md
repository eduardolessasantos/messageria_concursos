# Guia de Deploy e Orquestração Docker: ConcursosTI

Este documento fornece as instruções para execução em containers Docker locais e publicação em servidores ou plataformas de nuvem (Railway, Render, VPS Linux).

---

## 1. Arquitetura em Containers (`docker-compose.prod.yml`)

O arquivo `docker-compose.prod.yml` orquestra os 5 serviços da aplicação e os containers de infraestrutura:

| Serviço | Porta Externa | Descrição |
|---|---|---|
| **`api`** | `5000` | Minimal API REST com documentação Swagger (`/`) e healthcheck (`/health`) |
| **`web`** | `5001` | Painel Blazor Server + MudBlazor conectado via WebSockets/SignalR |
| **`consumer`** | - | Worker em background para persistência e desduplicação no MySQL |
| **`notification`** | - | Worker para envio de alertas por e-mail (Resend API ou Mailpit) |
| **`producer`** | - | Worker de varredura periódica e publicação de editais de TI |
| **`mysql`** | `3307` | Banco de dados MySQL 8.0 persistido em volume `mysqldata` |
| **`rabbitmq`** | `5672` / `15672` | Broker AMQP com painel de administração web |
| **`mailpit`** | `1025` / `8025` | Webmail de testes locais (habilitado com profile `dev`) |

---

## 2. Execução Local Completa via Docker Compose

### 2.1 Subir Todos os Serviços em Produção (Sem Mailpit)

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

### 2.2 Subir Incluindo o Mailpit para Testes de E-mail Locais

```bash
docker compose -f docker-compose.prod.yml --profile dev up -d --build
```

### 2.3 Verificar o Status e Logs dos Containers

```bash
# Verificar status dos containers
docker compose -f docker-compose.prod.yml ps

# Visualizar logs em tempo real
docker compose -f docker-compose.prod.yml logs -f api web
```

### 2.4 Encerrar o Ambiente

```bash
docker compose -f docker-compose.prod.yml down
```

---

## 3. Configuração de Variáveis de Ambiente

Crie um arquivo `.env` na raiz do projeto (ou configure no painel da sua nuvem):

```env
# Senha do MySQL
MYSQL_ROOT_PASSWORD=sua_senha_segura_aqui

# Usuário e Senha do RabbitMQ (padrão: guest/guest)
RABBITMQ_USER=guest
RABBITMQ_PASS=guest

# Provedor de Notificação: "Resend" ou "Mailpit"
EMAIL_PROVIDER=Resend

# Credenciais Resend (https://resend.com)
RESEND_API_KEY=re_sua_chave_resend_aqui
EMAIL_TO=seu_email_cadastrado@dominio.com
```

---

## 4. Deploy no GitHub Pages (Frontend & Dashboard com Loading)

O frontend estático localizado na pasta `docs/` é publicado automaticamente no **GitHub Pages** a cada `git push` na branch `main` via o workflow `.github/workflows/deploy-pages.yml`.

### 4.1 Comportamento da Página no GitHub Pages
- **Tela de Carregamento Bloqueante com Animação/GIF:** Enquanto os serviços do backend (Render ou local) estão inicializando (*cold start*), a página bloqueia a navegação e exibe um GIF animado e o checklist de saúde em tempo real:
  - 🌐 `Concurso.Api` (Minimal API REST)
  - 🗄️ `Banco de Dados MySQL`
  - 🐰 `Broker RabbitMQ`
  - ⚙️ `Workers (Notificação e Coleta de TI)`
- **Liberação Automática da Navegação:** O script realiza polling no endpoint `/health` a cada 2,5 segundos. Assim que todos os projetos reportam `Healthy`, a tela de loading desaparece com transição suave e o dashboard completo de editais de TI é liberado.
- **Configurador de URL:** O usuário pode alternar facilmente entre a URL do Render em produção (`https://concurso-api.onrender.com`) e o ambiente de desenvolvimento local (`http://localhost:5000`).

### 4.2 Ativando o GitHub Pages no Repositório
1. No seu repositório no GitHub, acesse **Settings** > **Pages**.
2. Em **Build and deployment** > **Source**, selecione **GitHub Actions**.
3. Ao enviar o código (`git push origin main`), o workflow `Deploy GitHub Pages` executará e disponibilizará a URL pública:
   `https://<seu-usuario>.github.io/<seu-repositorio>/`

---

## 5. Deploy no Render (Backend, Workers e Broker)

### 5.1 Por que o Render é necessário? (Confirmação)
**Sim, o deploy no Render (ou plataforma equivalente) é obrigatório.**  
O GitHub Pages é uma plataforma de hospedagem puramente estática (HTML/CSS/JS). Ele **não executa** código .NET em background, não roda containers Docker, não processa filas do RabbitMQ nem gerencia bancos de dados MySQL. Toda a lógica de negócio, persistência, mensageria e workers deve rodar em um servidor ou nuvem como o Render.

### 5.2 Publicação via Blueprint (`render.yaml`)
A solução já contém o arquivo [`render.yaml`](render.yaml) configurado para subir a arquitetura completa:
1. No painel do [Render](https://dashboard.render.com), clique em **New +** > **Blueprint**.
2. Conecte o repositório do projeto. O Render detectará automaticamente o arquivo `render.yaml` e provisionará:
   - **`concurso-api` (Web Service Docker):** API pública com healthcheck configurado em `/health`.
   - **`concurso-consumer` (Background Worker):** Consumidor com persistência no banco.
   - **`concurso-notification` (Background Worker):** Worker de envio de e-mails via Resend.
   - **`concurso-producer` (Background Worker):** Worker de coleta periódica de editais de TI.
3. Configure as variáveis de ambiente sensíveis no painel do Render:
   - `ConnectionStrings__DefaultConnection`: URL do seu banco MySQL (ex: Aiven, Supabase ou Render Database).
   - `RabbitMQ__Host`, `RabbitMQ__Username`, `RabbitMQ__Password`: Credenciais de um broker AMQP gerenciado gratuito (ex: [CloudAMQP](https://www.cloudamqp.com)).
   - `Resend__ApiKey`: Chave de API do provedor Resend.
   - `Email__To`: Seu e-mail de destino.

---

## 6. Endpoints de Verificação e Saúde

- **Health Check da API:** `GET http://localhost:5000/health` (ou `https://<sua-api>.onrender.com/health`)
- **Swagger UI:** `http://localhost:5000/`
- **Painel Blazor Web:** `http://localhost:5001/`
- **GitHub Pages Portal:** `https://<seu-usuario>.github.io/<seu-repositorio>/`
- **RabbitMQ Management:** `http://localhost:15672/` (`guest` / `guest`)
- **Mailpit Webmail (se ativo):** `http://localhost:8025/`

