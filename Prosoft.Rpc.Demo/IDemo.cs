using System;

namespace Prosoft.Rpc.Demo
{
    public class HelloResponse
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    public class HelloRequest
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    public interface IDemo
    {
        HelloResponse Hello(string name, int age, HelloRequest helloRequest);
    }
}