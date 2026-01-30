namespace Jubeka.CLI.Application;

public interface IResponseFormatter
{
    string Format(string body, bool pretty);
}
