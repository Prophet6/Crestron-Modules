using System;
using System.Text;
using Crestron.SimplSharp;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Trace = Crestron.SimplSharp;

namespace CrestronMqttWrapper
{
    public class CrestronMqttClient
    {
        private MqttClient _client;

        // Events exposed to SIMPL+
        public event Action<bool> ConnectionStateChanged;
        public event Action<string, string> MessageReceived;

        public bool IsConnected => _client != null && _client.IsConnected;

        public void Initialize(string broker, int port, string clientId,
                               string username = "", string password = "",
                               bool useTls = false)
        {
            try
            {
                _client = new MqttClient(broker, port, useTls, null, null, MqttSslProtocols.None);

                _client.MqttMsgPublishReceived += OnMqttMsgPublishReceived;
                _client.ConnectionClosed += OnConnectionClosed;

                CrestronConsole.PrintLine("[MQTT] Client initialized for {0}:{1}", broker, port);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[MQTT] Initialize error: " + ex.Message);
            }
        }

        private void OnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string payload = Encoding.UTF8.GetString(e.Message, 0, e.Message.Length);

            MessageReceived?.Invoke(topic, payload);
        }

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            CrestronConsole.PrintLine("[MQTT] Connection closed");
            ConnectionStateChanged?.Invoke(false);
        }

        public void Connect(string username = "", string password = "")
        {
            if (_client == null)
            {
                CrestronConsole.PrintLine("[MQTT] Client not initialized.");
                return;
            }

            try
            {
                byte code = _client.Connect(Guid.NewGuid().ToString(), username, password);

                if (_client.IsConnected)
                {
                    CrestronConsole.PrintLine("[MQTT] Connected successfully. Return code: " + code);
                    ConnectionStateChanged?.Invoke(true);
                }
                else
                {
                    CrestronConsole.PrintLine("[MQTT] Connection failed. Return code: " + code);
                }
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[MQTT] Connect error: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            if (_client != null && _client.IsConnected)
            {
                try
                {
                    _client.Disconnect();
                    CrestronConsole.PrintLine("[MQTT] Disconnected");
                    ConnectionStateChanged?.Invoke(false);
                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("[MQTT] Disconnect error: " + ex.Message);
                }
            }
        }

        public void Publish(string topic, string payload, int qos = 0, bool retain = false)
        {
            if (_client == null || !_client.IsConnected)
            {
                CrestronConsole.PrintLine("[MQTT] Not connected. Cannot publish.");
                return;
            }

            try
            {
                _client.Publish(topic, Encoding.UTF8.GetBytes(payload), (byte)qos, retain);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[MQTT] Publish error: " + ex.Message);
            }
        }

        public void Subscribe(string topic, int qos = 0)
        {
            if (_client == null || !_client.IsConnected)
            {
                CrestronConsole.PrintLine("[MQTT] Not connected. Cannot subscribe.");
                return;
            }

            try
            {
                _client.Subscribe(new string[] { topic }, new byte[] { (byte)qos });
                CrestronConsole.PrintLine("[MQTT] Subscribed to: " + topic);
            }
            catch (Exception ex)
            {
                CrestronConsole.PrintLine("[MQTT] Subscribe error: " + ex.Message);
            }
        }
    }
}
