using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading.Tasks;

// ==================== 메인 프로그램 ====================
public class Program
{
    private static string IP = "";
    public static async Task Main(string[] args)
    {
        Console.WriteLine("===== 파일 전송 테스트 =====\n");

        Console.WriteLine("프로토콜:");
        Console.WriteLine("1: TCP");
        Console.WriteLine("2: UDP");
        Console.Write("선택: ");
        string protocolChoice = Console.ReadLine();

        Console.WriteLine("\n역할:");
        Console.WriteLine("1: 서버");
        Console.WriteLine("2: 클라이언트");
        Console.Write("선택: ");
        string roleChoice = Console.ReadLine();

        Console.WriteLine();

        if (protocolChoice == "1") // TCP
        {
            if (roleChoice == "1")
            {
                TcpServer server = new TcpServer();
                await server.StartServer(5000);
            }
            else if (roleChoice == "2")
            {
                TcpClientHandler client = new TcpClientHandler();

                Console.Write("서버 IP (기본 127.0.0.1): ");
                string ip = Console.ReadLine();
                if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

                await client.ConnectAsync(ip, 5000);

                Console.Write("파일 경로: ");
                string filePath = Console.ReadLine();

                Console.Write("Delay (ms, 기본 0): ");
                string delayInput = Console.ReadLine();
                int delay = string.IsNullOrEmpty(delayInput) ? 0 : int.Parse(delayInput);

                await client.SendFileAsync(filePath, delay);
                Console.WriteLine("종료: 아무 키나...");
                Console.ReadKey();
                client.Disconnect();
            }
        }
        else if (protocolChoice == "2") // UDP
        {
            if (roleChoice == "1")
            {
                UdpServer server = new UdpServer();
                await server.StartServer(5000);
            }
            else if (roleChoice == "2")
            {
                UdpClientHandler client = new UdpClientHandler();

                Console.Write("서버 IP (기본 127.0.0.1): ");
                string ip = Console.ReadLine();
                if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

                await client.ConnectAsync(ip, 5000);

                Console.Write("파일 경로: ");
                string filePath = Console.ReadLine();

                Console.Write("Delay (ms, 기본 0): ");
                string delayInput = Console.ReadLine();
                int delay = string.IsNullOrEmpty(delayInput) ? 0 : int.Parse(delayInput);

                await client.SendFileAsync(filePath, delay);

                Console.WriteLine("종료: 아무 키나...");
                Console.ReadKey();
                client.Disconnect();
            }
        }
    }
}


#region TCP
// ==================== TCP 서버 ====================
public class TcpServer
{
    private TcpListener listener;
    private bool isRunning;
    public async Task StartServer(int port)
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            isRunning = true;
            Console.WriteLine($"TCP 서버 시작 (포트: {port})\n");

            TcpClient client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("클라이언트 연결\n");
            await HandleClientAsync(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        FileStream fileStream = null;
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[8192];

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            fileStream = new FileStream($"tcp_received_{timestamp}.bin", FileMode.Create, FileAccess.Write);

            long totalBytesReceived = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while (isRunning)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("\n연결 종료");
                    break;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                await stream.WriteAsync(buffer, 0, bytesRead); // 에코 백
                totalBytesReceived += bytesRead;

                if (totalBytesReceived % 5000000 < 8192)
                {
                    Console.WriteLine($"수신: {totalBytesReceived / (1024.0 * 1024.0):F2} MB");
                }
            }

            sw.Stop();
            Console.WriteLine($"\n크기: {totalBytesReceived / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"시간: {sw.Elapsed.TotalSeconds:F2}초");
            Console.WriteLine($"속도: {totalBytesReceived / (1024.0 * 1024.0) / sw.Elapsed.TotalSeconds:F2} MB/s\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류: {ex.Message}");
        }
        finally
        {
            fileStream?.Close();
            client?.Close();
        }
    }
}

// ==================== TCP 클라이언트 ====================
public class TcpClientHandler
{
    private TcpClient client;
    private NetworkStream stream;
    private const int CHUNK_SIZE = 900;

    public async Task<bool> ConnectAsync(string serverIp, int port)
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIp, port);
            stream = client.GetStream();
            //_ : 언더바는 반환값 무시
            //await : 비동기 작업 완료 대기
            _ = ReceiveDataAsync();//서버로부터 에코백 받기 

            Console.WriteLine($"TCP 서버 연결: {serverIp}:{port}\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task SendFileAsync(string filePath, int delay)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"파일 없음: {filePath}");
            return;
        }

        try
        {
            byte[] fileData = await File.ReadAllBytesAsync(filePath);
            Console.WriteLine($"파일: {Path.GetFileName(filePath)}");
            Console.WriteLine($"크기: {fileData.Length:N0} bytes\n");

            int offset = 0;
            int chunkCount = 0;
            int totalChunks = (int)Math.Ceiling((double)fileData.Length / CHUNK_SIZE);
            Stopwatch sw = Stopwatch.StartNew();

            while (offset < fileData.Length)
            {
                // 남은 데이터 크기 계산
                int currentChunkSize = Math.Min(CHUNK_SIZE, fileData.Length - offset);

                // 청크 복사
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fileData, offset, chunk, 0, currentChunkSize);

                // 전송
                await stream.WriteAsync(chunk, 0, currentChunkSize);
                chunkCount++;

                if (chunkCount % 50000 == 0)
                {
                    Console.WriteLine($"송신: {chunkCount}/{totalChunks} ({currentChunkSize} bytes)");
                }

                offset += currentChunkSize;
                if (delay > 0) await Task.Delay(delay);
            }

            sw.Stop();
            Console.WriteLine($"\n송신 완료");
            Console.WriteLine($"청크: {chunkCount:N0}");
            Console.WriteLine($"마지막 청크: {fileData.Length % CHUNK_SIZE} bytes");
            Console.WriteLine($"시간: {sw.Elapsed.TotalSeconds:F2}초\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"전송 오류: {ex.Message}");
        }
    }

    public async Task ReceiveDataAsync()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string savePath = $"tcp_echo_received_{timestamp}.bin";
        using FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);

        byte[] buffer = new byte[8192];
        long totalBytes = 0;
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("\n서버에서 연결 종료됨");
                    break;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }

            sw.Stop();
            Console.WriteLine($"\n 에코백 저장 완료: {savePath}");
            Console.WriteLine($" 수신 크기: {totalBytes / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($" 시간: {sw.Elapsed.TotalSeconds:F2}초");
            Console.WriteLine($" 속도: {totalBytes / (1024.0 * 1024.0) / sw.Elapsed.TotalSeconds:F2} MB/s\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"수신 오류: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        stream?.Close();
        client?.Close();
    }
}
#endregion


          #region UDP
