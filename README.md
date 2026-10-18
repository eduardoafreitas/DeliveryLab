# DeliveryLab

Laboratório de desenvolvimento backend de uma empresa fictícia de entregas.
A API permite cadastrar, consultar, listar, acompanhar e cancelar entregas, com
persistência em PostgreSQL e validação das transições de status.

O objetivo didático é praticar a investigação de código existente, implementação
de demandas, manutenção, depuração, testes e refatoração. O projeto é
propositalmente pequeno e simples. Novos conceitos e mudanças arquiteturais serão
introduzidos gradualmente por tarefas de treinamento, quando houver necessidade real.

## Funcionalidades implementadas

- Cadastro de entregas com destinatário, origem, destino e previsão de entrega opcional.
- Geração de identificador `Guid` e código de rastreamento único no formato `DL-{Guid sem hífens}`.
- Listagem paginada com filtro opcional por status.
- Consulta por identificador ou código de rastreamento.
- Alteração de status com regras de transição e atualização de `UpdatedAt`.
- Registro automático de `DeliveredAt` quando a entrega é concluída.
- Cancelamento de entregas em `Created` ou `InTransit` por endpoint específico.
- Persistência com EF Core, três migrations e duas entregas fictícias iniciais.
- Documentação OpenAPI com interface Scalar e seis testes automatizados de integração.
- Configuração local da conexão com User Secrets em `Development`.

## Tecnologias e estrutura

.NET 8, C#, ASP.NET Core Web API com controllers, Entity Framework Core 8,
PostgreSQL com Npgsql, OpenAPI com Swashbuckle, Scalar e xUnit. Os testes utilizam
`WebApplicationFactory` e SQLite em memória. A solução contém dois projetos:

```text
DeliveryLab/
├── .config/
│   └── dotnet-tools.json
├── DeliveryLab.Api/
│   ├── Controllers/
│   │   └── DeliveryController.cs
│   ├── Models/
│   │   └── Delivery.cs
│   ├── DTOs/
│   │   ├── CreateDeliveryRequest.cs
│   │   ├── UpdateDeliveryStatusRequest.cs
│   │   └── DeliveryResponse.cs
│   ├── Data/
│   │   ├── DeliveryLabDbContext.cs
│   │   └── Migrations/
│   │       ├── 20260923232857_InitialCreate.cs
│   │       ├── 20260923232857_InitialCreate.Designer.cs
│   │       ├── 20260930040217_AddEstimatedDeliveryDate.cs
│   │       ├── 20260930040217_AddEstimatedDeliveryDate.Designer.cs
│   │       ├── 20261003043122_AddDeliveredAt.cs
│   │       ├── 20261003043122_AddDeliveredAt.Designer.cs
│   │       └── DeliveryLabDbContextModelSnapshot.cs
│   ├── Services/
│   │   └── DeliveryService.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Program.cs
│   ├── appsettings.json
│   └── DeliveryLab.Api.csproj
├── DeliveryLab.Tests/
│   ├── DeliveryApiFactory.cs
│   ├── DeliveryEndpointsTests.cs
│   └── DeliveryLab.Tests.csproj
├── .gitignore
├── DeliveryLab.sln
└── README.md
```

| Arquivo | Responsabilidade |
| --- | --- |
| `Program.cs` | Registrar controllers, DbContext, serviço, OpenAPI e Scalar; mapear as rotas. |
| `DeliveryController.cs` | Receber requisições, chamar o serviço e retornar os códigos HTTP e DTOs. |
| `Delivery.cs` | Representar os dados persistidos de uma entrega. |
| `CreateDeliveryRequest.cs` | Receber os três campos obrigatórios da criação e a previsão de entrega opcional. |
| `UpdateDeliveryStatusRequest.cs` | Receber e validar o status permitido. |
| `DeliveryResponse.cs` | Definir os dados retornados pela API. |
| `DeliveryService.cs` | Criar, consultar, filtrar e paginar entregas; validar transições de status; registrar datas e mapear o DTO de resposta usando diretamente o DbContext. |
| `DeliveryLabDbContext.cs` | Mapear a entidade, configurar o índice único de rastreamento e definir duas entregas iniciais. |
| `Data/Migrations/` | Versionar a tabela, o índice único, os dados iniciais e os campos de previsão e conclusão; registrar o modelo do EF. |
| `appsettings.json` | Manter a chave da conexão sem credenciais e o logging padrão. |
| `launchSettings.json` | Definir a URL HTTP e o ambiente `Development` usados pelo perfil `http`. |
| `DeliveryApiFactory.cs` | Iniciar a API em memória nos testes, substituindo PostgreSQL por SQLite em memória. |
| `DeliveryEndpointsTests.cs` | Exercitar seis cenários de criação, consulta, atualização de status e cancelamento via HTTP. |
| `.config/dotnet-tools.json` | Fixar a ferramenta local `dotnet-ef` na versão 8.0.31. |

