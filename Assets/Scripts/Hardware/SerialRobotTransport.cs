using System;
using System.IO.Ports;
using UnityEngine;

namespace ARMIS.Hardware
{
    /// <summary>
    /// Sends B{n}S{n}E{n}G{n} packets to the Arduino over USB serial.
    /// Rate-limited to commandRateHz so a fast Unity Update loop cannot
    /// flood the serial buffer. All serial exceptions are caught here —
    /// a dropped USB cable must never throw out of Update().
    /// </summary>
    public class SerialRobotTransport : IRobotTransport
    {
        readonly SerialPort _port;
        readonly float _minIntervalSeconds;
        float _sinceLastSend;
        float _sinceLastSuccess;
        bool _connected;

        public bool IsConnected => _connected;
        public string StatusText { get; private set; } = "Disconnected";
        public float TimeSinceLastSuccessfulSend => _sinceLastSuccess;

        public SerialRobotTransport(string portName, int baudRate, float commandRateHz)
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 200,
                WriteTimeout = 200,
                NewLine = "\n"
            };
            _minIntervalSeconds = 1f / Mathf.Max(1f, commandRateHz);
        }

        public void Connect()
        {
            try
            {
                if (!_port.IsOpen) _port.Open();
                _connected = true;
                StatusText = $"Connected ({_port.PortName} @ {_port.BaudRate})";
                _sinceLastSuccess = 0f;
            }
            catch (Exception ex)
            {
                _connected = false;
                StatusText = $"Connect failed: {ex.Message}";
                Debug.LogWarning($"[ARMIS] Serial connect failed: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try { if (_port.IsOpen) _port.Close(); }
            catch (Exception ex) { Debug.LogWarning($"[ARMIS] Serial close error: {ex.Message}"); }
            finally
            {
                _connected = false;
                StatusText = "Disconnected";
            }
        }

        public bool SendPacket(int baseDeg, int shoulderDeg, int elbowDeg, int gripperDeg)
        {
            if (!_connected || !_port.IsOpen) return false;
            if (_sinceLastSend < _minIntervalSeconds) return false; // rate limit — not an error

            string packet = $"B{baseDeg}S{shoulderDeg}E{elbowDeg}G{gripperDeg}\n";
            try
            {
                _port.Write(packet);
                _sinceLastSend = 0f;
                _sinceLastSuccess = 0f;
                return true;
            }
            catch (Exception ex)
            {
                _connected = false;
                StatusText = $"Write failed: {ex.Message}";
                Debug.LogWarning($"[ARMIS] Serial write failed, treating as disconnected: {ex.Message}");
                return false;
            }
        }

        public void Tick(float deltaTime)
        {
            _sinceLastSend += deltaTime;
            if (_connected) _sinceLastSuccess += deltaTime;
        }
    }
}
