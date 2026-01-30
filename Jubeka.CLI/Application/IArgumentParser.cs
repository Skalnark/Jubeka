using Jubeka.CLI.Domain;

namespace Jubeka.CLI.Application;

public interface IArgumentParser
{
    ParseResult Parse(string[] args);
}