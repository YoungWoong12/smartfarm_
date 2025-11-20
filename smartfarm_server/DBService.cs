using MySql.Data.MySqlClient;
using System;

namespace smartfarm_Server
{
    public class DBService
    {
        // ===== DB 연결 문자열 =====
        public static readonly string _connStr =
            "Server=localhost;Database=smartfarm;Uid=root;Pwd=1234;";

        // =========================================================
        //  DB에 온도 저장
        //  - temperature_log : 항상 저장
        //  - temp_alarm_log  : 30도 이상(HIGH) 또는 20도 이하(LOW)일 때만 추가 저장
        //    status 컬럼에 "HIGH" / "LOW" 값 기록
        // =========================================================
        public static void SaveTemperatureToDB(string sensor, float temp)
        {
            try
            {
                // 소수점 1자리로 맞춰서 저장 (DECIMAL(4,1) 맞춰주기)
                float roundedTemp = (float)Math.Round(temp, 1);

                using (var conn = new MySqlConnection(_connStr))
                {
                    conn.Open();

                    // 1) 기본 로그 기록 (항상)
                    string sqlLog =
                        "INSERT INTO temperature_log(sensor_name, temperature) " +
                        "VALUES(@sensor, @temp)";
                    using (var cmd = new MySqlCommand(sqlLog, conn))
                    {
                        cmd.Parameters.AddWithValue("@sensor", sensor);
                        cmd.Parameters.AddWithValue("@temp", roundedTemp);
                        cmd.ExecuteNonQuery();
                    }

                    // 2) 알람 상태 계산
                    string status = null;

                    if (roundedTemp >= 30.0f)
                    {
                        status = "HIGH";   // 30도 이상
                    }
                    else if (roundedTemp <= 20.0f)
                    {
                        status = "LOW";    // 20도 이하
                    }

                    // 3) HIGH / LOW 인 경우에만 알람 테이블에 추가 저장
                    if (status != null)
                    {
                        string sqlAlarm =
                            "INSERT INTO temp_alarm_log(sensor_name, temperature, status) " +
                            "VALUES(@sensor2, @temp2, @status)";
                        using (var cmd2 = new MySqlCommand(sqlAlarm, conn))
                        {
                            cmd2.Parameters.AddWithValue("@sensor2", sensor);
                            cmd2.Parameters.AddWithValue("@temp2", roundedTemp);
                            cmd2.Parameters.AddWithValue("@status", status);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] DB 저장 실패 : {ex.Message}");
            }
        }
    }
}
