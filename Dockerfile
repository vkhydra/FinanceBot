FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FinanceBot.sln ./
COPY FinanceBot.Domain/FinanceBot.Domain.csproj FinanceBot.Domain/
COPY FinanceBot.Application/FinanceBot.Application.csproj FinanceBot.Application/
COPY FinanceBot.Infrastructure/FinanceBot.Infrastructure.csproj FinanceBot.Infrastructure/
COPY FinanceBot.Api/FinanceBot.Api.csproj FinanceBot.Api/
COPY FinanceBot.TelegramWorker/FinanceBot.TelegramWorker.csproj FinanceBot.TelegramWorker/
COPY FinanceBot.Api.Tests/FinanceBot.Api.Tests.csproj FinanceBot.Api.Tests/

RUN dotnet restore FinanceBot.sln

COPY . .

RUN dotnet publish FinanceBot.Api/FinanceBot.Api.csproj -c Release -o /app/api /p:UseAppHost=false
RUN dotnet publish FinanceBot.TelegramWorker/FinanceBot.TelegramWorker.csproj -c Release -o /app/worker /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/api ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FinanceBot.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app

COPY --from=build /app/worker ./

ENTRYPOINT ["dotnet", "FinanceBot.TelegramWorker.dll"]

FROM node:22-alpine AS web-deps
WORKDIR /app

COPY financebot-web/package.json financebot-web/package-lock.json ./
RUN npm ci

FROM node:22-alpine AS web-build
WORKDIR /app

ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=web-deps /app/node_modules ./node_modules
COPY financebot-web ./

RUN npm run build

FROM node:22-alpine AS web
WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

COPY --from=web-build /app/public ./public
COPY --from=web-build /app/.next/standalone ./
COPY --from=web-build /app/.next/static ./.next/static

EXPOSE 3000

ENTRYPOINT ["node", "server.js"]
