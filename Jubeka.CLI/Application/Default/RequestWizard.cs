using System;
using System.Collections.Generic;
using System.Linq;
using Jubeka.CLI.Domain;
using Jubeka.Core.Domain;

namespace Jubeka.CLI.Application.Default;

public sealed class RequestWizard(IPrompt prompt) : IRequestWizard
{
    public RequestDefinition BuildRequest(EnvRequestAddOptions options, IReadOnlyDictionary<string, string> vars)
    {
        Console.WriteLine("Starting request add wizard:");

        string name = prompt.PromptRequired("Request name", options.Name);
        string method = prompt.PromptRequired("Method", options.Method ?? "GET");
        string url = prompt.PromptRequired("URL", options.Url);
        string? body = prompt.PromptOptional("Body (optional)", options.Body);

        List<QueryParamDefinition> queries = NormalizeQueryParams(options.QueryParams);
        ManageQueryParams(queries, vars);

        List<string> headers = options.Headers?.ToList() ?? [];
        ManageHeaders(headers);

        AuthConfig auth = BuildAuthConfig(vars);

        return new RequestDefinition(name, method, url, body, queries, headers, auth);
    }

    public RequestDefinition EditRequest(RequestDefinition request, IReadOnlyDictionary<string, string> vars)
    {
        string name = request.Name;
        string method = request.Method;
        string url = request.Url;
        string? body = request.Body;
        List<QueryParamDefinition> queries = request.QueryParams.ToList();
        List<string> headers = request.Headers.ToList();
        AuthConfig auth = request.Auth;

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Edit request:");
            Console.WriteLine("1) Name");
            Console.WriteLine("2) Method");
            Console.WriteLine("3) URL");
            Console.WriteLine("4) Body");
            Console.WriteLine("5) Query params");
            Console.WriteLine("6) Headers");
            Console.WriteLine("7) Auth");
            Console.WriteLine("8) Save");
            Console.WriteLine("9) Cancel");

            string choice = prompt.PromptWithDefault("Select", "8");
            switch (choice)
            {
                case "1":
                    name = prompt.PromptRequired("Name", name);
                    break;
                case "2":
                    method = prompt.PromptRequired("Method", method);
                    break;
                case "3":
                    url = prompt.PromptRequired("URL", url);
                    break;
                case "4":
                    body = prompt.PromptOptional("Body (optional)", body);
                    break;
                case "5":
                    ManageQueryParams(queries, vars);
                    break;
                case "6":
                    ManageHeaders(headers);
                    break;
                case "7":
                    auth = BuildAuthConfig(vars, auth);
                    break;
                case "8":
                    return new RequestDefinition(name, method, url, body, queries, headers, auth);
                case "9":
                    return request;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    public int SelectRequestIndex(IReadOnlyList<RequestDefinition> requests, string? requestName)
    {
        if (!string.IsNullOrWhiteSpace(requestName))
        {
            int idx = requests.ToList().FindIndex(r => string.Equals(r.Name, requestName, StringComparison.OrdinalIgnoreCase));
            return idx;
        }

        Console.WriteLine("Select a request:");
        for (int i = 0; i < requests.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {requests[i].Name} [{requests[i].Method}] {requests[i].Url}");
        }

        string input = prompt.PromptWithDefault("Enter number (blank to cancel)", string.Empty);
        if (string.IsNullOrWhiteSpace(input))
        {
            return -1;
        }

        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= requests.Count)
        {
            return choice - 1;
        }

        Console.WriteLine("Invalid selection.");
        return -1;
    }

    private void ManageQueryParams(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Query params:");
            if (queries.Count == 0)
            {
                Console.WriteLine("(none)");
            }
            else
            {
                for (int i = 0; i < queries.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {queries[i].Key}={queries[i].Value}");
                }
            }

            Console.WriteLine("1) Add new");
            Console.WriteLine("2) Change");
            Console.WriteLine("3) Delete");
            Console.WriteLine("4) Select existing variable");
            Console.WriteLine("5) Done");

            string choice = prompt.PromptWithDefault("Select", "5");
            switch (choice)
            {
                case "1":
                    AddQueryParam(queries, vars, allowVarSelection: true);
                    break;
                case "2":
                    ChangeQueryParam(queries, vars);
                    break;
                case "3":
                    DeleteQueryParam(queries);
                    break;
                case "4":
                    AddQueryParam(queries, vars, allowVarSelection: true, forceSelectVar: true);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private void AddQueryParam(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars, bool allowVarSelection, bool forceSelectVar = false)
    {
        string key = prompt.PromptRequired("Query key", null);
        string value;

        if (allowVarSelection && vars.Count > 0 && (forceSelectVar || prompt.PromptYesNo("Use existing variable?", false) == true))
        {
            string? selected = PromptSelectVariable(vars);
            if (selected == null)
            {
                return;
            }

            value = $"{{{{{selected}}}}}";
        }
        else
        {
            value = prompt.PromptRequired("Query value", null);
        }

        queries.Add(new QueryParamDefinition(key, value));
    }

    private void ChangeQueryParam(List<QueryParamDefinition> queries, IReadOnlyDictionary<string, string> vars)
    {
        if (queries.Count == 0)
        {
            Console.WriteLine("No query params to change.");
            return;
        }

        int index = PromptIndex("Select query param", queries.Count);
        if (index < 0)
        {
            return;
        }

        QueryParamDefinition current = queries[index];
        string key = prompt.PromptRequired("Query key", current.Key);
        string value;
        if (vars.Count > 0 && prompt.PromptYesNo("Use existing variable?", false) == true)
        {
            string? selected = PromptSelectVariable(vars);
            if (selected == null)
            {
                return;
            }

            value = $"{{{{{selected}}}}}";
        }
        else
        {
            value = prompt.PromptRequired("Query value", current.Value);
        }

        queries[index] = new QueryParamDefinition(key, value);
    }

    private void DeleteQueryParam(List<QueryParamDefinition> queries)
    {
        if (queries.Count == 0)
        {
            Console.WriteLine("No query params to delete.");
            return;
        }

        int index = PromptIndex("Select query param to delete", queries.Count);
        if (index < 0)
        {
            return;
        }

        queries.RemoveAt(index);
    }

    private void ManageHeaders(List<string> headers)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Headers:");
            if (headers.Count == 0)
            {
                Console.WriteLine("(none)");
            }
            else
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {headers[i]}");
                }
            }

            Console.WriteLine("1) Add new");
            Console.WriteLine("2) Change");
            Console.WriteLine("3) Delete");
            Console.WriteLine("4) Done");

            string choice = prompt.PromptWithDefault("Select", "4");
            switch (choice)
            {
                case "1":
                    headers.Add(prompt.PromptRequired("Header (Name: Value)", null));
                    break;
                case "2":
                    if (headers.Count == 0)
                    {
                        Console.WriteLine("No headers to change.");
                        break;
                    }
                    int index = PromptIndex("Select header", headers.Count);
                    if (index >= 0)
                    {
                        headers[index] = prompt.PromptRequired("Header (Name: Value)", headers[index]);
                    }
                    break;
                case "3":
                    if (headers.Count == 0)
                    {
                        Console.WriteLine("No headers to delete.");
                        break;
                    }
                    int deleteIndex = PromptIndex("Select header to delete", headers.Count);
                    if (deleteIndex >= 0)
                    {
                        headers.RemoveAt(deleteIndex);
                    }
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
    }

    private AuthConfig BuildAuthConfig(IReadOnlyDictionary<string, string> vars, AuthConfig? current = null)
    {
        AuthMethod method = current?.Method ?? AuthMethod.Inherit;
        Console.WriteLine();
        Console.WriteLine("Auth method:");
        Console.WriteLine("1) Inherit");
        Console.WriteLine("2) None");
        Console.WriteLine("3) Basic");
        Console.WriteLine("4) Bearer");

        string choice = prompt.PromptWithDefault("Select", ((int)method + 1).ToString());
        method = choice switch
        {
            "1" => AuthMethod.Inherit,
            "2" => AuthMethod.None,
            "3" => AuthMethod.Basic,
            "4" => AuthMethod.Bearer,
            _ => method
        };

        return method switch
        {
            AuthMethod.Basic => new AuthConfig(method,
                Username: PromptValueOrVariable("Username", vars, current?.Username),
                Password: PromptValueOrVariable("Password", vars, current?.Password)),
            AuthMethod.Bearer => new AuthConfig(method,
                Token: PromptValueOrVariable("Token", vars, current?.Token)),
            _ => new AuthConfig(method)
        };
    }

    private string PromptValueOrVariable(string label, IReadOnlyDictionary<string, string> vars, string? defaultValue)
    {
        if (vars.Count > 0 && prompt.PromptYesNo($"Use existing variable for {label}?", false) == true)
        {
            string? selected = PromptSelectVariable(vars);
            if (selected != null)
            {
                return $"{{{{{selected}}}}}";
            }
        }

        return prompt.PromptRequired(label, defaultValue);
    }

    private string? PromptSelectVariable(IReadOnlyDictionary<string, string> vars)
    {
        List<string> keys = vars.Keys.OrderBy(k => k).ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {keys[i]}");
        }

        int index = PromptIndex("Select variable", keys.Count);
        return index < 0 ? null : keys[index];
    }

    private int PromptIndex(string label, int max)
    {
        string input = prompt.PromptWithDefault($"{label} (1-{max}, blank to cancel)", string.Empty);
        if (string.IsNullOrWhiteSpace(input))
        {
            return -1;
        }

        return int.TryParse(input, out int index) && index >= 1 && index <= max ? index - 1 : -1;
    }

    private static List<QueryParamDefinition> NormalizeQueryParams(IReadOnlyList<string>? raw)
    {
        List<QueryParamDefinition> results = [];
        if (raw == null)
        {
            return results;
        }

        foreach (string entry in raw)
        {
            int idx = entry.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            string key = entry[..idx];
            string value = entry[(idx + 1)..];
            results.Add(new QueryParamDefinition(key, value));
        }

        return results;
    }
}
