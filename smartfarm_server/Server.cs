// Server.cs
// 아두이노(시리얼)에서 온도 데이터를 받아 DB 저장 + WPF 클라이언트로 JSON 전송하는 서버 클래스

using System;                                  // 기본 타입, 예외 처리 등 사용
using System.Collections.Generic;              // List<T> 사용
using System.IO.Ports;                         // SerialPort 사용
using System.Net;                              // IPAddress 사용
using System.Net.Sockets;                      // TcpListener, TcpClient 사용
using System.Text;                             // Encoding 사용
using System.Threading.Tasks;                  // 비동기 처리(Task) 사용
using MySql.Data.MySqlClient;                  // (QueryTemperature에서 MySql 사용 시)
using Newtonsoft.Json;                         // JSON 직렬화/역직렬화 라이브러리 사용
using smartfarm_Server.Models;                 // Json.cs에 정의한 DTO(SensorPacket 등) 사용

namespace smartfarm_Server                     // 서버 프로젝트 네임스페이스
{
    public class Server                        // 서버 기능을 담당하는 클래스
    {
        // ===== TCP 관련 필드 =====
        private readonly int _tcpPort;         // 클라이언트와 통신할 TCP 포트 번호
        private TcpListener _listener;         // 클라이언트 접속을 받는 TcpListener
        private readonly List<TcpClient> _clients = new List<TcpClient>(); // 현재 접속 중인 클라이언트 리스트
        private readonly object _lock = new object();      // _clients 동시 접근 방지용 lock 오브젝트

        // ===== 시리얼(아두이노) 관련 필드 =====
        private readonly string _serialPortName;           // 아두이노가 연결된 COM 포트 이름 (예: "COM3")
        private readonly int _baudRate;                    // 통신 속도 (보레이트, 예: 9600)
        private SerialPort _serialPort;                    // 시리얼 포트 객체

        // ===== 최근 수신된 센서 값 저장용 =====
        private float _lastTemp1 = float.NaN;              // Temp1(센서1) 마지막 값 (초기값은 NaN)
        private float _lastTemp2 = float.NaN;              // Temp2(센서2) 마지막 값 (초기값은 NaN)

        // ===== 생성자 =====
        public Server(int tcpPort, string serialPortName, int baudRate = 9600) // TCP 포트, 시리얼 포트 이름, 보레이트 지정
        {
            _tcpPort = tcpPort;                            // TCP 포트 설정
            _serialPortName = serialPortName;              // 시리얼 포트 이름 설정
            _baudRate = baudRate;                          // 보레이트 설정
        }

        // ===== 서버 시작 (시리얼 + TCP 리스닝) =====
        public async Task StartAsync()                     // 비동기로 서버를 시작하는 메서드
        {
            // 1) 시리얼 포트 오픈
            OpenSerialPort();                              // 아두이노 연결용 시리얼 포트 열기

            // 2) TCP 서버 시작 (클라이언트 접속 대기)
            _listener = new TcpListener(IPAddress.Any, _tcpPort); // 모든 IP에서 _tcpPort로 들어오는 연결 받기
            _listener.Start();                             // 리스너 시작
            Console.WriteLine($"[SERVER] Tcp 서버 가동  Port={_tcpPort}"); // 로그 출력

            // 3) 무한 루프로 클라이언트 접속 처리
            while (true)                                   // 서버가 살아 있는 동안 반복
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(); // 클라이언트 접속 대기 (비동기)
                Console.WriteLine("[SERVER] 클라 연결 .");  // 접속 로그 출력

                lock (_lock)                               // 클라이언트 리스트 보호
                {
                    _clients.Add(client);                  // 리스트에 새 클라이언트 추가
                }

                _ = HandleClientAsync(client);             // 클라이언트 하나를 별도 비동기 작업으로 처리
            }
        }

        // ===== 시리얼 포트 열기 =====
        private void OpenSerialPort()                      // 아두이노와의 시리얼 통신을 위한 포트 오픈 메서드
        {
            try                                             // 예외 처리를 위해 try 사용
            {
                _serialPort = new SerialPort(_serialPortName, _baudRate); // 포트 이름과 보레이트로 SerialPort 생성
                _serialPort.Encoding = Encoding.UTF8;       // 문자열 인코딩을 UTF8로 설정
                _serialPort.NewLine = "\n";                 // ReadLine 시 줄바꿈 기준을 '\n'으로 설정
                _serialPort.DataReceived += SerialDataReceived; // 데이터 수신 시 호출될 이벤트 핸들러 등록
                _serialPort.Open();                         // 시리얼 포트 열기

                Console.WriteLine($"[SERVER] 시리얼 연결  {_serialPortName} @ {_baudRate}"); // 성공 로그 출력
            }
            catch (Exception ex)                            // 예외 발생 시
            {
                Console.WriteLine($"[SERVER] 시리얼 연결 실패 : {ex.Message}"); // 실패 로그 출력
            }
        }

