# DeliveryLab

Laboratório de desenvolvimento backend de uma empresa fictícia de entregas.
Esta primeira versão apenas registra entregas, consulta seus dados e altera seu status.

O objetivo didático é praticar a investigação de código existente, implementação
de demandas, manutenção, debugging, testes e refatoração. O projeto é
propositalmente pequeno e simples. Novos conceitos e mudanças arquiteturais serão
introduzidos gradualmente por tarefas de treinamento, quando houver necessidade real.

## Tecnologias e estrutura

.NET 8, C#, ASP.NET Core Web API com controllers, Entity Framework Core 8,
PostgreSQL, OpenAPI, Scalar e xUnit. São apenas dois projetos:

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
│   │       ├── <timestamp>_InitialCreate.cs
│   │       ├── <timestamp>_InitialCreate.Designer.cs
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
| `CreateDeliveryRequest.cs` | Receber e validar os três campos obrigatórios da criação. |
| `UpdateDeliveryStatusRequest.cs` | Receber e validar o status permitido. |
| `DeliveryResponse.cs` | Definir os dados retornados pela API. |
| `DeliveryService.cs` | Criar, consultar e atualizar entregas usando diretamente o DbContext; mapear a entidade para o DTO de resposta. |
| `DeliveryLabDbContext.cs` | Mapear a entidade, configurar o índice único de rastreamento e definir duas entregas iniciais. |
| `Data/Migrations/` | Criar a tabela, o índice único e os dados iniciais; registrar o modelo do EF. |
| `appsettings.json` | Manter a chave da conexão sem credenciais e o logging padrão. |
| `launchSettings.json` | Definir a URL HTTP usada por `dotnet run`. |
| `DeliveryApiFactory.cs` | Iniciar a API em memória nos testes, substituindo PostgreSQL por SQLite em memória. |
| `DeliveryEndpointsTests.cs` | Exercitar os quatro comportamentos básicos solicitados via HTTP. |
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

## Restaurar, aplicar a migration e executar

Após cadastrar a conexão, execute os comandos a partir da pasta que contém `DeliveryLab.sln` (na mesma sessão do PowerShell):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet restore
dotnet tool restore
dotnet build
dotnet ef database update --project DeliveryLab.Api
dotnet run --project DeliveryLab.Api --launch-profile http
```

A migration `InitialCreate` já está incluída. Ela cria a tabela `Deliveries`, o
índice único de `TrackingCode` e duas entregas fictícias com status `Created`:

| Destinatário | Origem | Destino | Código |
| --- | --- | --- | --- |
| Cliente Teste 1 | São Paulo - SP | Campinas - SP | `DL-11111111111111111111111111111111` |
| Cliente Teste 2 | São José do Rio Preto - SP | Ribeirão Preto - SP | `DL-22222222222222222222222222222222` |

As migrations são aplicadas pelo comando acima, não automaticamente ao iniciar a
API. O seed usa IDs e datas UTC fixas e é inserido pela migration inicial.

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
Nesta versão do pacote, os arquivos JavaScript do Scalar são servidos pela própria API.

## Endpoints

| Método | Rota | Resultado |
| --- | --- | --- |
| POST | `/api/deliveries` | `201` com a entrega e header `Location`; `400` para dados inválidos. |
| GET | `/api/deliveries` | `200` com todas as entregas, ou `[]` quando não houver dados. |
| GET | `/api/deliveries/{id}` | `200` com a entrega; `404` se não existir. |
| GET | `/api/deliveries/tracking/{trackingCode}` | `200` com a entrega; `404` se não existir. |
| PATCH | `/api/deliveries/{id}/status` | `200` com a entrega atualizada; `400` para status inválido; `404` se não existir. |

Exemplo de corpo do POST:

```json
{
  "recipientName": "Maria Silva",
  "originAddress": "São Paulo - SP",
  "destinationAddress": "Campinas - SP"
}
```

Exemplo de corpo do PATCH:

```json
{
  "status": "InTransit"
}
```

`RecipientName`, `OriginAddress` e `DestinationAddress` são obrigatórios. Campos
ausentes, nulos, vazios ou contendo apenas espaços retornam `400`. Os status aceitos
são exatamente `Created`, `InTransit`, `Delivered` e `Cancelled`, respeitando
maiúsculas e minúsculas. Não há regras de transição: por exemplo, `Delivered` pode
voltar para `InTransit`. Repetir o mesmo status também atualiza `UpdatedAt`.

As respostas usam JSON em camelCase. Erros de validação e respostas `404` usam o
tratamento padrão do ASP.NET Core, sem personalização de Problem Details.

## Fluxo de criação

1. O cliente envia `POST /api/deliveries` com JSON no corpo.
2. O ASP.NET Core seleciona `DeliveryController.Create`, converte o JSON em
   `CreateDeliveryRequest` e valida seus atributos. Se inválido, `[ApiController]`
   retorna `400` antes de executar a action.
3. O controller passa o DTO para `DeliveryService.CreateAsync`.
4. O serviço cria `Delivery` com um novo Guid, código `DL-` seguido do Guid sem
   hífens, status `Created`, `CreatedAt = DateTime.UtcNow` e `UpdatedAt = null`.
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
6. Se existir, altera `Status`, define `UpdatedAt = DateTime.UtcNow` e chama
   `SaveChangesAsync`. O EF Core executa o UPDATE no PostgreSQL.
7. O serviço mapeia a entrega para `DeliveryResponse`; o controller retorna `200`
   com o JSON atualizado. Não verifica transições entre estados.

## Testes

```powershell
dotnet test
```

Existem apenas quatro testes xUnit:

1. Criação bem-sucedida, incluindo a consulta da entrega persistida.
2. Consulta de Id inexistente retorna `404`.
3. Alteração de status persiste o valor e `UpdatedAt`.
4. Status inválido retorna `400` e mantém os dados anteriores.

Os testes iniciam a API com `WebApplicationFactory`, passando pelos controllers,
validação dos DTOs, serviço e EF Core. Cada teste usa seu próprio SQLite em memória,
sem acessar ou modificar o PostgreSQL local. Eles não validam as particularidades
do provider PostgreSQL nem executam suas migrations. A aplicação usa PostgreSQL.

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
| Tests | `xunit` | 2.9.3 | Framework dos quatro testes. |
| Tests | `xunit.runner.visualstudio` | 3.1.4 | Descobrir e executar testes xUnit pelo runner .NET/Visual Studio. |

Além desses pacotes, o manifesto local inclui a ferramenta NuGet `dotnet-ef`
8.0.31 para os comandos de migrations.

## Decisões simples desta versão

- API HTTP local na porta 5080, sem abertura automática do navegador.
- Código de rastreamento no formato `DL-{Guid sem hífens}`, derivado do Id.
- Validação na entrada HTTP com Data Annotations; serviço recebe os DTOs validados.
- Mapeamento manual concentrado em um método privado do serviço.
- POST retorna `201` e `Location`; PATCH retorna `200` com a entrega atualizada.
- Status e busca por código de rastreamento distinguem maiúsculas e minúsculas.
- Seed fixo via `HasData`, aplicado pela migration inicial.
- Migrations executadas explicitamente pela CLI.
- OpenAPI e Scalar disponíveis sem condição de ambiente, para o laboratório.
- SQLite em memória apenas nos testes para executá-los sem banco externo.

O projeto não inclui outras camadas, interfaces de serviço ou repositórios. Sua
organização inicial serve como base para os exercícios de treinamento.
