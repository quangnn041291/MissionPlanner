using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    /// <summary>
    /// Collects what the autopilot sends during an onboard compass calibration, and explains why one failed
    /// </summary>
    public class MagCalMonitor
    {
        private readonly object locker = new object();

        private readonly Dictionary<byte, MAVLink.mavlink_mag_cal_progress_t> progress = new Dictionary<byte, MAVLink.mavlink_mag_cal_progress_t>();
        private readonly Dictionary<byte, MAVLink.mavlink_mag_cal_report_t> reports = new Dictionary<byte, MAVLink.mavlink_mag_cal_report_t>();
        private readonly Dictionary<byte, byte> attempts = new Dictionary<byte, byte>();
        private readonly List<string> messages = new List<string>();

        private int packetsub1;
        private int packetsub2;
        private int packetsub3;
        private bool subscribed;

        public List<MAVLink.mavlink_mag_cal_progress_t> Progress
        {
            get { lock (locker) return progress.Values.ToList(); }
        }

        public List<MAVLink.mavlink_mag_cal_report_t> Reports
        {
            get { lock (locker) return reports.Values.ToList(); }
        }

        /// <summary>
        /// the autopilot's own calibration messages, and the attempts it has retried
        /// </summary>
        public List<string> Messages
        {
            get { lock (locker) return messages.ToList(); }
        }

        /// <summary>
        /// clear what an earlier calibration left and start listening, call this before the start command so its
        /// reason for refusing is caught too
        /// </summary>
        public void Start()
        {
            Stop();

            lock (locker)
            {
                progress.Clear();
                reports.Clear();
                attempts.Clear();
                messages.Clear();
            }

            packetsub1 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS, ReceivedPacket, MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
            packetsub2 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT, ReceivedPacket, MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
            packetsub3 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.STATUSTEXT, ReceivedPacket, MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
            subscribed = true;
        }

        public void Stop()
        {
            if (!subscribed)
                return;

            MainV2.comPort.UnSubscribeToPacketType(packetsub1);
            MainV2.comPort.UnSubscribeToPacketType(packetsub2);
            MainV2.comPort.UnSubscribeToPacketType(packetsub3);
            subscribed = false;
        }

        private bool ReceivedPacket(MAVLink.MAVLinkMessage packet)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                MainV2.comPort.DebugPacket(packet, true);

            lock (locker)
            {
                if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS)
                {
                    var obj = (MAVLink.mavlink_mag_cal_progress_t)packet.data;

                    // the autopilot starts a failed attempt over by itself, say so rather than just resetting the bar
                    if (attempts.TryGetValue(obj.compass_id, out var last) && obj.attempt > last)
                        AddMessage("Mag " + obj.compass_id + ": attempt " + last + " failed, retrying");

                    attempts[obj.compass_id] = obj.attempt;
                    progress[obj.compass_id] = obj;
                }
                else if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT)
                {
                    var obj = (MAVLink.mavlink_mag_cal_report_t)packet.data;

                    // skip empty reports, but never a failed one
                    if (obj.compass_id == 0 && obj.ofs_x == 0 && !IsFailed(obj))
                        return true;

                    reports[obj.compass_id] = obj;
                }
                else if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.STATUSTEXT)
                {
                    var text = Encoding.ASCII.GetString(packet.ToStructure<MAVLink.mavlink_statustext_t>().text);
                    var end = text.IndexOf('\0');
                    if (end >= 0)
                        text = text.Substring(0, end);
                    text = text.Trim();

                    var lower = text.ToLower();
                    if (!lower.StartsWith("prearm") && (lower.Contains("mag") || lower.Contains("compass")))
                        AddMessage(text);
                }
            }

            return true;
        }

        private void AddMessage(string text)
        {
            if (text != "" && !messages.Contains(text))
                messages.Add(text);
        }

        public static bool IsFailed(MAVLink.mavlink_mag_cal_report_t report)
        {
            return report.cal_status >= (byte)MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED;
        }

        /// <summary>
        /// every compass that started has either saved its result or failed
        /// </summary>
        public static bool IsComplete(List<MAVLink.mavlink_mag_cal_progress_t> progress, List<MAVLink.mavlink_mag_cal_report_t> reports)
        {
            var compasses = progress.Select(a => a.compass_id).Union(reports.Select(a => a.compass_id)).Count();
            var finished = reports.Count(a => IsFailed(a) || a.autosaved == 1);

            return compasses != 0 && compasses == finished;
        }

        /// <summary>
        /// why the autopilot rejected this compass, and what the user can do about it
        /// </summary>
        public static string Describe(MAVLink.mavlink_mag_cal_report_t report)
        {
            var fitlimit = MainV2.comPort.MAV.param["COMPASS_CAL_FIT"]?.Value;
            var offsmax = MainV2.comPort.MAV.param["COMPASS_OFFS_MAX"]?.Value;
            var offsets = report.ofs_x.ToString("0") + "/" + report.ofs_y.ToString("0") + "/" + report.ofs_z.ToString("0");
            var maxoffset = new[] { report.ofs_x, report.ofs_y, report.ofs_z }.Max(a => Math.Abs(a));

            switch ((MAVLink.MAG_CAL_STATUS)report.cal_status)
            {
                case MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED_ORIENTATION:
                    return "the compass orientation does not match the autopilot's. Check how the compass is mounted and its ORIENT parameter, or set COMPASS_AUTO_ROT to correct it automatically.";
                case MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED_RADIUS:
                    return "the measured field strength is outside the expected range. Move away from metal, magnets and power wires, and check the compass is not faulty.";
                case MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED_OFFSETS:
                    return "the offsets (" + offsets + ") are larger than COMPASS_OFFS_MAX allows. Something magnetic close to the compass is interfering with it.";
                case MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED_DIAG_SCALING:
                    return "the scale factors are out of range. This is usually magnetic interference or a faulty compass.";
                case MAVLink.MAG_CAL_STATUS.MAG_CAL_FAILED_RESIDUALS_HIGH:
                    return "the samples do not fit well enough. Rotate the vehicle smoothly through every orientation, away from interference.";
            }

            if (fitlimit != null && report.fitness > fitlimit)
                return "the fit is not good enough (fitness " + report.fitness.ToString("0.0") + ", COMPASS_CAL_FIT allows " +
                       fitlimit.Value.ToString("0.0") + "). Rotate the vehicle slowly through every orientation, away from metal, magnets and electronics, or raise COMPASS_CAL_FIT.";

            if (offsmax != null && maxoffset > offsmax)
                return "the offsets (" + offsets + ") are larger than COMPASS_OFFS_MAX (" + offsmax.Value.ToString("0") +
                       ") allows. Something magnetic close to the compass is interfering with it.";

            return "the result is outside what the autopilot accepts (fitness " + report.fitness.ToString("0.0") + ", offsets " +
                   offsets + "). Check for magnetic interference near the compass and try again.";
        }

        /// <summary>
        /// a message for the user once the calibration is complete, or null when every compass passed
        /// </summary>
        public string FailureMessage(List<MAVLink.mavlink_mag_cal_report_t> reports)
        {
            var failed = reports.Where(IsFailed).OrderBy(a => a.compass_id).ToList();
            if (failed.Count == 0)
                return null;

            var message = "Compass calibration failed\n\n" +
                          string.Join("\n\n", failed.Select(a => "Mag " + a.compass_id + ": " + Describe(a)));

            var autopilot = Messages;
            if (autopilot.Count > 0)
                message += "\n\nAutopilot messages:\n" + string.Join("\n", autopilot);

            if (reports.Any(a => !IsFailed(a) && a.autosaved == 1))
                message += "\n\nThe compasses that passed were saved, reboot the autopilot to use them.";

            return message;
        }
    }
}