        // ===== 아두이노에서 한 줄 데이터가 들어왔을 때 호출되는 이벤트 핸들러 =====
        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e) // 시리얼 수신 이벤트 핸들러
        {
            try                                             // 수신 및 파싱 중 예외 처리
            {
                string line = _serialPort.ReadLine().Trim(); // 한 줄 읽어서 앞뒤 공백 제거 (예: "Temp1:24.6")
                Console.WriteLine($"[SERVER] 시리얼 수신 : {line}"); // 수신 로그 출력

                // "Temp1:" 또는 "Temp2:"로 시작하는지 확인하여 각 센서 값 업데이트
                if (line.StartsWith("Temp1:", StringComparison.OrdinalIgnoreCase)) // Temp1 데이터인지 확인
                {
                    string valueStr = line.Substring("Temp1:".Length); // "Temp1:" 이후의 숫자 부분만 추출
                    if (float.TryParse(valueStr, out float t1))        // float으로 파싱 시도
                    {
                        _lastTemp1 = t1;                               // 파싱 성공 시 _lastTemp1에 저장
                    }
                }
                else if (line.StartsWith("Temp2:", StringComparison.OrdinalIgnoreCase)) // Temp2 데이터인지 확인
                {
                    string valueStr = line.Substring("Temp2:".Length); // "Temp2:" 이후 숫자 부분만 추출
                    if (float.TryParse(valueStr, out float t2))        // float으로 파싱 시도
                    {
                        _lastTemp2 = t2;                               // 파싱 성공 시 _lastTemp2에 저장
                    }
                }

                // 두 센서 값이 모두 유효(NaN이 아님)할 때만 DB 저장 및 JSON 전송
                if (!float.IsNaN(_lastTemp1) && !float.IsNaN(_lastTemp2)) // 두 값 모두 유효한지 확인
                {
                    // 1) DB 저장 (센서1 → "A", 센서2 → "B")
                    DBService.SaveTemperatureToDB("A", _lastTemp1);   // 센서 A 온도 저장
                    DBService.SaveTemperatureToDB("B", _lastTemp2);   // 센서 B 온도 저장

                    // 2) JSON 객체 생성
                    var packet = new SensorPacket                     // Json.cs에 정의한 SensorPacket 객체 생성
                    {
                        sensor1 = _lastTemp1,                         // sensor1 필드에 Temp1 값 대입
                        sensor2 = _lastTemp2                          // sensor2 필드에 Temp2 값 대입
                    };

                    // 3) JSON 직렬화 (예: {"sensor1":23.5,"sensor2":30.1})
                    string json = JsonConvert.SerializeObject(packet); // SensorPacket을 JSON 문자열로 변환

                    // 4) 현재 접속 중인 모든 클라이언트에게 JSON 전송
                    BroadcastToClients(json);                         // 브로드캐스트 메서드 호출
                }
            }
            catch (Exception ex)                                      // 수신/파싱 중 예외 발생 시
            {
                Console.WriteLine($"[SERVER] 아두이노 읽기 실패 : {ex.Message}"); // 오류 메시지 출력
            }
        }

        // ===== 특정 기간 온도 조회 (히스토리 요청용, 필요하면 클라에서 사용 가능) =====
        // 클라이언트에서 "REQ:SELECT:시작시간|끝시간" 포맷으로 요청한다고 가정
        private string QueryTemperature(string start, string end)     // DB에서 특정 기간 온도 조회 메서드
        {
            var rows = new List<string>();                            // 결과 행들을 저장할 리스트

            try                                                       // 예외 처리
            {
                using (var conn = new MySqlConnection(DBService._connStr)) // DB 연결 생성
                {
                    conn.Open();                                      // 연결 오픈

                    string sql =
                        "SELECT sensor_name, temperature, created_at " +
                        "FROM temperature_log " +
                        "WHERE created_at BETWEEN @s AND @e " +
                        "ORDER BY created_at ASC";                    // 기간 내의 데이터를 시간순으로 조회하는 SQL

                    using (var cmd = new MySqlCommand(sql, conn))     // 커맨드 객체 생성
                    {
                        cmd.Parameters.AddWithValue("@s", start);     // 시작 시간 파라미터 바인딩
                        cmd.Parameters.AddWithValue("@e", end);       // 끝 시간 파라미터 바인딩

                        using (var reader = cmd.ExecuteReader())      // 쿼리 실행 후 리더로 결과 읽기
                        {
                            while (reader.Read())                     // 결과 행 반복
                            {
                                string row =
                                    $"{reader["sensor_name"]},{reader["temperature"]},{reader["created_at"]}"; // "A,24.6,시간" 형식으로 문자열 구성
                                rows.Add(row);                        // 리스트에 추가
                            }
                        }
                    }
                }
            }
            catch (Exception ex)                                      // 예외 발생 시
            {
                Console.WriteLine($"[SERVER] DB 조회 실패 : {ex.Message}"); // 오류 로그 출력
            }

            return string.Join("|", rows);                            // 각 행을 '|'로 이어붙여 문자열 반환
        }

        // ===== 모든 클라이언트에게 메시지(여기서는 JSON)를 전송 =====
        private void BroadcastToClients(string message)               // 현재 접속 중인 모든 클라이언트에게 전송하는 메서드
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");     // 문자열을 UTF8 바이트 배열로 변환 + 줄바꿈 추가

            lock (_lock)                                              // 클라이언트 리스트 동기화
            {
                List<TcpClient> dead = new List<TcpClient>();         // 끊어진 클라이언트를 따로 모을 리스트

                foreach (var client in _clients)                      // 현재 접속 중인 모든 클라이언트 순회
                {
                    try                                              // 전송 시 예외 처리
                    {
                        NetworkStream ns = client.GetStream();       // 네트워크 스트림 얻기
                        ns.Write(data, 0, data.Length);              // 데이터 전송
                        ns.Flush();                                  // 버퍼 비우기
                    }
                    catch                                            // 전송 실패 시
                    {
                        dead.Add(client);                            // dead 리스트에 추가 (나중에 제거)
                    }
                }

                // 전송 실패한(끊어진) 클라이언트 정리
                foreach (var d in dead)                              // dead 리스트 순회
                {
                    _clients.Remove(d);                              // _clients에서 제거
                    try { d.Close(); } catch { }                     // 소켓 닫기 시도
                }
            }
        }

        // ===== 클라이언트 개별 처리 (요청 수신) =====
        private async Task HandleClientAsync(TcpClient client)       // 각 클라이언트별로 실행되는 비동기 메서드
        {
            NetworkStream ns = client.GetStream();                   // 네트워크 스트림 획득
            byte[] buffer = new byte[1024];                          // 수신 버퍼

            try                                                      // 수신/처리 구간 예외 처리
            {
                while (true)                                         // 클라이언트 연결 유지 동안 반복
                {
                    int len = await ns.ReadAsync(buffer, 0, buffer.Length); // 데이터 수신 (비동기)
                    if (len == 0) break;                             // 길이가 0이면 연결 종료로 간주하고 탈출

                    string msg = Encoding.UTF8.GetString(buffer, 0, len).Trim(); // 수신된 바이트를 문자열로 변환
                    Console.WriteLine($"[SERVER] 클라에서 수신 : {msg}");        // 로그 출력

                    if (msg.StartsWith("REQ:SELECT:"))               // 히스토리 조회 요청인지 확인
                    {
                        string payload = msg.Substring("REQ:SELECT:".Length); // "REQ:SELECT:" 뒤의 본문 부분 추출
                        string[] range = payload.Split('|');         // "시작|끝" 형태를 '|' 기준으로 분리

                        if (range.Length == 2)                       // 시작/끝 두 개가 맞게 들어왔는지 확인
                        {
                            string start = range[0];                 // 시작 시간 문자열
                            string end = range[1];                   // 끝 시간 문자열

                            string result = QueryTemperature(start, end); // DB에서 해당 기간 조회

                            byte[] outBytes = Encoding.UTF8.GetBytes(result + "\n"); // 결과를 바이트 배열로 변환
                            await ns.WriteAsync(outBytes, 0, outBytes.Length);       // 클라이언트에 전송
                            await ns.FlushAsync();                                    // flush
                        }
                    }
                    else
                    {
                        // 이외의 명령을 처리하고 싶을 때 이 블록 안에 작성
                    }
                }
            }
            catch (Exception ex)                                     // 통신 중 예외 발생 시
            {
                Console.WriteLine($"[SERVER] 클라 예외 : {ex.Message}"); // 로그 출력
            }

            Console.WriteLine("[SERVER] 클라 종료 .");               // 클라이언트 종료 로그

            lock (_lock)                                             // _clients 리스트 보호
            {
                _clients.Remove(client);                             // 리스트에서 제거
            }

            client.Close();                                          // 소켓 닫기
        }

        // ===== 서버 종료 처리 =====
        public void Stop()                                           // 서버를 정리하며 종료시키는 메서드
        {
            try
            {
                _listener?.Stop();                                   // TcpListener 중지
            }
            catch { }

            try
            {
                _serialPort?.Close();                                // 시리얼 포트 닫기
            }
            catch { }

            lock (_lock)                                             // 클라이언트 리스트 보호
            {
                foreach (var c in _clients)                          // 모든 클라이언트 순회
                {
                    try { c.Close(); } catch { }                     // 각 클라이언트 연결 닫기
                }
                _clients.Clear();                                    // 리스트 비우기
            }
        }
    }
}
