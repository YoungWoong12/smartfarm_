// TempRecord.cs
// 클라이언트 내부에서 온도 데이터를 보관할 때 사용할 수 있는 간단한 클래스

using System;                           // DateTime 사용

namespace ServerConnecting              // 기존 코드와 동일한 네임스페이스 유지
{
    public class TempRecord             // 한 건의 온도 기록을 나타내는 클래스
    {
        public string Sensor { get; set; }      // 센서 이름 (A, B 등)
        public float Temperature { get; set; }  // 온도 값
        public DateTime Time { get; set; }      // 시간
    }
}
