using Jubeka.Core.Domain;

namespace Jubeka.CLI.Application;

public interface IResponseWriter
{
    void Write(ResponseData response, bool pretty);
}
