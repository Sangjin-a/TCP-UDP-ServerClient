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
// ==================== 프로토콜 정의 ====================
public static class Protocol
{
    public const byte DATA_CHUNK = 0x01;      // 일반 데이터 청크
    public const byte END_OF_TRANSMISSION = 0x04;  // 전송 완료 (EOT)
    public const byte ACK = 0x06;             // 수신 확인
}

// ==================== 전송 레포트 ====================
public class TransmissionReport
{
    public string FileName { get; set; }
    public long TotalBytes { get; set; }
    public int TotalChunks { get; set; }
    public double ElapsedSeconds { get; set; }
    public double TransferRateMBps { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }

    public void PrintReport()
    {
        Console.WriteLine("\n========================================");
        Console.WriteLine("           전송 레포트");
        Console.WriteLine("========================================");
        Console.WriteLine($"파일명      : {FileName}");
        Console.WriteLine($"파일 크기   : {TotalBytes:N0} bytes ({TotalBytes / (1024.0 * 1024.0):F2} MB)");
        Console.WriteLine($"전송 청크   : {TotalChunks:N0}개");
        Console.WriteLine($"시작 시간   : {StartTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"종료 시간   : {EndTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"소요 시간   : {ElapsedSeconds:F2}초");
        Console.WriteLine($"전송 속도   : {TransferRateMBps:F2} MB/s");
        Console.WriteLine($"전송 상태   : {(IsSuccess ? "성공 ✓" : "실패 ✗")}");
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            Console.WriteLine($"오류 메시지 : {ErrorMessage}");
        }
        Console.WriteLine("========================================\n");
    }

    public void SaveToFile(string logPath = "transmission_log.txt")
    {
        try
        {
            string logEntry = $"[{EndTime:yyyy-MM-dd HH:mm:ss}] " +
                            $"File: {FileName}, " +
                            $"Size: {TotalBytes:N0} bytes, " +
                            $"Time: {ElapsedSeconds:F2}s, " +
                            $"Speed: {TransferRateMBps:F2} MB/s, " +
                            $"Status: {(IsSuccess ? "SUCCESS" : "FAILED")}" +
                            (!string.IsNullOrEmpty(ErrorMessage) ? $", Error: {ErrorMessage}" : "") + "\n";

            File.AppendAllText(logPath, logEntry);
            Console.WriteLine($"레포트 저장: {logPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"레포트 저장 실패: {ex.Message}");
        }
    }
}

// ==================== TCP 서버 (수정) ====================
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
        TransmissionReport report = new TransmissionReport
        {
            StartTime = DateTime.Now,
            IsSuccess = false
        };

        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[900];

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"tcp_received_{timestamp}.bin";
            fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            string doEcho = "Y"; // 기본값 Y
            Console.Write("에코백 Y/N : ");
            doEcho = Console.ReadLine();
            doEcho = string.IsNullOrEmpty(doEcho) ? "Y" : doEcho;
            long totalBytesReceived = 0;
            int chunkCount = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while (isRunning)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                // EOT 시그널 확인 (먼저 체크)
                /*  if (buffer[bytesRead - 1] == Protocol.END_OF_TRANSMISSION)
                      if (buffer[0] == Protocol.END_OF_TRANSMISSION)
                      {
                          Console.WriteLine("\n[수신] 전송 완료 시그널 (EOT) 받음");

                          // ACK 전송
                          byte[] ack = new byte[] { Protocol.ACK };
                          await stream.WriteAsync(ack, 0, 1);
                          Console.WriteLine("[송신] ACK 전송\n");

                          report.IsSuccess = true;
                          break; // EOT는 파일에 쓰지 않고 종료
                      }*/

                // 일반 데이터 처리
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                if (doEcho.ToUpper() == "Y")
                    await stream.WriteAsync(buffer, 0, bytesRead); // 에코백
                totalBytesReceived += bytesRead;
                chunkCount++;

                /*   if (totalBytesReceived % 5000000 < 8192)
                   {
                       Console.WriteLine($"수신: {totalBytesReceived / (1024.0 * 1024.0):F2} MB");
                   }*/
            }

            sw.Stop();

            // 레포트 작성
            report.FileName = fileName;
            report.TotalBytes = totalBytesReceived;
            report.TotalChunks = chunkCount;
            report.EndTime = DateTime.Now;
            report.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            report.TransferRateMBps = totalBytesReceived / (1024.0 * 1024.0) / sw.Elapsed.TotalSeconds;

            report.PrintReport();
            report.SaveToFile();
        }
        catch (Exception ex)
        {
            report.ErrorMessage = ex.Message;
            report.EndTime = DateTime.Now;
            Console.WriteLine($"오류: {ex.Message}");
            report.PrintReport();
        }
        finally
        {
            fileStream?.Close();
            client?.Close();
        }
    }
}

// ==================== TCP 클라이언트 (수정) ====================
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
            Console.WriteLine("에코백 Y/N : ");
            string doEcho = Console.ReadLine();
            doEcho = string.IsNullOrEmpty(doEcho) ? "Y" : doEcho;
            if (doEcho.ToUpper() == "Y")
                _ = ReceiveDataAsync();

            Console.WriteLine($"TCP 서버 연결: {serverIp}:{port}\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<TransmissionReport> SendFileAsync(string filePath, int delay = 0)
    {
        TransmissionReport report = new TransmissionReport
        {
            FileName = Path.GetFileName(filePath),
            StartTime = DateTime.Now,
            IsSuccess = false
        };

        if (!File.Exists(filePath))
        {
            report.ErrorMessage = "파일을 찾을 수 없습니다";
            report.EndTime = DateTime.Now;
            Console.WriteLine($"파일 없음: {filePath}");
            return report;
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

            // 데이터 전송
            while (offset < fileData.Length)
            {
                int currentChunkSize = Math.Min(CHUNK_SIZE, fileData.Length - offset);
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fileData, offset, chunk, 0, currentChunkSize);

                await stream.WriteAsync(chunk, 0, currentChunkSize);
                chunkCount++;

                /*  if (chunkCount % 50000 == 0)
                  {
                      Console.WriteLine($"송신: {chunkCount}/{totalChunks} ({currentChunkSize} bytes)");
                  }*/

                offset += currentChunkSize;
                if (delay > 0) await Task.Delay(delay);
            }

            /*      // EOT 시그널 전송
                  Console.WriteLine("\n[송신] 전송 완료 시그널 (EOT) 전송");
                  byte[] eot = new byte[] { Protocol.END_OF_TRANSMISSION };
                  await stream.WriteAsync(eot, 0, 1);

                  // ACK 대기
                  byte[] ackBuffer = new byte[1];
                  int ackRead = await stream.ReadAsync(ackBuffer, 0, 1);
                  if (ackRead == 1 && ackBuffer[0] == Protocol.ACK)
                  {
                      Console.WriteLine("[수신] ACK 받음 - 서버가 전송 완료 확인\n");
                      report.IsSuccess = true;
                  }*/

            sw.Stop();

            // 레포트 작성
            report.TotalBytes = fileData.Length;
            report.TotalChunks = chunkCount;
            report.EndTime = DateTime.Now;
            report.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            report.TransferRateMBps = fileData.Length / (1024.0 * 1024.0) / sw.Elapsed.TotalSeconds;

            report.PrintReport();
            report.SaveToFile();

            return report;
        }
        catch (Exception ex)
        {
            report.ErrorMessage = ex.Message;
            report.EndTime = DateTime.Now;
            Console.WriteLine($"전송 오류: {ex.Message}");
            report.PrintReport();
            return report;
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
