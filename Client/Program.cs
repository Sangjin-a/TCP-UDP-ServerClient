using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

public class Program
{
    public static async Task Main(string[] args)
    {
        TcpClientHandler clientHandler = new TcpClientHandler();

        string serverIp;
        int serverPort = 5000;
        int delay = 100;
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string configFilePath = Path.Combine(baseDirectory, "client_config.xml");
        // 서버 연결
        XmlSerializer serializer = new XmlSerializer(typeof(Config));

        // XmlReaderSettings로 네임스페이스 무시
        using (XmlReader reader = XmlReader.Create(configFilePath))
        {
            Config config = (Config)serializer.Deserialize(reader);
            Console.WriteLine("설정 파일을 성공적으로 불러왔습니다.");
            Console.WriteLine($"  - 서버 IP: {config.ServerIp}");
            Console.WriteLine($"  - 서버 포트: {config.ServerPort}");
            Console.WriteLine($"  - 지연 시간: {config.Delay}ms");
            serverIp = config.ServerIp;
            serverPort = config.ServerPort;
            delay = config.Delay;
        }
        bool connected = await clientHandler.ConnectAsync(serverIp, serverPort);

        if (!connected)
        {
            Console.WriteLine("서버 연결 실패");
            return;
        }

        // 파일 경로 입력 받기
        while (true)
        {
            Console.Write("전송할 파일 경로를 입력하세요: ");
            string filePath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.WriteLine("파일 경로가 입력되지 않았습니다.");
                return;
            }

            // 파일 전송
            await clientHandler.SendFileAsync(filePath, delay);
        }
        // 종료 대기
        Console.WriteLine("\n종료하려면 아무 키나 누르세요...");
        Console.ReadKey();

        clientHandler.Disconnect();
    }
}

[XmlRoot("Config")]
public class Config
{
    [XmlElement("ServerIp")]
    public string ServerIp { get; set; }

    [XmlElement("ServerPort")]
    public int ServerPort { get; set; }

    [XmlElement("Delay")]
    public int Delay { get; set; }
}