# jubeka-cli

This is the command line interface for Jubeka, a REST client for making HTTP requests, testing APIs, and automating workflows.

> Jubeka is the name of my oldest cat. I named the project after her because of [Bruno](https://github.com/usebruno/bruno)

## Usage

Build the CLI explicitly (useful once other projects like Jubeka.GUI exist):

```
dotnet build Jubeka/CLI/Jubeka.CLI.csproj
```

Basic request:

```
dotnet run --project Jubeka/CLI/Jubeka.CLI.csproj -- request --method GET --url https://httpbin.org/get
```

Send JSON body and query params:

```
dotnet run --project Jubeka/CLI/Jubeka.CLI.csproj -- request --method POST --url https://httpbin.org/post \
	--body '{"name":"Jubeka"}' \
	--query page=1 --query size=10
```

Use environment variables from YAML (supports ${var} or {{var}} substitution):

```
dotnet run --project Jubeka/CLI/Jubeka.CLI.csproj -- request --method GET --url "${baseUrl}/users" \
	--header "Authorization: Bearer ${token}" \
	--env env.yml
```

Example env.yml:

```
variables:
	baseUrl: https://api.example.com
	token: abc123
```