## Preparar o PostgreSQL e os User Secrets

Tenha um SDK capaz de compilar `net8.0`, o runtime ASP.NET Core 8 e uma instalação
local do PostgreSQL em execução. O projeto não utiliza Docker.

Use o `psql` ou o pgAdmin para criar ou selecionar o banco e o usuário da sua
instalação. Cadastre a senha localmente, sem incluí-la em scripts versionados.
No `psql`, o comando interativo `\password` permite definir a senha do usuário
conectado sem escrevê-la no comando SQL.

O arquivo `DeliveryLab.Api/appsettings.json` mantém `ConnectionStrings:DeliveryLab`
vazia. A conexão completa deve ser cadastrada nos User Secrets da API, fora do
repositório. O projeto já contém o `UserSecretsId`; não é necessário executar
`dotnet user-secrets init` novamente.

A partir da pasta que contém `DeliveryLab.sln`, execute este bloco no PowerShell.
Quando solicitado, cole a connection string completa da sua instalação, incluindo
host, porta, banco, usuário e senha. A entrada fica oculta e não integra o histórico
de comandos; não compartilhe esse valor no chat.

```powershell
$connectionInput = Read-Host 'Connection string PostgreSQL completa' -AsSecureString
try {
    $localCredential = [System.Net.NetworkCredential]::new('', $connectionInput)
    @{ 'ConnectionStrings:DeliveryLab' = $localCredential.Password } |
        ConvertTo-Json -Compress |
        dotnet user-secrets set --project DeliveryLab.Api
}
finally {
    $connectionInput.Dispose()
    Remove-Variable connectionInput, localCredential -ErrorAction SilentlyContinue
}
```

`WebApplication.CreateBuilder(args)` carrega os User Secrets automaticamente no
ambiente `Development`. O perfil `http` já seleciona esse ambiente. `Program.cs`
continua lendo a mesma chave com `GetConnectionString("DeliveryLab")` e usando
Npgsql, sem mudanças no DbContext ou nos serviços.

Os User Secrets são destinados ao desenvolvimento e armazenados localmente sem
criptografia. Em outros ambientes, forneça a conexão por configuração externa,
como a variável `ConnectionStrings__DeliveryLab`. Variáveis de ambiente têm
precedência sobre User Secrets; confira se há uma definição antiga na sua sessão.

## Restaurar, aplicar as migrations e executar

Após cadastrar a conexão, execute os comandos a partir da pasta que contém `DeliveryLab.sln` (na mesma sessão do PowerShell):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet restore
dotnet tool restore
dotnet build
dotnet ef database update --project DeliveryLab.Api
dotnet run --project DeliveryLab.Api --launch-profile http
```

As migrations incluídas são aplicadas em sequência por `dotnet ef database update`:

| Migration | Alteração |
| --- | --- |
| `20260923232857_InitialCreate` | Cria `Deliveries`, a chave primária, o índice único de `TrackingCode` e os dados iniciais. |
| `20260930040217_AddEstimatedDeliveryDate` | Adiciona `EstimatedDeliveryDate`, opcional, do tipo PostgreSQL `date`. |
| `20261003043122_AddDeliveredAt` | Adiciona `DeliveredAt`, opcional, do tipo PostgreSQL `timestamp with time zone`. |

A migration inicial insere duas entregas fictícias com status `Created`:

| Destinatário | Origem | Destino | Código |
| --- | --- | --- | --- |
| Cliente Teste 1 | São Paulo - SP | Campinas - SP | `DL-11111111111111111111111111111111` |
| Cliente Teste 2 | São José do Rio Preto - SP | Ribeirão Preto - SP | `DL-22222222222222222222222222222222` |

As migrations são aplicadas pelo comando acima, não automaticamente ao iniciar a
API. O seed usa IDs e datas UTC fixas; os campos opcionais das entregas iniciais
permanecem nulos.

Para criar uma migration após uma alteração no modelo, mantenha `ASPNETCORE_ENVIRONMENT=Development` na sessão para carregar os User Secrets:

```powershell
dotnet ef migrations add NomeDaAlteracao --project DeliveryLab.Api --output-dir Data/Migrations
dotnet ef database update --project DeliveryLab.Api
```

## URLs e documentação

- URL base da API: `http://localhost:5080`
- Entregas: `http://localhost:5080/api/deliveries`
- Scalar: `http://localhost:5080/scalar`
- Documento OpenAPI: `http://localhost:5080/openapi/v1.json`

