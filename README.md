# 🤖 FinanceBot

Assistente financeiro pessoal integrado ao **WhatsApp**, desenvolvido com **.NET Core**. O sistema permite o registro ágil de despesas e receitas, utilizando o **Supabase** (PostgreSQL) para persistência e o **Twilio** para a interface de mensagens.

---

## 🚀 Como Testar (Comandos)

Interaja com o bot pelo WhatsApp utilizando os padrões abaixo:

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
* **Mensageria:** Twilio WhatsApp API
* **Padrão:** Service Layer (Lógica de parsing desacoplada)

---

## ⚙️ Configuração e Execução

### 1. Clonar e Instalar
```bash
git clone https://github.com/seu-usuario/FinanceBot.git
cd FinanceBot
```

### 2. Configurar Secrets (Segurança)
Este projeto utiliza **User Secrets** para proteger as credenciais do Supabase. Não suba o arquivo `appsettings.json` com chaves reais.
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SUA_STRING_DE_CONEXAO_AQUI"
```

### 3. Banco de Dados
```bash
dotnet ef database update
```

### 4. Executar
```bash
dotnet watch run
```

### 5. Webhook (Ngrok)
Exponha a porta local para o Twilio:
```bash
ngrok http http://localhost:5000
```

---

## 🛡️ Segurança
* Arquivos `appsettings.json` e pastas `bin/obj` estão no `.gitignore`.
* Credenciais sensíveis são gerenciadas via variáveis de ambiente ou Secrets locais.

---

## 🗺️ Roadmap
* [ ] Dashboard Web (Interface Administrativa).
* [ ] Relatórios mensais automáticos.
* [ ] Categorização automática de gastos.