using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lab5
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server("E:" + System.IO.Path.DirectorySeparatorChar + "CSAN" + System.IO.Path.DirectorySeparatorChar);
            try
            {
                server.Start("http://127.0.0.1:8000/");
            }
            catch
            {
            }
            Console.ReadLine();
        }
    }
}
