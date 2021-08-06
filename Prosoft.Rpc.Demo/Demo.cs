using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
