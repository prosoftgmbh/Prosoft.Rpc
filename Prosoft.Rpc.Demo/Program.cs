using System;
using Nancy.Hosting.Self;

namespace Prosoft.Rpc.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Prosoft.Rpc.Client.GetSession = () => Guid.Empty;
            Prosoft.Rpc.Client.GetUri = (contractType) => "http://localhost:8000/rpc";

            var host = new NancyHost(new Uri("http://localhost:8000"));

            host.Start();

            var service = Prosoft.Rpc.Client.Create<IDemo>();

            Console.WriteLine(service.Hello("Jörg"));

            Console.ReadKey();
        }
    }
}
