#include <DHT.h>

#define DHTPIN1 10
#define DHTPIN2 11
#define DHTTYPE1 DHT11
#define DHTTYPE2 DHT11
DHT dht1(DHTPIN1, DHTTYPE1);
DHT dht2(DHTPIN2, DHTTYPE2);

void setup() {
  Serial.begin(9600);
  dht1.begin();
  dht2.begin();
}

void loop() {
  float t1 = dht1.readTemperature();
  float t2 = dht2.readTemperature();

  if (!isnan(t1)&&!isnan(t2)) {
    Serial.print("Temp1:");
    Serial.println(t1,1);  // 예: 24.6
    Serial.print("Temp2:");
    Serial.println(t2,2);
    
  }

  delay(5000);

}
