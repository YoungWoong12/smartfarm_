using System;
using System.Threading.Tasks;

namespace smartfarm_Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // ===== 여기서 TCP 포트 + 아두이노 COM 포트 설정 =====
            int tcpPort = 6000;          // 클라 포트 설정
            string comPort = "COM3";     // 아두이노 포트  ex) "COM3, COM4"

            Server server = new Server(tcpPort, comPort);

            // 비동기로 서버 시작
            Task.Run(async () => await server.StartAsync()).GetAwaiter().GetResult();

            
            Console.WriteLine("서버 종료");
        }
    }
}
