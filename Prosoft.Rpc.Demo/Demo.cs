using System;

namespace Prosoft.Rpc.Demo
{
    public class Demo : IDemo
    {
        public string Hello(string name)
        {
            return "Hello " + name;
        }
    }
}