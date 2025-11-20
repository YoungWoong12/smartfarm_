// Json.cs (Server)
// JSON 관련 DTO 클래스들을 관리하는 파일

using System;                         // DateTime 형 사용을 위해 System 네임스페이스 포함

namespace smartfarm_Server.Models     // 서버 프로젝트 네임스페이스 + Models 서브네임스페이스
{
    /// <summary>
    /// 아두이노에서 들어온 두 개의 센서 값을 JSON 형태로 표현할 때 사용하는 DTO
    /// 예: {"sensor1":23.5,"sensor2":30.1}
    /// </summary>
    public class SensorPacket         // 센서 패킷 정보를 담는 클래스
    {
        public float sensor1 { get; set; }  // 첫 번째 센서(Temp1) 값
        public float sensor2 { get; set; }  // 두 번째 센서(Temp2) 값
    }

    /// <summary>
    /// DB에서 조회된 온도 기록을 JSON으로 내려줄 때 사용할 수 있는 DTO (현재는 확장용)
    /// 예: {"sensor":"A","temperature":23.5,"time":"2025-01-08T12:30:00"}
    /// </summary>
    public class TemperatureRecord    // 온도 기록 정보를 담는 클래스
    {
        public string sensor { get; set; }     // 센서 이름 (A, B 등)
        public float temperature { get; set; } // 온도 값
        public DateTime time { get; set; }     // 측정 시각
    }
}
