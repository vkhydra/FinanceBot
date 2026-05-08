# 🤖 FinanceBot

Assistente financeiro pessoal integrado ao **Telegram**, desenvolvido com **.NET Core**. O sistema permite o registro agil de despesas e receitas, utilizando **PostgreSQL** para persistencia e uma estrutura separada em **API**, **Application**, **Infrastructure**, **Domain** e **TelegramWorker**.

---

## 🚀 Como Testar (Comandos)

Interaja com o bot pelo Telegram utilizando os padrões abaixo:

### 1. Registrar Gastos (Saídas)
Digite a descrição seguida do valor.
* **Exemplo:** `Almoço 35.50`
* **Exemplo:** `Uber 15`
* O sistema tenta classificar automaticamente o gasto em categorias como `Alimentacao`, `Transporte`, `Mercado` e `Moradia`.

### 2. Registrar Receitas (Ganhos)
Use os prefixos `+`, `ganho` ou `receita`. Adicione "fixo" ao final para receitas recorrentes.
* **Variável:** `+ Venda OLX 200`
* **Fixa:** `+ Salário 5000 fixo`

### 3. Comandos de Sistema
* **`total`** ou **`resumo`**: Resumo do saldo do dia (Ganhos - Gastos).
* **`listar`** ou **`movimentos`**: Mostra os últimos 5 movimentos (📈 e 📉).
* **`relatorio`**: Mostra o relatório mensal consolidado (**Premium/trial**).
* **`plano`** ou **`status`**: Mostra o plano atual e a quota do mês.
* **`upgrade`** ou **`assinar`**: Registra o pedido de upgrade para o Premium.
* **`desfazer`**: Remove a última ação realizada.
* **`desvincular`**: Remove o vínculo deste chat com a sua conta.
* **`ajuda`**: Exibe o menu de comandos.

### 4. Vínculo com Telegram
Antes de registrar lançamentos no bot, o chat precisa estar vinculado a uma conta:
1. Faça `register` ou `login` na API.
2. Chame `POST /auth/gerar-vinculo` autenticado.
3. Envie ao bot: `/vincular 123456`

### 5. Plano Free atual
- O plano **Free** permite **50 lançamentos por mês** por usuário.
- Novos usuários recebem um **trial Premium inicial** por padrão.
- Quando o limite é atingido, a API e o bot bloqueiam novos lançamentos até existir upgrade de plano.
- O status atual do acesso pode ser consultado em `GET /api/billing/status`.

---

## 🛠️ Tecnologias e Arquitetura

* **Runtime:** .NET 8/10 (C#)
* **Banco de Dados:** Supabase (PostgreSQL)
* **ORM:** Entity Framework Core
* **Mensageria:** Telegram Bot API
* **Padrão:** Clean Architecture

---

## ⚙️ Configuração e Execução

### 1. Clonar e Instalar
```bash
git clone https://github.com/seu-usuario/FinanceBot.git
cd FinanceBot
```

### 2. Configurar Secrets (Segurança)
Este projeto utiliza **User Secrets** para proteger as credenciais do banco, a chave JWT e o token do bot. Não suba credenciais reais para arquivos versionados.
```bash
dotnet user-secrets init
dotnet user-secrets set "Telegram:BotToken" "SEU_TOKEN_DO_BOT"
dotnet user-secrets set "Jwt:Key" "UMA_CHAVE_LONGA_E_SECRETA"
```

### 3. Banco de Dados
Suba um PostgreSQL local para desenvolvimento:
```bash
docker compose up -d postgres
```

Depois aponte a aplicação para esse banco:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=financebot;Username=financebot;Password=financebot" --project FinanceBot.Api
```

Como `FinanceBot.Api` e `FinanceBot.TelegramWorker` compartilham o mesmo `UserSecretsId`, esse comando já atende os dois projetos.

Em seguida, aplique as migrations:
```bash
dotnet ef database update --project FinanceBot.Infrastructure --startup-project FinanceBot.Api
```

> **Importante:** a migration de identidade recria as tabelas financeiras atuais para estabelecer o modelo multiusuário. Use em uma base considerada limpa.

### 4. Executar a API
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project FinanceBot.Api
```

Swagger fica disponivel em:
```text
http://localhost:5179/swagger
```

Fluxos iniciais da API:
- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/gerar-vinculo` (Bearer token)
- `POST /auth/desvincular` (Bearer token)
- `GET /api/billing/status` (Bearer token)
- `POST /api/billing/solicitar-upgrade` (Bearer token)
- `GET /api/relatorios/mensal` (Bearer token, Premium/trial)

### 5. Executar o Worker do Telegram
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project FinanceBot.TelegramWorker
```

A API e o bot agora sobem em processos separados, mas consomem a mesma camada de aplicação e a mesma infraestrutura.

### 6. Executar o Frontend Web (Next.js)
O frontend fica em `financebot-web/` e usa **Next.js + Tailwind CSS + shadcn/ui**.

Crie o arquivo de ambiente:
```bash
cp financebot-web/.env.example financebot-web/.env.local
```

Em ambiente local HTTP, mantenha `FINANCEBOT_SESSION_COOKIE_SECURE=false` para o cookie de sessao funcionar sem HTTPS.

Depois suba o frontend:
```bash
cd financebot-web
npm install
npm run dev
```

Por padrão, o frontend espera a API em:
```text
http://127.0.0.1:5057
```

Rotas iniciais da Web:
- `/login`
- `/register`
- `/dashboard`
- `/plano`
- `/telegram`

Na interface Web atual:
- o `/dashboard` exibe resumo do dia, ultimos movimentos, status do plano/quota e o relatorio mensal quando o acesso efetivo estiver em Premium/trial;
- o `/plano` concentra a experiencia comercial do upgrade, com estado de trial, pedido pendente, beneficios do Premium e CTA dedicado;
- o `/dashboard` tambem permite registrar o pedido de upgrade para o Premium e refletir o estado pendente desse fluxo;
- o `/telegram` permite gerar um novo codigo de vinculo e tambem disparar a desvinculacao autenticada pela propria interface.

### 7. Executar a stack completa com Docker
Exporte o token do bot e a chave JWT no shell:
```bash
export TELEGRAM_BOT_TOKEN="SEU_TOKEN_DO_BOT"
export JWT_SECRET="UMA_CHAVE_LONGA_E_SECRETA"
```

Depois suba toda a stack:
```bash
docker compose up --build -d
```

Isso sobe:
- PostgreSQL
- FinanceBot.Api
- FinanceBot.TelegramWorker
- FinanceBot.Web

As migrations do banco sao aplicadas automaticamente no startup da API.
No compose local, o frontend sobe com `FINANCEBOT_SESSION_COOKIE_SECURE=false` para permitir login em `http://127.0.0.1:3000`.

Para acompanhar:
```bash
docker compose logs -f api
docker compose logs -f telegram-worker
docker compose logs -f web
```

Interfaces expostas:
- API: `http://127.0.0.1:5057`
- Frontend Web: `http://127.0.0.1:3000`

---

## 🛡️ Segurança
* Arquivos `appsettings.json` e pastas `bin/obj` estão no `.gitignore`.
* Credenciais sensíveis são gerenciadas via variáveis de ambiente ou Secrets locais.

---

## 🗺️ Roadmap
* [ ] Dashboard Web (Interface Administrativa).
* [ ] Relatórios mensais automáticos.
* [ ] Categorização automática de gastos.