OpenAPI é a especificação/documento que descreve a API: métodos, rotas, parâmetros,
requests, responses e schemas. Scalar utiliza esse documento OpenAPI para fornecer
uma interface visual que permite visualizar e testar os endpoints.

O documento é gerado pelo Swashbuckle e exibido pelo Scalar, usando a
[integração documentada pelo Scalar](https://scalar.com/products/api-references/integrations/aspnetcore/integration).
Não há Swagger UI. Os atributos dos controllers e DTOs alimentam a documentação.
No Scalar, selecione uma operação, preencha os parâmetros e o JSON e execute a chamada.
OpenAPI e Scalar estão mapeados em todos os ambientes, sem condição de `Development`.

## Endpoints

| Método | Rota | Resultado |
| --- | --- | --- |
| POST | `/api/deliveries` | `201` com a entrega e header `Location`; `400` para dados inválidos. |
| GET | `/api/deliveries` | `200` com uma página de entregas, ou `[]` quando não houver resultados; `400` para parâmetros inválidos. |
| GET | `/api/deliveries/{id}` | `200` com a entrega; `404` se não existir. |
| GET | `/api/deliveries/tracking/{trackingCode}` | `200` com a entrega; `404` se não existir. |
| PATCH | `/api/deliveries/{id}/status` | `200` com a entrega atualizada; `400` para status ou transição inválida; `404` se não existir. |
| PATCH | `/api/deliveries/{id}/cancel` | `200` com a entrega cancelada; `400` se o status atual impedir o cancelamento; `404` se não existir. Não recebe corpo. |

Os parâmetros `{id}` usam a restrição de rota `guid`.

### Filtro e paginação

| Parâmetro | Padrão | Comportamento |
| --- | --- | --- |
| `status` | Ausente | Filtra por `Created`, `InTransit`, `Delivered` ou `Cancelled`, com correspondência exata. Ausente, vazio ou composto apenas por espaços não aplica filtro. |
| `page` | `1` | Número da página, maior ou igual a `1`. |
| `pageSize` | `10` | Quantidade de registros por página, maior ou igual a `1`. Não há limite máximo definido no código. |

Exemplo: `GET /api/deliveries?status=InTransit&page=1&pageSize=10`.
A ordenação usa `CreatedAt` decrescente e, em caso de empate, `Id` crescente.
A resposta é um array, sem total de registros ou metadados de paginação.

### Corpos das requisições

Exemplo de corpo do POST:

```json
{
  "recipientName": "Maria Silva",
  "originAddress": "São Paulo - SP",
  "destinationAddress": "Campinas - SP",
  "estimatedDeliveryDate": "2026-11-01"
}
```

`estimatedDeliveryDate` é opcional, aceita `null` e representa uma data sem horário
no formato `yyyy-MM-dd`. Não há regra específica para exigir uma data futura.

Exemplo de corpo de `PATCH /api/deliveries/{id}/status`:

```json
{
  "status": "InTransit"
}
```

`recipientName`, `originAddress` e `destinationAddress` são obrigatórios. Valores
ausentes, nulos, vazios ou contendo apenas espaços retornam `400`. O DTO de status
aceita somente `Created`, `InTransit`, `Delivered` e `Cancelled`, com distinção
entre maiúsculas e minúsculas; o serviço também valida a transição solicitada.

### Resposta de uma entrega

As operações retornam `DeliveryResponse`; a listagem retorna um array desse DTO.
As propriedades são serializadas em camelCase:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id` | `Guid` | Identificador da entrega. |
| `trackingCode` | `string` | Código de rastreamento com índice único no banco. |
| `recipientName` | `string` | Nome do destinatário. |
| `originAddress` | `string` | Endereço de origem. |
| `destinationAddress` | `string` | Endereço de destino. |
| `status` | `string` | Status atual. |
| `createdAt` | `DateTime` | Data e hora de criação em UTC. |
| `updatedAt` | `DateTime?` | Data e hora da última transição válida em UTC; inicialmente nulo. |
| `estimatedDeliveryDate` | `DateOnly?` | Previsão de entrega informada no cadastro; opcional. |
| `deliveredAt` | `DateTime?` | Data e hora de conclusão em UTC; nulo até a transição para `Delivered`. |

Falhas na validação dos DTOs usam `ValidationProblemDetails`. Rejeições explícitas
de parâmetros da listagem e de transições de status retornam `400` com uma mensagem
textual. Os retornos `404` para entregas inexistentes usam o tratamento padrão do
ASP.NET Core. Não há padronização adicional de erros na aplicação.

## Regras de status e cancelamento

Toda entrega é criada em `Created`. As únicas transições permitidas são:

| Status atual | Próximos status permitidos |
| --- | --- |
| `Created` | `InTransit` ou `Cancelled` |
| `InTransit` | `Delivered` ou `Cancelled` |
| `Delivered` | Nenhum; estado final. |
| `Cancelled` | Nenhum; estado final. |

Não é permitido repetir o status atual, retornar a um estado anterior ou concluir
diretamente uma entrega em `Created`. Essas tentativas retornam `400` e não persistem
alterações. Cada transição válida atualiza `UpdatedAt` com `DateTime.UtcNow`.
Na conclusão, `DeliveredAt` recebe o mesmo instante de `UpdatedAt`.

O endpoint de cancelamento usa o mesmo método de atualização de status, solicitando
`Cancelled`. Entregas já concluídas ou canceladas retornam `400`. Cancelar preserva
o registro da entrega no banco.

## Fluxo de criação

1. O cliente envia `POST /api/deliveries` com JSON no corpo.
2. O ASP.NET Core seleciona `DeliveryController.Create`, converte o JSON em
   `CreateDeliveryRequest` e valida seus atributos. Se inválido, `[ApiController]`
   retorna `400` antes de executar a action.
3. O controller passa o DTO para `DeliveryService.CreateAsync`.
4. O serviço cria `Delivery` com um novo Guid, código `DL-` seguido do Guid sem
   hífens, status `Created`, `CreatedAt = DateTime.UtcNow`, `UpdatedAt = null` e
   `DeliveredAt = null`. A previsão de entrega é copiada da requisição.
5. O serviço adiciona a entidade ao `DeliveryLabDbContext` e chama `SaveChangesAsync`.
6. O EF Core, pelo provider Npgsql, executa o INSERT no PostgreSQL. O índice único
   protege `TrackingCode` contra duplicidade.
7. O serviço mapeia manualmente a entidade persistida para `DeliveryResponse`.
8. O controller retorna `201 Created`, o JSON da entrega e o header `Location`
   apontando para a consulta pelo Id.

```text
HTTP Request → DeliveryController / CreateDeliveryRequest → DeliveryService
→ DeliveryLabDbContext → PostgreSQL → DeliveryResponse → HTTP Response (201)
```

## Fluxo de alteração de status

1. O cliente envia `PATCH /api/deliveries/{id}/status` com o novo status.
2. O ASP.NET Core lê o Guid da rota e o `UpdateDeliveryStatusRequest` do corpo.
3. Os atributos do DTO validam o status. Um valor ausente ou inválido retorna `400`
   antes de chamar o serviço.
4. `DeliveryController.UpdateStatus` chama `DeliveryService.UpdateStatusAsync`.
5. O serviço busca a entrega pelo Id usando `DeliveryLabDbContext`. Se não existir,
   retorna `null` e o controller responde `404`.
6. Se existir, o serviço verifica a transição. Quando inválida, lança
   `InvalidOperationException`, convertida pelo controller em `400` com a mensagem
   da exceção, antes de alterar ou persistir a entidade.
7. Em uma transição válida, altera `Status`, atualiza `UpdatedAt` e preenche
   `DeliveredAt` caso o novo status seja `Delivered`. Em seguida, chama
   `SaveChangesAsync`, que persiste o UPDATE no PostgreSQL.
8. O serviço mapeia a entrega para `DeliveryResponse`; o controller retorna `200`
   com o JSON atualizado. O cancelamento percorre o mesmo fluxo com `Cancelled`.

## Testes

```powershell
dotnet test
```

Existem seis testes xUnit em `DeliveryEndpointsTests.cs`:

1. Criação bem-sucedida, incluindo a consulta da entrega persistida.
2. Consulta de Id inexistente retorna `404`.
3. Alteração de status persiste o valor e `UpdatedAt`.
4. Status inválido retorna `400` e mantém os dados anteriores.
5. Cancelamento de uma entrega em `Created` retorna `200` e persiste `Cancelled` e `UpdatedAt`.
6. Cancelamento de uma entrega em `Delivered` retorna `400` e preserva o status, com `DeliveredAt` preenchido.

Os testes iniciam a API com `WebApplicationFactory`, passando pelos controllers,
validação dos DTOs, serviço e EF Core. Cada teste usa seu próprio SQLite em memória,
sem acessar ou modificar o PostgreSQL local. Eles não validam as particularidades
do provider PostgreSQL nem executam suas migrations. A aplicação usa PostgreSQL.

A suíte atual não cobre especificamente filtros e paginação, consulta por código
de rastreamento, previsão de entrega ou todas as transições permitidas e proibidas.
Essas funcionalidades existem no código, mas a cobertura automatizada é parcial.

## Pacotes NuGet adicionados

Dependências diretas, fixadas nos arquivos `.csproj`:

| Projeto | Pacote | Versão | Uso |
| --- | --- | --- | --- |
| API | `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.11 | Provider EF Core para PostgreSQL; traz o EF Core e Npgsql como dependências. |
| API | `Microsoft.EntityFrameworkCore.Design` | 8.0.31 | Ferramentas de design para criação e aplicação de migrations. |
| API | `Swashbuckle.AspNetCore.SwaggerGen` | 10.2.3 | Gerar o documento OpenAPI a partir dos controllers e DTOs. |
| API | `Swashbuckle.AspNetCore.Swagger` | 10.2.3 | Servir o documento OpenAPI por HTTP. |
| API | `Scalar.AspNetCore` | 2.17.8 | Exibir a interface visual para documentação e chamadas manuais. |
| Tests | `Microsoft.AspNetCore.Mvc.Testing` | 8.0.31 | Hospedar a API nos testes com `WebApplicationFactory`. |
| Tests | `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.31 | Banco relacional em memória exclusivo dos testes. |
| Tests | `Microsoft.NET.Test.Sdk` | 17.14.1 | Integrar os testes ao comando `dotnet test`. |
| Tests | `xunit` | 2.9.3 | Framework dos seis testes. |
| Tests | `xunit.runner.visualstudio` | 3.1.4 | Descobrir e executar testes xUnit pelo runner .NET/Visual Studio. |

Além desses pacotes, o manifesto local inclui a ferramenta NuGet `dotnet-ef`
8.0.31 para os comandos de migrations.

## Organização e limites atuais

- API HTTP local na porta 5080, sem abertura automática do navegador.
- Código de rastreamento no formato `DL-{Guid sem hífens}`, derivado do Id.
- Validação na entrada HTTP com Data Annotations; serviço recebe os DTOs validados.
- Mapeamento manual concentrado em um método privado do serviço.
- POST retorna `201` e `Location`; PATCH retorna `200` com a entrega atualizada.
- Status e busca por código de rastreamento distinguem maiúsculas e minúsculas.
- Consultas usam `AsNoTracking`; a listagem aplica filtro, ordenação e paginação no banco.
- Transições de status são validadas no serviço, inclusive no cancelamento.
- Seed fixo via `HasData`, aplicado pela migration inicial.
- Migrations executadas explicitamente pela CLI.
- OpenAPI e Scalar disponíveis sem condição de ambiente, para o laboratório.
- SQLite em memória apenas nos testes para executá-los sem banco externo.
- A conexão não contém credenciais no arquivo versionado; User Secrets atende ao ambiente `Development`.

O projeto não inclui outras camadas, interfaces de serviço ou repositórios. Sua
organização inicial serve como base para os exercícios de treinamento.

Não há autenticação ou autorização configurada, endpoint de exclusão de entregas,
integração de pagamentos, mensageria ou serviços distribuídos implementados.

## Roadmap

As fases abaixo separam o estado implementado das evoluções planejadas. Os nomes
das fases futuras não representam funcionalidades disponíveis na API atual.

### Fase 1 — DeliveryLab Core

**Estado:** núcleo de entregas implementado, com cobertura automatizada parcial.

- **Concluído:** cadastro com previsão opcional, rastreamento, consultas, filtro por
  status, paginação, regras de transição, registro de conclusão e cancelamento.
- **Concluído:** persistência PostgreSQL com EF Core e migrations, documentação
  OpenAPI/Scalar, configuração com User Secrets e seis testes de integração.
- **Lacuna identificada:** a suíte ainda não cobre todos os cenários descritos na
  seção de testes. A implementação de novos testes depende da definição das próximas demandas.

### Fase 2 — DeliveryLab Payments

**Estado:** planejada, sem implementação no código atual.

Fase reservada à evolução de pagamentos. Requisitos, regras de negócio, contratos,
provedores e critérios de aceite ainda precisam ser definidos. Não há endpoints,
modelos ou testes de pagamentos na solução.

### Fase 3 — DeliveryLab Distributed

**Estado:** planejada, sem implementação no código atual.

Fase reservada à evolução para cenários distribuídos. A divisão de serviços, os
contratos de comunicação, a infraestrutura e os critérios de aceite ainda precisam
ser definidos. A solução atual permanece composta pela API e pelo projeto de testes.
