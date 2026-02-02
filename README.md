# jubeka-cli

Jubeka is a REST client for making HTTP requests, testing APIs, and automating workflows.

## Development goals

- [DONE] A core library that can be used for different User Interfaces.
- [WIP] CLI tool for sending HTTP requests with variable substitution and OpenAPI support.
- [TBD] GUI application.
- [TBD] API to sync environment configs and collections across devices.
  - [TBD] User authentication and account management.
  - [TBD] Web application for managing environments and collections.

## Building

This project is using a Makefile for building.

Targets:

build   Build the CLI project (Debug)
release   Publish Release binary (single-file)
publish   Publish with custom vars

### Examples

``` bash
make release
make release RUNTIME=linux-arm64
make publish PROJECT=Jubeka.CLI/Jubeka.CLI.csproj RUNTIME=linux-x64 PUBLISH_DIR=./your_dir"
```

## Features

- Send HTTP requests with headers, query parameters, and request bodies.
- Substitute variables in URLs, headers, query parameters, and bodies using a YAML file.
- Load OpenAPI specifications from URL, file, or raw text and generate requests by `operationId`.
- Manage named environment configs stored under `$HOME/.config/jubeka/`.

## Usage

### Basic request

```bash
jubeka-cli request --method GET --url https://api.example.com/ping
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
jubeka-cli request --method GET --url '${baseUrl}/pets/{{id}}' \
 --header "Authorization: Bearer {{token}}" \
 --env env.yml
```

### OpenAPI request by operationId

```bash
jubeka-cli openapi request --operation getPet \
 --spec-url https://example.com/openapi.yaml \
 --env env.yml
```

### Environment config management

Create or update a named environment configuration (stored in `$HOME/.config/jubeka/NAME/config.json`):

```bash
jubeka-cli env create --name dev --vars env.yml --spec-url https://example.com/openapi.yaml
```

Edit an environment configuration (wizard):

```bash
jubeka-cli env edit --name dev
```

Edit an environment configuration inline:

```bash
jubeka-cli env edit --name dev --inline --vars env.yml
```

Set the current environment (so you can omit `--name` in env commands):

```bash
jubeka-cli env set --name dev
```

Delete an environment configuration:

```bash
dotnet run --project Jubeka.CLI/Jubeka.CLI.csproj -- \
 env delete --name dev
```

If the wizard leaves the vars path empty, it defaults to `NAME.yml`. Default OpenAPI spec is optional.

Use a named environment when invoking OpenAPI requests:

```bash
jubeka-cli openapi request --operation getPet --env-name dev
```

### Add requests to a collection

Add a request to an environment's collection interactively:

```bash
jubeka-cli request add --name dev
```

If you set a current environment, you can omit `--name`:

```bash
jubeka-cli request add
```

List requests in a collection:

```bash
jubeka-cli request list --name dev
```

Edit a request from the list:

```bash
jubeka-cli request edit --name dev
```

Execute a stored request:

```bash
jubeka-cli request exec --name dev --req-name Ping
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