// ==================== UDP 서버 ====================
public class UdpServer
{
    private UdpClient udpServer;
    private bool isRunning;

    public async Task StartServer(int port)
    {
        try
        {
            udpServer = new UdpClient(port);
            isRunning = true;
            Console.WriteLine($"UDP 서버 시작 (포트: {port})\n");
            await ReceiveDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류: {ex.Message}");
        }
    }

    private async Task ReceiveDataAsync()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        FileStream fileStream = new FileStream($"udp_received_{timestamp}.bin", FileMode.Create, FileAccess.Write);

        int packetCount = 0;
        long totalBytesReceived = 0;
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            while (isRunning)
            {
                UdpReceiveResult result = await udpServer.ReceiveAsync();
                byte[] receivedData = result.Buffer;

                // 종료 신호 체크
                if (receivedData.Length == 3 &&
                    receivedData[0] == 0xFF &&
                    receivedData[1] == 0xFF &&
                    receivedData[2] == 0xFF)
                {
                    Console.WriteLine("\n전송 완료 신호 수신");
                    break;
                }

                // 데이터 저장
                await fileStream.WriteAsync(receivedData, 0, receivedData.Length);
                packetCount++;
                totalBytesReceived += receivedData.Length;

                if (packetCount % 50000 == 0)
                {
                    Console.WriteLine($"수신: {packetCount:N0} ({receivedData.Length} bytes) | {totalBytesReceived / (1024.0 * 1024.0):F2} MB");
                }
            }

            sw.Stop();    
            Console.WriteLine($"\n패킷: {packetCount:N0}");
            Console.WriteLine($"크기: {totalBytesReceived / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"시간: {sw.Elapsed.TotalSeconds:F2}초");
            Console.WriteLine($"속도: {totalBytesReceived / (1024.0 * 1024.0) / sw.Elapsed.TotalSeconds:F2} MB/s\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류: {ex.Message}");
        }
        finally
        {
            fileStream?.Close();
        }
    }
}

// ==================== UDP 클라이언트 ====================
public class UdpClientHandler
{
    private UdpClient udpClient;
    private const int CHUNK_SIZE = 900;

    public async Task<bool> ConnectAsync(string serverIp, int port)
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Connect(serverIp, port);
            Console.WriteLine($"UDP 서버 연결: {serverIp}:{port}\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task SendFileAsync(string filePath, int delay)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"파일 없음: {filePath}");
            return;
        }

        try
        {
            byte[] fileData = await File.ReadAllBytesAsync(filePath);
            Console.WriteLine($"파일: {Path.GetFileName(filePath)}");
            Console.WriteLine($"크기: {fileData.Length:N0} bytes\n");

            int offset = 0;
            int chunkCount = 0;
            int totalChunks = (int)Math.Ceiling((double)fileData.Length / CHUNK_SIZE);
            Stopwatch sw = Stopwatch.StartNew();

            while (offset < fileData.Length)
            {
                // 남은 데이터 크기 계산
                int currentChunkSize = Math.Min(CHUNK_SIZE, fileData.Length - offset);

                // 청크 복사
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fileData, offset, chunk, 0, currentChunkSize);

                // UDP 전송
                await udpClient.SendAsync(chunk, currentChunkSize);
                chunkCount++;

                if (chunkCount % 50000 == 0)
                {
                    Console.WriteLine($"송신: {chunkCount}/{totalChunks} ({currentChunkSize} bytes)");
                }

                offset += currentChunkSize;
                if (delay > 0) await Task.Delay(delay);
            }

            sw.Stop();
            Console.WriteLine($"\n송신 완료");
            Console.WriteLine($"청크: {chunkCount:N0}");
            Console.WriteLine($"마지막 청크: {fileData.Length % CHUNK_SIZE} bytes");
            Console.WriteLine($"시간: {sw.Elapsed.TotalSeconds:F2}초\n");

            // 종료 신호 전송
            byte[] endSignal = new byte[] { 0xFF, 0xFF, 0xFF };
            await udpClient.SendAsync(endSignal, endSignal.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"전송 오류: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        udpClient?.Close();
    }
}
#endregion
