using LiveCharts;
using LiveCharts.Wpf;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using ServerConnecting;
using smartfarm_client.Models;   // SensorPacket 정의
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace smartfarm_client
{
    public partial class MainWindow : Window
    {
        // ================== 실시간 차트 용 ==================
        public ChartValues<double> Temp1Values = new ChartValues<double>();   // 센서1
        public ChartValues<double> Temp2Values = new ChartValues<double>();   // 센서2
        public List<DateTime> TimeList = new List<DateTime>();                // 시간 라벨

        // ================== 분석 탭 통계 DTO ==================
        public class SensorStats
        {
            public string Sensor { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public double Avg { get; set; }
        }

        // ================== 생성자 ==================
        public MainWindow()
        {
            InitializeComponent();

            // ----- 차트 시리즈 설정 (Temp1 파랑, Temp2 주황) -----
            TemperatureChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Temp1",
                    Values = Temp1Values,
                    StrokeThickness = 3,
                    PointGeometrySize = 0,
                    LineSmoothness = 0,
                    Stroke = Brushes.DeepSkyBlue
                },
                new LineSeries
                {
                    Title = "Temp2",
                    Values = Temp2Values,
                    StrokeThickness = 3,
                    PointGeometrySize = 0,
                    LineSmoothness = 0,
                    Stroke = Brushes.Orange
                }
            };

            // ----- 축 설정 (가독성 업) -----
            // Y축: 숫자만 크게, 제목은 XAML의 TextBlock(Temperature (℃))로 표시
            TemperatureChart.AxisY.Clear();
            TemperatureChart.AxisY.Add(new Axis
            {
                Foreground = Brushes.White,
                FontSize = 16,                          // Y축 숫자 크게
                LabelFormatter = v => v.ToString("0.0") // 소수점 1자리
            });

            // X축: Time 제목 + 숫자 크게 (TitleFontSize 같은 거 없음)
            TemperatureChart.AxisX.Clear();
            TemperatureChart.AxisX.Add(new Axis
            {
                Title = "Time",
                Foreground = Brushes.White,
                Labels = new List<string>(),
                FontSize = 16       // 아래 시간 숫자 + 제목 둘 다 이 크기로
            });

            // ----- 서버 연결 후 수신 시작 -----
            if (Connect.ConnectToServer("127.0.0.1", 6000))
            {
                StartReceive();
            }
            else
            {
                MessageBox.Show("서버 연결 실패", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================== 실시간 수신 루프 ==================
        private async void StartReceive()
        {
            NetworkStream ns = Connect.Client.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int len = await ns.ReadAsync(buffer, 0, buffer.Length);
                    if (len == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, len).Trim();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateUI(data);
                    });
                }
            }
            catch
            {
                // 필요하면 로그
            }
        }

        // ================== 실시간 차트 / 현재온도 UI ==================
        private void UpdateUI(string json)
        {
            SensorPacket packet;
            try
            {
                packet = JsonConvert.DeserializeObject<SensorPacket>(json);
            }
            catch
            {
                return;
            }

            if (packet == null) return;

            // 센서 값 소수점 1자리로 맞추기
            double t1 = Math.Round(packet.sensor1, 1);
            double t2 = Math.Round(packet.sensor2, 1);

            // TEMP1 / TEMP2 각각 표시
            txtTemp1.Text = $"{t1:F1} ℃";
            txtTemp2.Text = $"{t2:F1} ℃";

            // 차트 값 추가
            Temp1Values.Add(t1);
            Temp2Values.Add(t2);

            DateTime now = DateTime.Now;
            TimeList.Add(now);
            TemperatureChart.AxisX[0].Labels.Add(now.ToString("HH:mm:ss"));

            // ================= 알람 버튼 (Temp1 / Temp2 둘 다 체크) =================
            // 조건 : Temp1 또는 Temp2 가 20도 이하이거나 30도 이상이면 ALARM
            if (t1 <= 20.0 || t1 >= 30.0 ||
                t2 <= 20.0 || t2 >= 30.0)
            {
                AlarmButton.Background = new SolidColorBrush(Colors.DarkRed);
                AlarmButton.Content = "ALARM";
            }
            else
            {
                AlarmButton.Background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
                AlarmButton.Content = "Alarm";
            }
        }



        // =================================================================
        //                     분석 탭 : 조회 버튼 (dpDay 하루만)
        // =================================================================
        private void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            // DatePicker 하나(dpDay)만 사용
            if (dpDay.SelectedDate == null)
            {
                MessageBox.Show("날짜를 선택하세요.");
                return;
            }

            DateTime day = dpDay.SelectedDate.Value;
            string start = day.ToString("yyyy-MM-dd 00:00:00");
            string end = day.ToString("yyyy-MM-dd 23:59:59");

            var A = new List<double>();
            var B = new List<double>();
            

            string connStr = "Server=localhost;Database=smartfarm;Uid=root;Pwd=1234;";

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "SELECT sensor_name, temperature " +
                             "FROM temperature_log " +
                             "WHERE created_at BETWEEN @s AND @e";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@s", start);
                    cmd.Parameters.AddWithValue("@e", end);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sensor = reader["sensor_name"].ToString();
                            double temp = Convert.ToDouble(reader["temperature"]);

                            if (sensor == "A") A.Add(temp);
                            if (sensor == "B") B.Add(temp);
                            
                        }
                    }
                }
            }

            // 이 날 하루 데이터만으로 통계 계산
            var result = new List<SensorStats>
            {
                MakeStats("A", A),
                MakeStats("B", B),
                
            };

            dgResult.ItemsSource = result;

            // 파이 차트도 이 날 기준
            pieA.Series = MakePie(CalcRate(A));
            pieB.Series = MakePie(CalcRate(B));
            
        }

        // ================== 통계/파이차트 유틸 ==================
        private SeriesCollection MakePie(double rate)
        {
            return new SeriesCollection
            {
                new PieSeries
                {
                    Title = "OK",
                    Values = new ChartValues<double> { rate },
                    Fill = Brushes.LightGreen
                },
                new PieSeries
                {
                    Title = "NG",
                    Values = new ChartValues<double> { 100 - rate },
                    Fill = Brushes.IndianRed
                }
            };
        }

        private SensorStats MakeStats(string name, List<double> list)
        {
            if (list.Count == 0)
                return new SensorStats { Sensor = name, Min = 0, Max = 0, Avg = 0 };

            return new SensorStats
            {
                Sensor = name,
                Min = Math.Round(list.Min(), 1),
                Max = Math.Round(list.Max(), 1),
                Avg = Math.Round(list.Average(), 2)   // 평균 소수 둘째자리까지
            };
        }

        private double CalcRate(List<double> list)
        {
            if (list.Count == 0) return 0;

            // 예: 20~30도 범위를 OK로 본다
            int ok = list.Count(v => v >= 20 && v <= 30);
            return Math.Round(ok * 100.0 / list.Count, 1);
        }
    }
}
