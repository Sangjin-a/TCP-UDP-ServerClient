using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class TcpServer
{
    private TcpListener listener;
    private bool isRunning;
    private const int PACKET_SIZE = 900; // 900 데이터 + 2 CRC + 8 타임스탬프

    public async Task StartServer(int port)
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            isRunning = true;

            Console.WriteLine($"서버가 포트 {port}에서 시작되었습니다.");

            while (isRunning)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("클라이언트 연결됨");

                // 각 클라이언트를 별도 태스크로 처리
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"서버 오류: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        FileStream fileStream = null;

        try
        {
            NetworkStream netStream = client.GetStream();
            MemoryStream packetBuffer = new MemoryStream();
            byte[] buffer = new byte[900];
            int packetCount = 0;
            long totalBytesReceived = 0;
            fileStream = new FileStream($"received_packet.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            while (isRunning)
            {
                int bytesRead = await netStream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Console.WriteLine("클라이언트 연결 종료");
                    break;
                }

                // 수신한 데이터를 버퍼에 추가
                packetBuffer.Write(buffer, 0, bytesRead);
                // 완전한 패킷들을 처리
                while (packetBuffer.Length >= PACKET_SIZE)
                {
                    byte[] packetData = packetBuffer.ToArray();
                    int currentPacketSize = PACKET_SIZE;
                    if (packetBuffer.Length < PACKET_SIZE)
                    {
                        currentPacketSize = (int)packetBuffer.Length;
                    }

                    packetCount++;
                    byte[] remaining = new byte[packetBuffer.Length - currentPacketSize];
                    Array.Copy(packetData, currentPacketSize, remaining, 0, remaining.Length);
                    packetBuffer = new MemoryStream();
                    packetBuffer.Write(remaining, 0, remaining.Length);
                    totalBytesReceived += currentPacketSize;

                    fileStream.Seek(0, SeekOrigin.End);
                    await fileStream.WriteAsync(packetData, 0, currentPacketSize);
                    await netStream.WriteAsync(packetData, 0, currentPacketSize);
                }
                if (totalBytesReceived % 500000 == 0)
                {
                    Console.WriteLine(totalBytesReceived);
                }
            }
            if (packetBuffer.Length > 0)
            {
                byte[] remain = packetBuffer.ToArray();
                fileStream.Seek(0, SeekOrigin.End);
                await fileStream.WriteAsync(remain, 0, remain.Length);
                totalBytesReceived += remain.Length;
                Console.WriteLine($"[버퍼] 남은 데이터: {packetBuffer.Length} bytes");
            }
            await Task.Delay(3000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"클라이언트 처리 오류: {ex.Message}");
            Console.WriteLine($"스택 트레이스: {ex.StackTrace}");
        }
        finally
        {
            fileStream?.Dispose();
            client.Close();
        }
    }

    private async Task ProcessPacket(byte[] packetData, int packetSize, NetworkStream stream)
    {
        try
        {
            DateTime dtCurrent = DateTime.Now;

            // 데이터 구조: [데이터] + [CRC 2바이트] + [타임스탬프 8바이트]
            int dataSize = packetSize - 2 - 8;

            if (dataSize < 1)
            {
                Console.WriteLine("경고: 데이터 크기가 너무 작습니다.");
                return;
            }

            // 데이터 추출
            byte[] data = new byte[dataSize];
            Array.Copy(packetData, 0, data, 0, dataSize);

            // 데이터 일부 출력
            /*     Console.Write($"데이터 (첫 50바이트): ");
                 for (int i = 0; i < Math.Min(dataSize, 50); i++)
                 {
                     Console.Write($"{data[i]:X2} ");
                 }
                 if (dataSize > 50)
                 {
                     Console.Write("...");
                 }*/
            Console.WriteLine();

            // 수신된 CRC 추출 (올바른 위치)
            ushort receivedCRC = (ushort)((packetData[dataSize] << 8) | packetData[dataSize + 1]);

            // CRC 계산
            ushort calculatedCRC = CalculateCRC16(data);

            // 타임스탬프 복원
            byte[] dtTimestamp = new byte[8];
            Array.Copy(packetData, dataSize + 2, dtTimestamp, 0, 8);
            DateTime dtReceive;



            long ticks = BitConverter.ToInt64(dtTimestamp, 0);
            /*              if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                          {
                              Console.WriteLine($"경고: 타임스탬프 범위 초과");
                              dtReceive = DateTime.MinValue;
                          }
                          else*/
            {
                dtReceive = DateTime.FromBinary(ticks);
            }


            // 시간 계산
            TimeSpan span = TimeSpan.Zero;
            span = dtCurrent - dtReceive;
            Console.WriteLine($"송신 시간: {dtReceive:HH:mm:ss.fff} ,수신 시간 {dtCurrent:HH:mm:ss.fff}  ");
            Console.WriteLine($"소요시간: {span.TotalMilliseconds:F3} ms");

            // CRC 검증
            bool crcMatch = (calculatedCRC == receivedCRC);
            /*           if (crcMatch)
                       {
                           Console.ForegroundColor = ConsoleColor.Green;
                           Console.WriteLine($"✓ CRC 일치: 0x{calculatedCRC:X4}");
                           Console.ResetColor();
                       }*/
            if (!crcMatch)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ CRC 불일치: 계산=0x{calculatedCRC:X4}, 수신=0x{receivedCRC:X4}");
                Console.ResetColor();
            }

            // CSV 리포트 저장
            if (dtReceive != DateTime.MinValue)
            {
                //CSVReport(crcMatch, span.TotalMilliseconds, dtCurrent, dtReceive);
            }

            // 에코 응답
            byte[] crcBin = new byte[2];
            crcBin[0] = (byte)(calculatedCRC >> 8);
            crcBin[1] = (byte)(calculatedCRC & 0xFF);

            /*string response = $"서버 응답: {crcBin[0]:X2} {crcBin[1]:X2}";
            byte[] responseData = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseData, 0, responseData.Length);*/
        }
        catch (Exception ex)
        {
            Console.WriteLine($"패킷 처리 오류: {ex.Message}");
        }
    }

    private void CSVReport(bool suc, double duration, DateTime receiveDT, DateTime sendDT)
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string reportPath = Path.Combine(baseDirectory, "Reports.csv");

        try
        {
            bool fileExists = File.Exists(reportPath);

            using (StreamWriter writer = new StreamWriter(reportPath, append: true, Encoding.UTF8))
            {
                // 헤더 작성 (파일이 없을 때)
                if (!fileExists)
                {
                    writer.WriteLine("Success,Duration(ms),ReceiveTime,SendTime");
                }

                // 데이터 작성
                string line = $"{suc},{duration:F3},{receiveDT:yyyy-MM-dd HH:mm:ss.fff},{sendDT:yyyy-MM-dd HH:mm:ss.fff}";
                writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CSV 저장 오류: {ex.Message}");
        }
    }

    private ushort CalculateCRC16(byte[] data)
    {
        ushort crc = 0xFFFF;

        for (int i = 0; i < data.Length; i++)
        {
            crc ^= (ushort)(data[i] << 8);

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ 0x1021);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }

    public void StopServer()
    {
        isRunning = false;
        listener?.Stop();
        Console.WriteLine("서버가 중지되었습니다.");
    }
}

// 서버 실행 예제
