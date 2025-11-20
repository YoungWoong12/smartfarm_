// Connect.cs
// WPF 클라이언트에서 서버(TCP)에 연결하기 위한 헬퍼 클래스

using System;                              // 예외 처리, 기본 타입 사용
using System.Net.Sockets;                  // TcpClient 사용

namespace ServerConnecting                  // 서버 연결용 네임스페이스
{
    public static class Connect             // 서버 연결을 담당하는 정적 클래스
    {
        private static TcpClient _client;   // 실제 TCP 연결을 담당하는 TcpClient 인스턴스

        public static TcpClient Client => _client; // 외부에서 TcpClient에 접근할 수 있도록 프로퍼티 제공

        public static bool ConnectToServer(string ip, int port) // 서버에 연결을 시도하는 메서드
        {
            try                                 // 예외 처리
            {
                _client = new TcpClient();      // TcpClient 객체 생성
                _client.Connect(ip, port);      // 지정한 IP, 포트로 연결 시도
                return true;                    // 성공 시 true 반환
            }
            catch (Exception ex)                // 연결 실패 시
            {
                Console.WriteLine($"서버 연결 실패: {ex.Message}"); // 실패 로그 출력
                return false;                   // false 반환
            }
        }

        public static void Close()             // 서버와의 연결을 종료하는 메서드
        {
            try
            {
                _client?.Close();              // TcpClient가 있으면 Close 호출
            }
            catch
            {
                // 예외는 무시
            }
        }
    }
}
