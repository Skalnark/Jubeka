# jubeka-cli

Jubeka is a REST client for making HTTP requests, testing APIs, and automating workflows.

## Features

- Send HTTP requests with headers, query parameters, and request bodies.
- Substitute variables in URLs, headers, query parameters, and bodies using a YAML file.
- Load OpenAPI specifications from URL, file, or raw text and generate requests by `operationId`.
- Manage named environment configs stored under `$HOME/.config/jubeka/`.

## Usage

### Basic request

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request --method GET --url https://api.example.com/ping
```

### Use environment variables from YAML

```yaml
# env.yml
variables:
 baseUrl: https://api.example.com
 token: abc123
 id: 42
```

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request --method GET --url "${baseUrl}/pets/{{id}}" \
 --header "Authorization: Bearer {{token}}" \
 --env env.yml
```

### OpenAPI request by operationId

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 openapi request --operation getPet \
 --spec-url https://example.com/openapi.yaml \
 --env env.yml
```

### Environment config management

Create or update a named environment configuration (stored in `$HOME/.config/jubeka/NAME.json`):

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env create --name dev --vars env.yml --spec-url https://example.com/openapi.yaml
```

Use a named environment when invoking OpenAPI requests:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 openapi request --operation getPet --env-name dev
```

## YAML variables format

```yaml
variables:
 baseUrl: https://api.example.com
 token: abc123
 id: 42
 body: '{"name":"Fido"}'
```

You can use `${var}` or `{{var}}` in URLs, headers, query parameters, and request bodies.

## Notes

- Missing required variables or invalid OpenAPI specifications will surface clear error messages.
- OpenAPI base URL can come from the spec `servers` section or the `baseUrl` variable.
