# 🤖 FinanceBot

Assistente financeiro pessoal integrado ao **Telegram**, desenvolvido com **.NET Core**. O sistema permite o registro agil de despesas e receitas, utilizando **PostgreSQL** para persistencia e um worker em **long polling** para receber mensagens do bot.

---

## 🚀 Como Testar (Comandos)

Interaja com o bot pelo Telegram utilizando os padrões abaixo:

### 1. Registrar Gastos (Saídas)
Digite a descrição seguida do valor.
* **Exemplo:** `Almoço 35.50`
* **Exemplo:** `Uber 15`

### 2. Registrar Receitas (Ganhos)
Use os prefixos `+`, `ganho` ou `receita`. Adicione "fixo" ao final para receitas recorrentes.
* **Variável:** `+ Venda OLX 200`
* **Fixa:** `+ Salário 5000 fixo`

### 3. Comandos de Sistema
* **`total`**: Resumo do saldo do dia (Ganhos - Gastos).
* **`listar`**: Mostra os últimos 5 movimentos (📈 e 📉).
* **`desfazer`**: Remove a última ação realizada.
* **`ajuda`**: Exibe o menu de comandos.

---

## 🛠️ Tecnologias e Arquitetura

* **Runtime:** .NET 8/10 (C#)
* **Banco de Dados:** Supabase (PostgreSQL)
* **ORM:** Entity Framework Core
* **Mensageria:** Telegram Bot API
* **Padrão:** Service Layer (Lógica de parsing desacoplada)

---

## ⚙️ Configuração e Execução

### 1. Clonar e Instalar
```bash
git clone https://github.com/seu-usuario/FinanceBot.git
cd FinanceBot
```

### 2. Configurar Secrets (Segurança)
Este projeto utiliza **User Secrets** para proteger as credenciais do banco e o token do bot. Não suba credenciais reais para arquivos versionados.
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SUA_STRING_DE_CONEXAO_AQUI"
dotnet user-secrets set "Telegram:BotToken" "SEU_TOKEN_DO_BOT"
```

### 3. Banco de Dados
```bash
dotnet ef database update
```

### 4. Executar
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

O bot e a API sobem no mesmo processo. Ao iniciar a aplicação, o worker do Telegram começa a consumir atualizações automaticamente por long polling.

---

## 🛡️ Segurança
* Arquivos `appsettings.json` e pastas `bin/obj` estão no `.gitignore`.
* Credenciais sensíveis são gerenciadas via variáveis de ambiente ou Secrets locais.

---

## 🗺️ Roadmap
* [ ] Dashboard Web (Interface Administrativa).
* [ ] Relatórios mensais automáticos.
* [ ] Categorização automática de gastos.
