# fiapx-identity-service

Serviço de identidade da plataforma **FIAP X**: cadastro e login por usuário e senha, emissão de JWT RS256 e
publicação do JWKS ([ADR-0011](https://github.com/FiapXHacka/fiapx/blob/main/docs/adr/0011-autenticacao-jwt-rs256.md)).
Publica `user.registered` via outbox transacional.

> Parte da plataforma FIAP X: arquitetura, ADRs e contratos ficam em [`FiapXHacka/fiapx`](https://github.com/FiapXHacka/fiapx).

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=FiapXHacka_fiapx-identity-service&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=FiapXHacka_fiapx-identity-service)

## Platform overview

```
Cliente ── API Gateway ── /auth/*, /.well-known/jwks.json ──▶ identity-service (este repo)
                                                                   │ identity_db (users, outbox_messages)
                                                                   └─ outbox relay ── user.registered ──▶ RabbitMQ (fiapx.events)
                                                                                                          └─▶ notification-service
```

## Status

| Issue | Escopo | Situação |
|---|---|---|
| #1 | Scaffold: Clean Architecture, EF Core, outbox + relay, health, OpenTelemetry, CI + Sonar | este PR |
| #2 | `POST /auth/register` | a fazer |
| #3 | `POST /auth/login` + JWKS | a fazer |
| #4 | Relay publicando `user.registered` (a infraestrutura já existe) | a fazer |

## Estrutura

```
src/
  FiapX.Identity.Domain/          entidade User e invariantes (sem dependências)
  FiapX.Identity.Application/     portas (IEventOutbox) e eventos de integração
  FiapX.Identity.Infrastructure/  EF Core (Npgsql, snake_case), migrations, mensageria (CloudEvents, outbox, RabbitMQ)
  FiapX.Identity.Api/             Minimal API, health checks, OpenTelemetry
tests/
  FiapX.Identity.UnitTests/         domínio, envelope CloudEvents (contrato), opções
  FiapX.Identity.IntegrationTests/  API real + Postgres e RabbitMQ via Testcontainers
db/schema.sql                     DDL de referência (igual à migration inicial)
```

### Mensageria (ADR-0006, ADR-0007)

- O evento é gravado em `outbox_messages` **na mesma transação** da mudança de negócio (`IEventOutbox.Enqueue` + `SaveChanges`).
- O `OutboxRelay` publica em ordem, com *publisher confirms*, na exchange topic `fiapx.events` (routing key = tipo do evento).
  As linhas são travadas com `FOR UPDATE SKIP LOCKED`, então várias réplicas podem rodar o relay sem publicar duas vezes.
  Falha no broker: a mensagem continua pendente, `attempts` é incrementado e o relay faz backoff exponencial.
- As exchanges (`fiapx.events`, `fiapx.dlx`) são declaradas de forma idempotente ao abrir o canal.

## Configuração

| Variável | Exemplo | Descrição |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=postgres;Database=identity_db;Username=identity_svc;Password=…` | Obrigatória |
| `RabbitMq__Uri` | `amqp://user:pass@rabbitmq:5672/` | Obrigatória |
| `Outbox__Enabled` / `Outbox__PollInterval` / `Outbox__BatchSize` | `true` / `00:00:01` / `50` | Relay da outbox |
| `Database__MigrateOnStartup` | `true` | Aplica as migrations ao subir (local/compose) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://otel-lgtm:4317` | Liga a exportação OTLP |
| `OTEL_SERVICE_NAME` | `identity-service` | Nome do serviço nos traces |

Endpoints: `GET /health/live` (sem dependências) e `GET /health/ready` (Postgres + RabbitMQ).

## Desenvolvimento

Requisitos: .NET SDK 10 (`global.json`) e Docker (Testcontainers).

```bash
dotnet tool restore                 # dotnet-ef
dotnet build
dotnet test                         # unit + integração (sobe Postgres e RabbitMQ em containers)
dotnet ef migrations add <Nome> -p src/FiapX.Identity.Infrastructure -s src/FiapX.Identity.Infrastructure \
  -o Persistence/Migrations
docker build -t fiapx-identity-service .
```

Para subir a plataforma inteira localmente, use o `docker-compose.yml` do repositório [`fiapx`](https://github.com/FiapXHacka/fiapx).

## CI

`.github/workflows/ci.yml`: build → testes (unit + integração) com cobertura → SonarQube Cloud (análise *secretless* via OIDC)
→ build da imagem. Actions fixadas por SHA; Dependabot atualiza.
