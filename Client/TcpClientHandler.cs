using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

public class TcpClientHandler
{
    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected;
    private const int CHUNK_SIZE = 900;
    private byte[] receiveBuffer = new byte[1024];



    public async Task<bool> ConnectAsync(string serverIp, int port)
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIp, port);
            stream = client.GetStream();
            isConnected = true;

            Console.WriteLine($"{serverIp}:{port}에 연결되었습니다.");

            // 수신 대기 시작
            _ = ReceiveDataAsync();

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
        if (!isConnected || stream == null)
        {
            Console.WriteLine("서버에 연결되어 있지 않습니다.");
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"파일을 찾을 수 없습니다: {filePath}");
            return;
        }

        try
        {
            // 파일을 모두 읽기
            byte[] fileData = await File.ReadAllBytesAsync(filePath);
            long fileSize = fileData.Length;

            Console.WriteLine($"파일 크기: {fileSize} bytes");
            Console.WriteLine($"전송 시작: {Path.GetFileName(filePath)}");

            int offset = 0;
            int chunkNumber = 1;
            int totalChunks = (int)Math.Ceiling((double)fileSize / CHUNK_SIZE);
            Console.WriteLine($"총 {totalChunks}개 청크로 분할 전송");
            DateTime startT = DateTime.Now;
            Console.WriteLine(startT.ToString("g"));
            while (offset < fileSize)
            {
                // 전송할 데이터 크기 계산 (마지막 청크는 900바이트 미만일 수 있음)
                int currentChunkSize = Math.Min(CHUNK_SIZE, (int)(fileSize - offset));

                // 데이터 복사
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fileData, offset, chunk, 0, currentChunkSize);

                // CRC 계산
                /*ushort crc = CalculateCRC16(chunk);

                //DateTime
                DateTime dt = DateTime.Now;
                byte[] dtTimestamp = BitConverter.GetBytes(dt.ToBinary());

                // 전송 패킷 생성: 데이터 + CRC (2바이트)
                byte[] packet = new byte[currentChunkSize + 2 + 8];
                Array.Copy(chunk, 0, packet, 0, currentChunkSize);
                packet[currentChunkSize] = (byte)(crc >> 8);     // CRC 상위 바이트
                packet[currentChunkSize + 1] = (byte)(crc & 0xFF); // CRC 하위 바이트
                for (int i = 0; i < 8; i++)
                {
                    packet[currentChunkSize + 2 + i] = dtTimestamp[i];
                }*/

                // 전송
                await stream.WriteAsync(chunk, 0, chunk.Length);

                //Console.WriteLine($"청크 {chunkNumber}/{totalChunks} 전송 완료 (크기: {currentChunkSize} bytes, CRC: 0x{crc:X4})");
                //Console.WriteLine($"타임스탬프: {dt}");
                offset += currentChunkSize;
                chunkNumber++;
                //Console.WriteLine(chunkNumber);
                // 선택사항: 전송 간 딜레이 (네트워크 부하 조절)
                //await Task.Delay(delay);
            }
            DateTime endT = DateTime.Now;
            Console.WriteLine(endT.ToString("g"));
            TimeSpan span = endT - startT;
            Console.WriteLine($"총 전송 시간: {span.TotalSeconds:F2} 초");
            //await stream.WriteAsync(new byte[1025]);
            Console.WriteLine($"파일 전송 완료: 총 {totalChunks}개 청크");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"파일 전송 오류: {ex.Message}");
        }
    }

    public async Task SendDataAsync(string message)
    {
        if (!isConnected || stream == null)
        {
            Console.WriteLine("서버에 연결되어 있지 않습니다.");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine($"송신: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"전송 오류: {ex.Message}");
        }
    }
    Stream echoSaveStream = new FileStream(CHUNK_SIZE.ToString() + "_echo.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);
    private async Task ReceiveDataAsync()
    {
        try
        {
            while (isConnected)
            {
                int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);

                if (bytesRead == 0)
                {
                    Console.WriteLine("서버와의 연결이 끊어졌습니다.");
                    break;
                }

                //string receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
                //Console.WriteLine($"수신: {receivedData}");
                await echoSaveStream.WriteAsync(receiveBuffer, 0, bytesRead);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"수신 오류: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    // CRC-16 계산 (CCITT 방식)
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

    public void Disconnect()
    {
        isConnected = false;
        stream?.Close();
        client?.Close();
        Console.WriteLine("연결이 종료되었습니다.");
    }
}

