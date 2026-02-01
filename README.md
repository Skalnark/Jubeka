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
 request --method GET --url '${baseUrl}/pets/{{id}}' \
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

Create or update a named environment configuration (stored in `$HOME/.config/jubeka/NAME/config.json`):

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env create --name dev --vars env.yml --spec-url https://example.com/openapi.yaml
```

Edit an environment configuration (wizard):

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env edit --name dev
```

Edit an environment configuration inline:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env edit --name dev --inline --vars env.yml
```

Set the current environment (so you can omit `--name` in env commands):

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env set --name dev
```

If the wizard leaves the vars path empty, it defaults to `NAME.yml`. Default OpenAPI spec is optional.

Use a named environment when invoking OpenAPI requests:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 openapi request --operation getPet --env-name dev
```

### Add requests to a collection

Add a request to an environment's collection interactively:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request add --name dev
```

If you set a current environment, you can omit `--name`:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request add
```

List requests in a collection:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request list --name dev
```

Edit a request from the list:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request edit --name dev
```

Execute a stored request:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 request exec --name dev --req-name Ping
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
