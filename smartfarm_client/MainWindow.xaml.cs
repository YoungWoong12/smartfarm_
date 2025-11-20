using client;
using LiveCharts;
using LiveCharts.Wpf;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using ServerConnecting;
using smartfarm_client.Models;
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
        // ===== 실시간 차트용 컬렉션 =====
        public ChartValues<double> Temp1Values = new ChartValues<double>();
        public ChartValues<double> Temp2Values = new ChartValues<double>();
        public List<DateTime> TimeList = new List<DateTime>();

        public MainWindow()
        {
            InitializeComponent();

            // -------- 실시간 차트 세팅 --------
            TemperatureChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Temp1",
                    Values = Temp1Values,
                    StrokeThickness = 2,
                    PointGeometrySize = 0,
                    LineSmoothness = 0
                },
                new LineSeries
                {
                    Title = "Temp2",
                    Values = Temp2Values,
                    StrokeThickness = 2,
                    PointGeometrySize = 0,
                    LineSmoothness = 0
                }
            };

            TemperatureChart.AxisY.Add(new Axis
            {
                Title = "Temperature",
                LabelFormatter = v => v.ToString("F1"),
                Foreground = Brushes.White,
                FontSize = 14
            });

            TemperatureChart.AxisX.Add(new Axis
            {
                Title = "Time",
                Labels = new List<string>(),
                Foreground = Brushes.White,
                FontSize = 14
            });

            // 기본 날짜 = 오늘
            dpDay.SelectedDate = DateTime.Today;

            // -------- 서버 연결 --------
            if (Connect.ConnectToServer("127.0.0.1", 6000))
                StartReceive();
        }

        // ==============================================================
        //  실시간 수신 루프
        // ==============================================================
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

                    string json = Encoding.UTF8.GetString(buffer, 0, len).Trim();

                    Application.Current.Dispatcher.Invoke(() => UpdateUI(json));
                }
            }
            catch
            {
                // 끊어져도 일단 무시
            }
        }

        // ==============================================================
        //  실시간 화면 업데이트
        // ==============================================================
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

            double t1 = Math.Round(packet.sensor1, 1);
            double t2 = Math.Round(packet.sensor2, 1);

            // 상단 카드 텍스트
            txtTemp1.Text = $"{t1:F1} ℃";
            txtTemp2.Text = $"{t2:F1} ℃";

            // 차트 데이터 추가
            Temp1Values.Add(t1);
            Temp2Values.Add(t2);

            DateTime now = DateTime.Now;
            TimeList.Add(now);
            TemperatureChart.AxisX[0].Labels.Add(now.ToString("HH:mm:ss"));

            // 카드 배경색 (20~30도 범위 벗어나면 빨간색)
            Brush normalBrush = new SolidColorBrush(Color.FromRgb(34, 34, 34));
            Brush alertBrush = new SolidColorBrush(Color.FromRgb(120, 0, 0));

            CardTemp1.Background = (t1 < 20 || t1 > 30) ? alertBrush : normalBrush;
            CardTemp2.Background = (t2 < 20 || t2 > 30) ? alertBrush : normalBrush;
        }

        // ==============================================================
        //  Analysis 탭용 통계 클래스
        // ==============================================================
        public class SensorStats
        {
            public string Sensor { get; set; } = "";
            public double Min { get; set; }
            public double Max { get; set; }
            public double Avg { get; set; }
        }

        // ==============================================================
        //  조회 버튼 클릭 (하루 기준 통계 + 파이 차트)
        // ==============================================================
        private void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            if (dpDay.SelectedDate == null)
            {
                MessageBox.Show("날짜를 선택하세요.");
                return;
            }

            string start = dpDay.SelectedDate.Value.ToString("yyyy-MM-dd 00:00:00");
            string end = dpDay.SelectedDate.Value.ToString("yyyy-MM-dd 23:59:59");

            var listA = new List<double>();
            var listB = new List<double>();

            string connStr = "Server=localhost;Database=smartfarm;Uid=root;Pwd=1234;";

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql =
                    "SELECT sensor_name, temperature " +
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

                            if (sensor == "A") listA.Add(temp);
                            else if (sensor == "B") listB.Add(temp);
                        }
                    }
                }
            }

            // DataGrid
            var result = new List<SensorStats>
            {
                MakeStats("A", listA),
                MakeStats("B", listB)
            };
            dgResult.ItemsSource = result;

            // 파이 차트
            pieA.Series = MakePie(listA);
            pieB.Series = MakePie(listB);
        }

        private SensorStats MakeStats(string name, List<double> list)
        {
            if (list.Count == 0)
                return new SensorStats { Sensor = name, Min = 0, Max = 0, Avg = 0 };

            return new SensorStats
            {
                Sensor = name,
                Min = list.Min(),
                Max = list.Max(),
                Avg = Math.Round(list.Average(), 1)
            };
        }

        // ==============================================================
        //  파이 차트 생성 (정상 온도 / 이상 온도)
        // ==============================================================
        private SeriesCollection MakePie(List<double> list)
        {
            int total = list.Count;
            int ok = list.Count(v => v >= 20 && v <= 30);
            int ng = total - ok;

            if (total == 0)
            {
                ok = 0;
                ng = 0;
                total = 1;
            }

            return new SeriesCollection
            {
                new PieSeries
                {
                    Title = "정상 온도",
                    Values = new ChartValues<int> { ok },  // 개수
                    Fill = Brushes.LightGreen,
                    DataLabels = true,
                    LabelPoint = cp => $"{cp.Participation * 100:F1}%",  // 파이 안 텍스트
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                },
                new PieSeries
                {
                    Title = "이상 온도",
                    Values = new ChartValues<int> { ng },
                    Fill = Brushes.IndianRed,
                    DataLabels = true,
                    LabelPoint = cp => $"{cp.Participation * 100:F1}%",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                }
            };
        }
    }
}
