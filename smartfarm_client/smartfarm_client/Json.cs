// Json.cs (Client)
// 서버에서 수신하는 JSON 패킷 구조를 정의하는 파일

using System;                              // DateTime 사용

namespace smartfarm_client.Models          // 클라이언트 프로젝트 네임스페이스 + Models
{
    /// <summary>
    /// 서버에서 브로드캐스트하는 센서 값 JSON 패킷
    /// 예: {"sensor1":23.5,"sensor2":30.1}
    /// </summary>
    public class SensorPacket              // 센서 JSON 패킷을 표현하는 클래스
    {
        public float sensor1 { get; set; } // 첫 번째 센서 값
        public float sensor2 { get; set; } // 두 번째 센서 값
    }

    /// <summary>
    /// 서버에서 과거 온도 이력 조회 시(JSON으로 응답하도록 확장할 때) 사용할 수 있는 DTO
    /// </summary>
    public class TemperatureRecord         // 온도 기록 JSON DTO
    {
        public string sensor { get; set; }     // 센서 이름
        public float temperature { get; set; } // 온도 값
        public DateTime time { get; set; }     // 측정 시각
    }
}
