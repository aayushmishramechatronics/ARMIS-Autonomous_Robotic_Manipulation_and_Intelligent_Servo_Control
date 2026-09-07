/*
 * ARMIS_Controller.ino
 * Autonomous Robotic Manipulation and Intelligent Servo Control
 *
 * Receives human-readable packets over serial:
 *   B<int>S<int>E<int>G<int>\n
 * e.g. "B90S90E90G90\n"
 *
 * Responsibilities (and ONLY these — Unity owns all planning/IK):
 *   1. Parse packets defensively (malformed input is dropped, never crashes).
 *   2. Independently clamp every joint to its own physical limits.
 *      The Arduino does NOT trust Unity's numbers.
 *   3. Drive four servos.
 *   4. Watchdog: if no valid packet arrives within WATCHDOG_TIMEOUT_MS,
 *      enter a communication-fault state and STOP applying new commands.
 *
 * SAFETY BEHAVIOR ON WATCHDOG TIMEOUT (documented, configurable below):
 *   Default = HOLD_LAST_POSITION. The Servo library keeps sending the last
 *   PWM pulse it was told to hold, so the arm simply stops where it is.
 *   We do NOT snap to 90 degrees or any other "default" pose — an
 *   unrequested motion during a comms fault is its own hazard.
 *
 * NO CHECKSUM: intentionally omitted. At 115200 baud on a short USB link,
 * uncaught byte corruption is rare and sscanf's %d already rejects
 * non-numeric garbage; a checksum would add a second parser to keep in
 * sync with Unity for a failure mode the format-check above already
 * mostly guards against. Revisit if this ships over a noisy RF link
 * instead of USB.
 *
 * NO DYNAMIC MEMORY: fixed-size char buffer only, no String class, no
 * heap allocation in the hot path — avoids AVR heap fragmentation on
 * long-running missions.
 */

#include <Servo.h>

// ---------------- Pin assignment ----------------
const uint8_t PIN_BASE     = 6;
const uint8_t PIN_SHOULDER = 9;
const uint8_t PIN_ELBOW    = 10;
const uint8_t PIN_GRIPPER  = 11;

// ---------------- Per-joint physical limits (degrees) ----------------
// MUST be mirrored by hand in RobotConfiguration.cs (servoMin/servoMax and
// the logical joint limits). Unity and Arduino do not share a config file.
const int JOINT_MIN[4] = {   0,   0,   0,   0 }; // Base, Shoulder, Elbow, Gripper
const int JOINT_MAX[4] = { 180, 180, 180, 180 };

// ---------------- Communication ----------------
const long BAUD_RATE = 115200;
const unsigned long WATCHDOG_TIMEOUT_MS = 500;
const size_t PACKET_BUF_SIZE = 32;

Servo servoBase, servoShoulder, servoElbow, servoGripper;

char packetBuf[PACKET_BUF_SIZE];
uint8_t packetLen = 0;

unsigned long lastValidPacketMillis = 0;
bool inCommFault = false;

int currentAngle[4] = { 90, 90, 90, 90 }; // last angle actually written to each servo

void setup()
{
  Serial.begin(BAUD_RATE);
  Serial.setTimeout(50);

  servoBase.attach(PIN_BASE);
  servoShoulder.attach(PIN_SHOULDER);
  servoElbow.attach(PIN_ELBOW);
  servoGripper.attach(PIN_GRIPPER);

  writeAllServos(currentAngle);
  lastValidPacketMillis = millis();
}

void loop()
{
  readSerialIntoBuffer();
  checkWatchdog();
}

// Reads bytes as they arrive; on newline, attempts to parse a full packet.
// Never blocks — Serial.available()-gated, so loop() stays responsive for
// the watchdog check even mid-packet.
void readSerialIntoBuffer()
{
  while (Serial.available() > 0)
  {
    char c = (char)Serial.read();

    if (c == '\n' || c == '\r')
    {
      if (packetLen > 0)
      {
        packetBuf[packetLen] = '\0';
        handlePacket(packetBuf);
        packetLen = 0;
      }
      continue;
    }

    if (packetLen < PACKET_BUF_SIZE - 1)
    {
      packetBuf[packetLen++] = c;
    }
    else
    {
      // Overlong line: malformed by construction. Drop it and resync on
      // the next newline rather than parsing a truncated, corrupted packet.
      packetLen = 0;
    }
  }
}

void handlePacket(const char *buf)
{
  int b, s, e, g;
  int matched = sscanf(buf, "B%dS%dE%dG%d", &b, &s, &e, &g);

  if (matched != 4)
  {
    // Malformed packet: fail safe by ignoring it entirely. Does NOT reset
    // the watchdog timer — a stream of garbage must not masquerade as a
    // healthy link.
    return;
  }

  int requested[4] = { b, s, e, g };
  int clamped[4];
  for (uint8_t i = 0; i < 4; i++)
  {
    clamped[i] = clampInt(requested[i], JOINT_MIN[i], JOINT_MAX[i]);
  }

  lastValidPacketMillis = millis();

  if (inCommFault)
  {
    // Recovering from a comms fault: resume on the next valid packet
    // rather than staying latched, since a reconnected serial link is
    // itself evidence the fault condition has cleared.
    inCommFault = false;
  }

  writeAllServos(clamped);
}

void checkWatchdog()
{
  unsigned long elapsed = millis() - lastValidPacketMillis;
  if (elapsed > WATCHDOG_TIMEOUT_MS && !inCommFault)
  {
    inCommFault = true;
    // HOLD_LAST_POSITION: intentionally do nothing to the servos here.
    // currentAngle[] already holds the last commanded pose and the Servo
    // library keeps outputting that PWM signal on its own.
  }
}

void writeAllServos(const int angles[4])
{
  servoBase.write(angles[0]);
  servoShoulder.write(angles[1]);
  servoElbow.write(angles[2]);
  servoGripper.write(angles[3]);
  for (uint8_t i = 0; i < 4; i++) currentAngle[i] = angles[i];
}

int clampInt(int v, int lo, int hi)
{
  if (v < lo) return lo;
  if (v > hi) return hi;
  return v;
}
