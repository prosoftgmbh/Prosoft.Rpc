using System;

namespace Prosoft.Rpc.Demo
{
    public class Demo : IDemo
    {
        public HelloResponse Hello(string name, int age, HelloRequest helloRequest)
        {
            return new HelloResponse()
            {
                Name = "Hello " + name,
                Age = age  + 1
            };
        }
    }
}