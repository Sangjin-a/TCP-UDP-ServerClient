public static class Program
{
    public static void Main(string[] args)
    {
TcpServer server = new TcpServer();
        int port = 5000;
        server.StartServer(port);
        Console.WriteLine("엔터 키를 눌러 서버를 종료합니다...");
        Console.ReadLine();
        server.StopServer();
    }
}