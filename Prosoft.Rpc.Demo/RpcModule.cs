using System;
using System.IO;
using System.Reflection;
using Nancy;

namespace Prosoft.Rpc.Demo
{
    public class RpcModule : NancyModule
    {
        public RpcModule()
            : base("/rpc")
        {
            Post("/", _ =>
            {
                var service = Request.Query["name"];
                var methodName = Request.Query["method"];

                if (string.IsNullOrWhiteSpace(service)) return 500;
                if (string.IsNullOrWhiteSpace(methodName)) return 500;

                var sessionId = Guid.Empty;

                if (!Request.Cookies.ContainsKey("sessionId") || !Guid.TryParse(Request.Cookies["sessionId"], out sessionId))
                {

                }

                if (!Prosoft.Rpc.Host.TryCreateInstance(service, out object instance))
                    return 404;

                byte[] requestData = null;

                if (Request.Body.Length != 0)
                {
                    requestData = new byte[Request.Body.Length];

                    Request.Body.Read(requestData, 0, requestData.Length);
                }

                try
                {
                    byte[] invokeResult = Prosoft.Rpc.Host.Invoke(instance, methodName, requestData);

                    if (invokeResult == null)
                    {
                        return 200;
                    }
                    else
                    {
                        return new Response()
                        {
                            ContentType = "application/json",
                            Contents = s =>
                            {
                                s.Write(invokeResult, 0, invokeResult.Length);
                            }
                        };
                    }
                }
                catch (TargetInvocationException e)
                {
                    return Response.AsText(e.InnerException.Message).WithStatusCode(HttpStatusCode.InternalServerError);
                }
                catch (Exception e)
                {
                    return Response.AsText(e.Message).WithStatusCode(HttpStatusCode.InternalServerError);
                }
            });
        }
    }
}
